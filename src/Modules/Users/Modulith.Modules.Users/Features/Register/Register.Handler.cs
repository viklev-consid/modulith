using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modulith.Modules.Organizations.Contracts.Commands;
using Modulith.Modules.Organizations.Contracts.Queries;
using Modulith.Modules.Users.Contracts;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.Shared.Infrastructure.Persistence;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.Register;

public sealed class RegisterHandler(
    UsersDbContext db,
    IPasswordHasher passwordHasher,
    IOptions<UsersOptions> options,
    ISingleUseTokenService tokenService,
    IMessageBus bus,
    ILogger<RegisterHandler> logger,
    IClock clock)
{
    private static readonly Action<ILogger, Guid, string, Exception?> registrationCompensated =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Warning,
            new EventId(1001, nameof(registrationCompensated)),
            "Compensated registration for user {UserId} after organization invitation acceptance failed with {ErrorCodes}");

    public async Task<ErrorOr<RegisterResponse>> Handle(RegisterCommand cmd, CancellationToken ct)
        => await UsersTelemetry.InstrumentAsync(nameof(RegisterHandler), () => HandleCoreAsync(cmd, ct));

    private async Task<ErrorOr<RegisterResponse>> HandleCoreAsync(RegisterCommand cmd, CancellationToken ct)
    {
        var emailResult = Email.Create(cmd.Email);
        if (emailResult.IsError)
        {
            return emailResult.Errors;
        }

        var email = emailResult.Value;

        UserInvitation? invitation = null;
        ErrorOr<ValidateOrganizationInvitationForRegistrationResponse>? organizationInvitation = null;
        var registration = options.Value.Registration;
        if (registration.Mode == RegistrationMode.Disabled)
        {
            return UsersErrors.RegistrationUnavailable;
        }

        if (registration.Mode == RegistrationMode.InviteOnly)
        {
            if (string.IsNullOrWhiteSpace(cmd.InvitationToken) &&
                string.IsNullOrWhiteSpace(cmd.OrganizationInvitationToken))
            {
                return UsersErrors.RegistrationUnavailable;
            }

            if (!string.IsNullOrWhiteSpace(cmd.InvitationToken))
            {
                invitation = await LoadInvitationForTokenAsync(cmd.InvitationToken, ct);
                if (invitation is null)
                {
                    return UsersErrors.RegistrationUnavailable;
                }
            }
            else if (!string.IsNullOrWhiteSpace(cmd.OrganizationInvitationToken))
            {
                organizationInvitation = await ValidateOrganizationInvitationAsync(
                    cmd.OrganizationInvitationToken,
                    email.Value,
                    ct);
                if (organizationInvitation.Value.IsError)
                {
                    return UsersErrors.RegistrationUnavailable;
                }
            }
        }

        if (registration.Mode != RegistrationMode.InviteOnly &&
            !string.IsNullOrWhiteSpace(cmd.OrganizationInvitationToken))
        {
            organizationInvitation = await ValidateOrganizationInvitationAsync(
                cmd.OrganizationInvitationToken,
                email.Value,
                ct);
            if (organizationInvitation.Value.IsError)
            {
                return organizationInvitation.Value.Errors;
            }
        }

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            return registration.Mode == RegistrationMode.InviteOnly
                ? UsersErrors.RegistrationUnavailable
                : UsersErrors.EmailAlreadyRegistered;
        }

        var passwordHash = new PasswordHash(passwordHasher.Hash(cmd.Password));
        var userResult = User.CreateWithPassword(email, passwordHash, cmd.DisplayName);
        if (userResult.IsError)
        {
            return userResult.Errors;
        }

        var user = userResult.Value;

        if (invitation is not null)
        {
            var acceptResult = invitation.Accept(user.Id, email, clock);
            if (acceptResult.IsError)
            {
                return UsersErrors.RegistrationUnavailable;
            }
        }

        db.Users.Add(user);

        // Grant default consents on registration.
        db.Consents.Add(Consent.Grant(user.Id.Value, ConsentKeys.WelcomeEmail, clock.UtcNow));

        var (_, rawConfirmationToken) = tokenService.Create(
            user.Id,
            TokenPurpose.EmailConfirmation,
            options.Value.EmailConfirmationTokenLifetime);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            // A concurrent registration claimed the same email between our pre-check and commit.
            db.ChangeTracker.Clear();
            return registration.Mode == RegistrationMode.InviteOnly
                ? UsersErrors.RegistrationUnavailable
                : UsersErrors.EmailAlreadyRegistered;
        }

        if (!string.IsNullOrWhiteSpace(cmd.OrganizationInvitationToken))
        {
            var acceptOrganizationInvitation = await AcceptOrganizationInvitationOrCompensateAsync(
                cmd.OrganizationInvitationToken,
                user,
                ct);
            if (acceptOrganizationInvitation.IsError)
            {
                return acceptOrganizationInvitation.Errors;
            }
        }

        await bus.PublishAsync(new UserRegisteredV1(user.Id.Value, user.Email.Value, user.DisplayName, Guid.NewGuid()));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(UserRegisteredV1)));

        await bus.PublishAsync(new EmailConfirmationRequestedV1(
            user.Id.Value,
            user.Email.Value,
            user.DisplayName,
            rawConfirmationToken,
            Guid.NewGuid()));
        UsersTelemetry.EventsPublished.Add(1, new KeyValuePair<string, object?>("event", nameof(EmailConfirmationRequestedV1)));

        return new RegisterResponse(user.Id.Value);
    }

    private async Task<ErrorOr<Success>> AcceptOrganizationInvitationOrCompensateAsync(
        string organizationInvitationToken,
        User user,
        CancellationToken ct)
    {
        try
        {
            var result = await bus.InvokeAsync<ErrorOr<AcceptedOrganizationInvitationForUserResponse>>(
                new AcceptOrganizationInvitationForUserCommand(
                    organizationInvitationToken,
                    user.Id.Value,
                    user.Email.Value),
                ct);

            if (!result.IsError)
            {
                return Result.Success;
            }

            await CompensateRegisteredUserAsync(user, ct);
            registrationCompensated(
                logger,
                user.Id.Value,
                string.Join(",", result.Errors.Select(error => error.Code)),
                null);
            return result.Errors;
        }
        catch
        {
            await CompensateRegisteredUserAsync(user, ct);
            throw;
        }
    }

    private async Task CompensateRegisteredUserAsync(User user, CancellationToken ct)
    {
        await db.Consents
            .Where(consent => consent.UserId == user.Id.Value)
            .ExecuteDeleteAsync(ct);
        await db.SingleUseTokens
            .Where(token => token.UserId == user.Id)
            .ExecuteDeleteAsync(ct);

        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
    }

    private async Task<ErrorOr<ValidateOrganizationInvitationForRegistrationResponse>> ValidateOrganizationInvitationAsync(
        string rawToken,
        string email,
        CancellationToken ct) =>
        await bus.InvokeAsync<ErrorOr<ValidateOrganizationInvitationForRegistrationResponse>>(
            new ValidateOrganizationInvitationForRegistrationQuery(rawToken, email),
            ct);

    private async Task<UserInvitation?> LoadInvitationForTokenAsync(string rawToken, CancellationToken ct)
    {
        var tokenHash = UserInvitation.HashRawValue(rawToken);

        return await db.UserInvitations
            .FromSqlInterpolated($"""
                SELECT * FROM users.user_invitations
                WHERE token_hash = {tokenHash}
                FOR UPDATE
                """)
            .FirstOrDefaultAsync(ct);
    }
}
