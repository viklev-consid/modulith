using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Users.Domain.Events;

/// <summary>Internal domain event raised after a password reset token is verified and the password is changed.</summary>
public sealed record PasswordReset(UserId UserId) : DomainEvent;
