using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Users.Domain.Events;

/// <summary>Internal domain event raised when a password reset is requested.</summary>
public sealed record PasswordResetRequested(UserId UserId, string Email) : DomainEvent;
