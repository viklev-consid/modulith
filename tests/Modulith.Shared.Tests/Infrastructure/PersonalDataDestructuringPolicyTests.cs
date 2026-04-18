using Modulith.Shared.Infrastructure.Logging;
using Modulith.Shared.Kernel.Gdpr;
using Serilog.Events;

namespace Modulith.Shared.Tests.Infrastructure;

[Trait("Category", "Unit")]
public sealed class PersonalDataDestructuringPolicyTests
{
    private readonly PersonalDataDestructuringPolicy _policy = new();

    private sealed class FakePropertyValueFactory : Serilog.Core.ILogEventPropertyValueFactory
    {
        public LogEventPropertyValue CreatePropertyValue(object? value, bool destructureObjects = false)
            => new ScalarValue(value);
    }

    private sealed class UserDto
    {
        public string? Username { get; init; }

        [PersonalData]
        public string? Email { get; init; }

        [SensitivePersonalData]
        public string? Password { get; init; }

        public string? Role { get; init; }
    }

    private sealed class NoPersonalDataDto
    {
        public string? Name { get; init; }
        public int Count { get; init; }
    }

    [Fact]
    public void TryDestructure_ObjectWithPersonalData_ReturnsTrueAndMasksFields()
    {
        var dto = new UserDto
        {
            Username = "alice",
            Email = "alice@example.com",
            Password = "secret123",
            Role = "admin",
        };

        var ok = _policy.TryDestructure(dto, new FakePropertyValueFactory(), out var result);

        Assert.True(ok);
        var structure = Assert.IsType<StructureValue>(result);
        var props = structure.Properties.ToDictionary(p => p.Name, p => p.Value);

        Assert.Equal("alice", ((ScalarValue)props["Username"]).Value);
        Assert.Equal("***", ((ScalarValue)props["Email"]).Value);
        Assert.Equal("***", ((ScalarValue)props["Password"]).Value);
        Assert.Equal("admin", ((ScalarValue)props["Role"]).Value);
    }

    [Fact]
    public void TryDestructure_ObjectWithoutPersonalData_ReturnsFalse()
    {
        var dto = new NoPersonalDataDto { Name = "test", Count = 5 };

        var ok = _policy.TryDestructure(dto, new FakePropertyValueFactory(), out var result);

        Assert.False(ok);
        Assert.Null(result);
    }

    [Fact]
    public void TryDestructure_PropertyWithSensitiveNamePattern_MasksIt()
    {
        var dto = new UserDto
        {
            Username = "bob",
            Email = "bob@example.com",
            Password = "p@ssword",
            Role = "user",
        };

        _policy.TryDestructure(dto, new FakePropertyValueFactory(), out var result);

        var structure = Assert.IsType<StructureValue>(result);
        var props = structure.Properties.ToDictionary(p => p.Name, p => p.Value);

        // "Password" matches the name pattern "password"
        Assert.Equal("***", ((ScalarValue)props["Password"]).Value);
    }
}
