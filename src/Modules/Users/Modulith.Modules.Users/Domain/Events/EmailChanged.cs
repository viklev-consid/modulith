using Modulith.Shared.Kernel.Domain;

namespace Modulith.Modules.Users.Domain.Events;

/// <summary>Internal domain event raised after an email change is confirmed.</summary>
public sealed record EmailChanged(UserId UserId, string OldEmail, string NewEmail) : DomainEvent;
