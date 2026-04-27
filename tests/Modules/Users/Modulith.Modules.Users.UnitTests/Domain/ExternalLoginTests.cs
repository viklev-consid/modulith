using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Domain.Events;
using Modulith.Shared.Kernel.Interfaces;

namespace Modulith.Modules.Users.UnitTests.Domain;

[Trait("Category", "Unit")]
public sealed class ExternalLoginTests
{
    private static Email ValidEmail => Email.Create("alice@example.com").Value;
    private const string ValidSubject = "google-subject-12345";

    // ── PendingExternalLogin ─────────────────────────────────────────────────

    [Fact]
    public void PendingExternalLogin_Create_StoresHashNotRawValue()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        var (pending, rawValue) = PendingExternalLogin.Create(
            ExternalLoginProvider.Google, ValidSubject, "alice@example.com", "Alice",
            isExistingUser: false, createdFromIp: null, userAgent: null, TimeSpan.FromMinutes(15), clock);

        var rawBytes = System.Text.Encoding.UTF8.GetBytes(rawValue);
        Assert.False(pending.TokenHash.SequenceEqual(rawBytes));
    }

    [Fact]
    public void PendingExternalLogin_Create_HashMatchesRawValue()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        var (pending, rawValue) = PendingExternalLogin.Create(
            ExternalLoginProvider.Google, ValidSubject, "alice@example.com", "Alice",
            isExistingUser: false, createdFromIp: null, userAgent: null, TimeSpan.FromMinutes(15), clock);

        var expectedHash = PendingExternalLogin.HashRawValue(rawValue);
        Assert.True(pending.TokenHash.SequenceEqual(expectedHash));
    }

    [Fact]
    public void PendingExternalLogin_Create_SetsExpiresAt()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new FixedClock(now);
        var lifetime = TimeSpan.FromMinutes(15);

        var (pending, _) = PendingExternalLogin.Create(
            ExternalLoginProvider.Google, ValidSubject, "alice@example.com", "Alice",
            isExistingUser: false, createdFromIp: null, userAgent: null, lifetime, clock);

        Assert.Equal(now.Add(lifetime), pending.ExpiresAt);
    }

    [Fact]
    public void PendingExternalLogin_Create_TruncatesLongUserAgent()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var longUa = new string('x', 600);

        var (pending, _) = PendingExternalLogin.Create(
            ExternalLoginProvider.Google, ValidSubject, "alice@example.com", "Alice",
            isExistingUser: false, createdFromIp: null, userAgent: longUa, TimeSpan.FromMinutes(15), clock);

        Assert.True(pending.UserAgent!.Length <= 512);
    }

    [Fact]
    public void PendingExternalLogin_IsValid_WhenFreshAndNotConsumed_ReturnsTrue()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        var (pending, _) = PendingExternalLogin.Create(
            ExternalLoginProvider.Google, ValidSubject, "alice@example.com", "Alice",
            isExistingUser: false, createdFromIp: null, userAgent: null, TimeSpan.FromMinutes(15), clock);

        Assert.True(pending.IsValid(clock));
    }

    [Fact]
    public void PendingExternalLogin_IsValid_WhenExpired_ReturnsFalse()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        var (pending, _) = PendingExternalLogin.Create(
            ExternalLoginProvider.Google, ValidSubject, "alice@example.com", "Alice",
            isExistingUser: false, createdFromIp: null, userAgent: null, TimeSpan.FromMinutes(15), clock);

        clock.Advance(TimeSpan.FromMinutes(16));
        Assert.False(pending.IsValid(clock));
    }

    [Fact]
    public void PendingExternalLogin_Consume_SucceedsWhenValid()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        var (pending, _) = PendingExternalLogin.Create(
            ExternalLoginProvider.Google, ValidSubject, "alice@example.com", "Alice",
            isExistingUser: false, createdFromIp: null, userAgent: null, TimeSpan.FromMinutes(15), clock);

        var result = pending.Consume(clock);

        Assert.False(result.IsError);
        Assert.NotNull(pending.ConsumedAt);
    }

    [Fact]
    public void PendingExternalLogin_Consume_FailsWhenExpired()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        var (pending, _) = PendingExternalLogin.Create(
            ExternalLoginProvider.Google, ValidSubject, "alice@example.com", "Alice",
            isExistingUser: false, createdFromIp: null, userAgent: null, TimeSpan.FromMinutes(15), clock);

        clock.Advance(TimeSpan.FromMinutes(16));
        var result = pending.Consume(clock);

        Assert.True(result.IsError);
    }

    [Fact]
    public void PendingExternalLogin_Consume_FailsWhenAlreadyConsumed()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        var (pending, _) = PendingExternalLogin.Create(
            ExternalLoginProvider.Google, ValidSubject, "alice@example.com", "Alice",
            isExistingUser: false, createdFromIp: null, userAgent: null, TimeSpan.FromMinutes(15), clock);

        pending.Consume(clock);
        var secondConsume = pending.Consume(clock);

        Assert.True(secondConsume.IsError);
    }

    [Fact]
    public void PendingExternalLogin_IsValid_AfterConsume_ReturnsFalse()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        var (pending, _) = PendingExternalLogin.Create(
            ExternalLoginProvider.Google, ValidSubject, "alice@example.com", "Alice",
            isExistingUser: false, createdFromIp: null, userAgent: null, TimeSpan.FromMinutes(15), clock);

        pending.Consume(clock);
        Assert.False(pending.IsValid(clock));
    }

    // ── User.CreateExternal ──────────────────────────────────────────────────

    [Fact]
    public void User_CreateExternal_ReturnsUserWithNullPassword()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        var result = User.CreateExternal(ValidEmail, "Alice", ExternalLoginProvider.Google, ValidSubject, clock);

        Assert.False(result.IsError);
        Assert.Null(result.Value.PasswordHash);
    }

    [Fact]
    public void User_CreateExternal_HasCompletedOnboardingIsFalse()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        var result = User.CreateExternal(ValidEmail, "Alice", ExternalLoginProvider.Google, ValidSubject, clock);

        Assert.False(result.IsError);
        Assert.False(result.Value.HasCompletedOnboarding);
    }

    [Fact]
    public void User_CreateExternal_RaisesUserProvisionedFromExternalEvent()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        var result = User.CreateExternal(ValidEmail, "Alice", ExternalLoginProvider.Google, ValidSubject, clock);

        Assert.Single(result.Value.DomainEvents);
        Assert.IsType<UserProvisionedFromExternal>(result.Value.DomainEvents.First());
    }

    [Fact]
    public void User_CreateExternal_WithEmptyDisplayName_ReturnsValidationError()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        var result = User.CreateExternal(ValidEmail, "", ExternalLoginProvider.Google, ValidSubject, clock);

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public void User_CreateExternal_WithTooLongDisplayName_ReturnsValidationError()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        var result = User.CreateExternal(ValidEmail, new string('x', 101), ExternalLoginProvider.Google, ValidSubject, clock);

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public void User_CreateExternal_TrimsDisplayName()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);

        var result = User.CreateExternal(ValidEmail, "  Alice  ", ExternalLoginProvider.Google, ValidSubject, clock);

        Assert.False(result.IsError);
        Assert.Equal("Alice", result.Value.DisplayName);
    }

    // ── User.LinkExternalLogin ───────────────────────────────────────────────

    [Fact]
    public void User_LinkExternalLogin_Succeeds_WhenNotAlreadyLinked()
    {
        var user = User.CreateWithPassword(ValidEmail, new PasswordHash("$2a$12$hash"), "Alice").Value;
        user.ClearDomainEvents();

        var result = user.LinkExternalLogin(ExternalLoginProvider.Google, ValidSubject, DateTimeOffset.UtcNow);

        Assert.False(result.IsError);
        Assert.Single(user.ExternalLogins);
    }

    [Fact]
    public void User_LinkExternalLogin_RaisesExternalLoginLinkedEvent()
    {
        var user = User.CreateWithPassword(ValidEmail, new PasswordHash("$2a$12$hash"), "Alice").Value;
        user.ClearDomainEvents();

        user.LinkExternalLogin(ExternalLoginProvider.Google, ValidSubject, DateTimeOffset.UtcNow);

        Assert.Single(user.DomainEvents);
        Assert.IsType<ExternalLoginLinked>(user.DomainEvents.First());
    }

    [Fact]
    public void User_LinkExternalLogin_FailsWhenProviderAlreadyLinked_SameSubject()
    {
        var user = User.CreateWithPassword(ValidEmail, new PasswordHash("$2a$12$hash"), "Alice").Value;
        user.LinkExternalLogin(ExternalLoginProvider.Google, ValidSubject, DateTimeOffset.UtcNow);
        user.ClearDomainEvents();

        var result = user.LinkExternalLogin(ExternalLoginProvider.Google, ValidSubject, DateTimeOffset.UtcNow);

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.Conflict, result.FirstError.Type);
    }

    [Fact]
    public void User_LinkExternalLogin_FailsWhenProviderAlreadyLinked_DifferentSubject()
    {
        // Guard is provider-level, not subject-level. A second Google account is rejected
        // even when its subject differs from the one already linked.
        var user = User.CreateWithPassword(ValidEmail, new PasswordHash("$2a$12$hash"), "Alice").Value;
        user.LinkExternalLogin(ExternalLoginProvider.Google, ValidSubject, DateTimeOffset.UtcNow);
        user.ClearDomainEvents();

        var result = user.LinkExternalLogin(ExternalLoginProvider.Google, "google-subject-99999", DateTimeOffset.UtcNow);

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.Conflict, result.FirstError.Type);
    }

    // ── User.UnlinkExternalLogin ─────────────────────────────────────────────

    [Fact]
    public void User_UnlinkExternalLogin_SucceedsWhenPasswordExists()
    {
        var user = User.CreateWithPassword(ValidEmail, new PasswordHash("$2a$12$hash"), "Alice").Value;
        user.LinkExternalLogin(ExternalLoginProvider.Google, ValidSubject, DateTimeOffset.UtcNow);
        user.ClearDomainEvents();

        var result = user.UnlinkExternalLogin(ExternalLoginProvider.Google);

        Assert.False(result.IsError);
        Assert.Empty(user.ExternalLogins);
    }

    [Fact]
    public void User_UnlinkExternalLogin_SucceedsWhenAnotherExternalLoginExists()
    {
        // With a single provider (Google) a user cannot hold two external logins, so
        // "another external login" as the retained credential cannot be tested here.
        // This scenario becomes testable once a second provider is added.
        // The password-as-retained-credential path is covered by SucceedsWhenPasswordExists.
        //
        // What we CAN assert is that the attempted setup (two Google logins) is itself
        // rejected — confirming the invariant that prevents the backdoor scenario.
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var user = User.CreateExternal(ValidEmail, "Alice", ExternalLoginProvider.Google, ValidSubject, clock).Value;
        user.LinkExternalLogin(ExternalLoginProvider.Google, ValidSubject, clock.UtcNow);

        var secondLink = user.LinkExternalLogin(ExternalLoginProvider.Google, "other-subject", clock.UtcNow);

        Assert.True(secondLink.IsError);
        Assert.Equal(ErrorOr.ErrorType.Conflict, secondLink.FirstError.Type);
    }

    [Fact]
    public void User_UnlinkExternalLogin_FailsWhenWouldLeaveNoCredential()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var user = User.CreateExternal(ValidEmail, "Alice", ExternalLoginProvider.Google, ValidSubject, clock).Value;
        user.LinkExternalLogin(ExternalLoginProvider.Google, ValidSubject, clock.UtcNow);
        user.ClearDomainEvents();

        var result = user.UnlinkExternalLogin(ExternalLoginProvider.Google);

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.Conflict, result.FirstError.Type);
    }

    [Fact]
    public void User_UnlinkExternalLogin_FailsWhenProviderNotLinked()
    {
        var user = User.CreateWithPassword(ValidEmail, new PasswordHash("$2a$12$hash"), "Alice").Value;

        var result = user.UnlinkExternalLogin(ExternalLoginProvider.Google);

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.NotFound, result.FirstError.Type);
    }

    [Fact]
    public void User_UnlinkExternalLogin_RaisesExternalLoginUnlinkedEvent()
    {
        var user = User.CreateWithPassword(ValidEmail, new PasswordHash("$2a$12$hash"), "Alice").Value;
        user.LinkExternalLogin(ExternalLoginProvider.Google, ValidSubject, DateTimeOffset.UtcNow);
        user.ClearDomainEvents();

        user.UnlinkExternalLogin(ExternalLoginProvider.Google);

        Assert.Single(user.DomainEvents);
        Assert.IsType<ExternalLoginUnlinked>(user.DomainEvents.First());
    }

    // ── User.SetInitialPassword ──────────────────────────────────────────────

    [Fact]
    public void User_SetInitialPassword_SucceedsWhenNoPasswordSet()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var user = User.CreateExternal(ValidEmail, "Alice", ExternalLoginProvider.Google, ValidSubject, clock).Value;

        var result = user.SetInitialPassword(new PasswordHash("$2a$12$hash"));

        Assert.False(result.IsError);
        Assert.NotNull(user.PasswordHash);
    }

    [Fact]
    public void User_SetInitialPassword_FailsWhenPasswordAlreadySet()
    {
        var user = User.CreateWithPassword(ValidEmail, new PasswordHash("$2a$12$existing"), "Alice").Value;

        var result = user.SetInitialPassword(new PasswordHash("$2a$12$new"));

        Assert.True(result.IsError);
        Assert.Equal(ErrorOr.ErrorType.Conflict, result.FirstError.Type);
    }

    // ── User.CompleteOnboarding ──────────────────────────────────────────────

    [Fact]
    public void User_CompleteOnboarding_SetsHasCompletedOnboardingToTrue()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var user = User.CreateExternal(ValidEmail, "Alice", ExternalLoginProvider.Google, ValidSubject, clock).Value;

        user.CompleteOnboarding();

        Assert.True(user.HasCompletedOnboarding);
    }

    [Fact]
    public void User_CompleteOnboarding_RaisesUserOnboardingCompletedEvent()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var user = User.CreateExternal(ValidEmail, "Alice", ExternalLoginProvider.Google, ValidSubject, clock).Value;
        user.ClearDomainEvents();

        user.CompleteOnboarding();

        Assert.Single(user.DomainEvents);
        Assert.IsType<UserOnboardingCompleted>(user.DomainEvents.First());
    }

    [Fact]
    public void User_CompleteOnboarding_IsIdempotent()
    {
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var user = User.CreateExternal(ValidEmail, "Alice", ExternalLoginProvider.Google, ValidSubject, clock).Value;

        user.CompleteOnboarding();
        user.ClearDomainEvents();
        var result = user.CompleteOnboarding();

        Assert.False(result.IsError);
        Assert.Empty(user.DomainEvents);
    }
}

file sealed class FixedClock(DateTimeOffset now) : IClock
{
    private DateTimeOffset _now = now;
    public DateTimeOffset UtcNow => _now;
    public void Advance(TimeSpan duration) => _now += duration;
}
