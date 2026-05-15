using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Domain.Events;

namespace Modulith.Modules.Users.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class UserTests
{
    private static Email validEmail => Email.Create("alice@example.com").Value;
    private static PasswordHash validHash => new("$2a$12$hashed");

    [Fact]
    public void Create_WithValidArguments_ReturnsUser()
    {
        var result = User.CreateWithPassword(validEmail, validHash, "Alice");

        Assert.False(result.IsError);
        Assert.Equal(validEmail, result.Value.Email);
        Assert.Equal("Alice", result.Value.DisplayName);
    }

    [Fact]
    public void Create_RaisesUserRegisteredEvent()
    {
        var result = User.CreateWithPassword(validEmail, validHash, "Alice");

        Assert.Single(result.Value.DomainEvents);
        Assert.IsType<UserRegistered>(result.Value.DomainEvents.First());
    }

    [Fact]
    public void Create_WithEmptyDisplayName_ReturnsValidationError()
    {
        var result = User.CreateWithPassword(validEmail, validHash, "");

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public void Create_WithTooLongDisplayName_ReturnsValidationError()
    {
        var result = User.CreateWithPassword(validEmail, validHash, new string('x', 101));

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public void Create_TrimsDisplayName()
    {
        var result = User.CreateWithPassword(validEmail, validHash, "  Alice  ");

        Assert.False(result.IsError);
        Assert.Equal("Alice", result.Value.DisplayName);
    }

    [Fact]
    public void ChangeEmail_ToDifferentEmail_Succeeds()
    {
        var user = User.CreateWithPassword(validEmail, validHash, "Alice").Value;
        user.ClearDomainEvents();
        var newEmail = Email.Create("alice-new@example.com").Value;

        var result = user.ChangeEmail(newEmail);

        Assert.False(result.IsError);
        Assert.Equal(newEmail, user.Email);
        Assert.Single(user.DomainEvents);
        Assert.IsType<UserEmailChanged>(user.DomainEvents.First());
    }

    [Fact]
    public void ChangeEmail_ToSameEmail_ReturnsConflict()
    {
        var user = User.CreateWithPassword(validEmail, validHash, "Alice").Value;

        var result = user.ChangeEmail(validEmail);

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.Conflict, result.FirstError.Type);
    }

    [Fact]
    public void UpdateProfile_WithDifferentDisplayName_UpdatesAndRaisesEvent()
    {
        var user = User.CreateWithPassword(validEmail, validHash, "Alice").Value;
        user.ClearDomainEvents();

        var result = user.UpdateProfile("Alice Updated");

        Assert.False(result.IsError);
        Assert.Equal("Alice Updated", user.DisplayName);
        var domainEvent = Assert.Single(user.DomainEvents);
        var profileUpdated = Assert.IsType<UserProfileUpdated>(domainEvent);
        Assert.Equal("Alice", profileUpdated.OldDisplayName);
        Assert.Equal("Alice Updated", profileUpdated.NewDisplayName);
    }

    [Fact]
    public void UpdateProfile_TrimsBeforeStoring()
    {
        var user = User.CreateWithPassword(validEmail, validHash, "Alice").Value;
        user.ClearDomainEvents();

        var result = user.UpdateProfile("  Trimmed  ");

        Assert.False(result.IsError);
        Assert.Equal("Trimmed", user.DisplayName);
    }

    [Fact]
    public void UpdateProfile_WithSameDisplayName_IsNoOpAndRaisesNoEvent()
    {
        var user = User.CreateWithPassword(validEmail, validHash, "Alice").Value;
        user.ClearDomainEvents();

        var result = user.UpdateProfile("Alice");

        Assert.False(result.IsError);
        Assert.Equal("Alice", user.DisplayName);
        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public void UpdateProfile_WithSameDisplayNameAfterTrim_IsNoOpAndRaisesNoEvent()
    {
        var user = User.CreateWithPassword(validEmail, validHash, "Alice").Value;
        user.ClearDomainEvents();

        var result = user.UpdateProfile("  Alice  ");

        Assert.False(result.IsError);
        Assert.Equal("Alice", user.DisplayName);
        Assert.Empty(user.DomainEvents);
    }

    [Fact]
    public void UpdateProfile_WithEmptyDisplayName_ReturnsValidationError()
    {
        var user = User.CreateWithPassword(validEmail, validHash, "Alice").Value;

        var result = user.UpdateProfile("");

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public void UpdateProfile_WithWhitespaceDisplayName_ReturnsValidationError()
    {
        var user = User.CreateWithPassword(validEmail, validHash, "Alice").Value;

        var result = user.UpdateProfile("   ");

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public void UpdateProfile_WithTooLongDisplayName_ReturnsValidationError()
    {
        var user = User.CreateWithPassword(validEmail, validHash, "Alice").Value;

        var result = user.UpdateProfile(new string('x', 101));

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public void UpdateProfile_LengthCheckAppliesAfterTrim()
    {
        var user = User.CreateWithPassword(validEmail, validHash, "Alice").Value;
        user.ClearDomainEvents();

        var paddedHundred = "  " + new string('x', 100) + "  ";
        var result = user.UpdateProfile(paddedHundred);

        Assert.False(result.IsError);
        Assert.Equal(new string('x', 100), user.DisplayName);
    }

    [Fact]
    public void User_HasNoPublicSetters()
    {
        var publicSetters = typeof(User)
            .GetProperties()
            .Where(p => p.SetMethod?.IsPublic == true)
            .ToList();

        Assert.Empty(publicSetters);
    }
}
