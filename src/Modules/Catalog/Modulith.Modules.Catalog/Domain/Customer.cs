using Modulith.Shared.Kernel.Domain;
using Modulith.Shared.Kernel.Gdpr;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Catalog.Domain;

public sealed class Customer : Entity<CustomerId>, IAuditableEntity
{
    private Customer(CustomerId id, Guid userId, string email, string displayName)
        : base(id)
    {
        UserId = userId;
        Email = email;
        DisplayName = displayName;
    }

    private Customer() : base(default!) { }

    [PersonalData]
    public Guid UserId { get; private set; }

    [PersonalData]
    public string Email { get; private set; } = null!;

    [PersonalData]
    public string DisplayName { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    public static Customer FromUserRegistered(Guid userId, string email, string displayName)
        => new(CustomerId.New(), userId, email, displayName);

    public void Anonymize()
    {
        UserId = Guid.NewGuid();
        Email = $"deleted-{Id.Value:N}@example.invalid";
        DisplayName = "Deleted User";
    }

    public void UpdateEmail(string email) => Email = email;

    public void UpdateDisplayName(string displayName) => DisplayName = displayName;
}
