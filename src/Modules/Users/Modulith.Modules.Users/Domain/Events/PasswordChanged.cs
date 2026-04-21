using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Users.Domain.Events;

/// <summary>Internal domain event raised when an authenticated user changes their password.</summary>
public sealed record PasswordChanged(UserId UserId) : DomainEvent;
