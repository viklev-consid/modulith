using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Domain.Events;

namespace Modulith.Modules.Users.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class UserTests
{
    private static Email ValidEmail => Email.Create("alice@example.com").Value;
    private static PasswordHash ValidHash => new("$2a$12$hashed");

    [Fact]
    public void Create_WithValidArguments_ReturnsUser()
    {
        var result = User.CreateWithPassword(ValidEmail, ValidHash, "Alice");

        Assert.False(result.IsError);
        Assert.Equal(ValidEmail, result.Value.Email);
        Assert.Equal("Alice", result.Value.DisplayName);
    }

    [Fact]
    public void Create_RaisesUserRegisteredEvent()
    {
        var result = User.CreateWithPassword(ValidEmail, ValidHash, "Alice");

        Assert.Single(result.Value.DomainEvents);
        Assert.IsType<UserRegistered>(result.Value.DomainEvents.First());
    }

    [Fact]
    public void Create_WithEmptyDisplayName_ReturnsValidationError()
    {
        var result = User.CreateWithPassword(ValidEmail, ValidHash, "");

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public void Create_WithTooLongDisplayName_ReturnsValidationError()
    {
        var result = User.CreateWithPassword(ValidEmail, ValidHash, new string('x', 101));

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public void Create_TrimsDisplayName()
    {
        var result = User.CreateWithPassword(ValidEmail, ValidHash, "  Alice  ");

        Assert.False(result.IsError);
        Assert.Equal("Alice", result.Value.DisplayName);
    }

    [Fact]
    public void ChangeEmail_ToDifferentEmail_Succeeds()
    {
        var user = User.CreateWithPassword(ValidEmail, ValidHash, "Alice").Value;
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
        var user = User.CreateWithPassword(ValidEmail, ValidHash, "Alice").Value;

        var result = user.ChangeEmail(ValidEmail);

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.Conflict, result.FirstError.Type);
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
