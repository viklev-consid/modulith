using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Domain.Events;

namespace Modulith.Modules.Users.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class RoleTests
{
    [Fact]
    public void Role_Admin_HasCorrectName()
    {
        Assert.Equal("admin", Role.Admin.Name);
    }

    [Fact]
    public void Role_User_HasCorrectName()
    {
        Assert.Equal("user", Role.User.Name);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("user")]
    [InlineData("moderator")]
    [InlineData("support-agent")]
    [InlineData("super_admin")]
    public void Role_Create_WithValidName_Succeeds(string name)
    {
        var result = Role.Create(name);

        Assert.False(result.IsError);
        Assert.Equal(name, result.Value.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ADMIN")]
    [InlineData("has space")]
    [InlineData("a")]                                 // too short
    [InlineData("this-name-is-way-too-long-to-be-a-valid-role-name-here")]
    public void Role_Create_WithInvalidName_ReturnsError(string name)
    {
        var result = Role.Create(name);

        Assert.True(result.IsError);
    }

    [Fact]
    public void Role_Equality_SameNameAreEqual()
    {
        var a = Role.Create("admin").Value;
        var b = Role.Create("admin").Value;

        Assert.Equal(a, b);
    }

    [Fact]
    public void Role_Equality_DifferentNamesAreNotEqual()
    {
        Assert.NotEqual(Role.Admin, Role.User);
    }
}

[Trait("Category", "Unit")]
public sealed class UserChangeRoleTests
{
    private static Email ValidEmail => Email.Create("alice@example.com").Value;
    private static PasswordHash ValidHash => new("$2a$12$hashed");

    [Fact]
    public void ChangeRole_ToNewRole_Succeeds()
    {
        var user = User.Create(ValidEmail, ValidHash, "Alice").Value;
        var changerId = user.Id;

        var result = user.ChangeRole(Role.Admin, changerId);

        Assert.False(result.IsError);
        Assert.Equal(Role.Admin, user.Role);
    }

    [Fact]
    public void ChangeRole_ToNewRole_RaisesUserRoleChangedEvent()
    {
        var user = User.Create(ValidEmail, ValidHash, "Alice").Value;
        user.ClearDomainEvents();
        var changerId = user.Id;

        user.ChangeRole(Role.Admin, changerId);

        Assert.Single(user.DomainEvents);
        var evt = Assert.IsType<UserRoleChanged>(user.DomainEvents.First());
        Assert.Equal("user", evt.OldRole);
        Assert.Equal("admin", evt.NewRole);
    }

    [Fact]
    public void ChangeRole_ToSameRole_ReturnsConflict()
    {
        var user = User.Create(ValidEmail, ValidHash, "Alice").Value;
        var changerId = user.Id;

        var result = user.ChangeRole(Role.User, changerId);

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.Conflict, result.FirstError.Type);
    }

    [Fact]
    public void Create_DefaultRole_IsUser()
    {
        var user = User.Create(ValidEmail, ValidHash, "Alice").Value;

        Assert.Equal(Role.User, user.Role);
    }

    [Fact]
    public void Create_WithAdminRole_HasAdminRole()
    {
        var user = User.Create(ValidEmail, ValidHash, "Alice", Role.Admin).Value;

        Assert.Equal(Role.Admin, user.Role);
    }
}
