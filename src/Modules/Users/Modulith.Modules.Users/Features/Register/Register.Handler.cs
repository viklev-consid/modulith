using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Contracts;
using Modulith.Modules.Users.Contracts.Events;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Errors;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.Shared.Kernel.Interfaces;
using Wolverine;

namespace Modulith.Modules.Users.Features.Register;

public sealed class RegisterHandler(
    UsersDbContext db,
    IPasswordHasher passwordHasher,
    IJwtGenerator jwtGenerator,
    IMessageBus bus,
    IClock clock)
{
    public async Task<ErrorOr<RegisterResponse>> Handle(RegisterCommand cmd, CancellationToken ct)
    {
        var emailResult = Email.Create(cmd.Email);
        if (emailResult.IsError)
            return emailResult.Errors;

        var email = emailResult.Value;

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return UsersErrors.EmailAlreadyRegistered;

        var passwordHash = new PasswordHash(passwordHasher.Hash(cmd.Password));
        var userResult = User.Create(email, passwordHash, cmd.DisplayName);
        if (userResult.IsError)
            return userResult.Errors;

        var user = userResult.Value;
        db.Users.Add(user);

        // Grant default consents on registration.
        db.Consents.Add(Consent.Grant(user.Id.Value, ConsentKeys.WelcomeEmail, clock.UtcNow));

        await db.SaveChangesAsync(ct);

        await bus.PublishAsync(new UserRegisteredV1(user.Id.Value, user.Email.Value, user.DisplayName));

        var token = jwtGenerator.Generate(user.Id, user.Email.Value, user.DisplayName);
        return new RegisterResponse(user.Id.Value, token);
    }
}
