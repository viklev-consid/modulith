using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Modulith.Modules.Notifications;
using Modulith.Modules.Notifications.Contracts.Commands;
using Modulith.Modules.Notifications.Contracts.Dtos;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Features.GetMyNotificationPreferences;
using Modulith.Modules.Notifications.Features.GetUnreadNotificationCount;
using Modulith.Modules.Notifications.Features.ListMyNotifications;
using Modulith.Modules.Notifications.Features.UpdateMyNotificationPreferences;
using Modulith.Modules.Notifications.Jobs;
using Modulith.Modules.Notifications.Persistence;
using Modulith.Modules.Notifications.Streaming;
using Wolverine;

namespace Modulith.Modules.Notifications.IntegrationTests.Features;

[Collection("NotificationsCrossModule")]
[Trait("Category", "Integration")]
public sealed class BellNotificationsTests(NotificationsCrossModuleFixture fixture) : IAsyncLifetime
{
    private readonly Guid userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly Guid otherUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateNotification_ProductDefault_PersistsBellNotification()
    {
        var notificationId = await CreateProductNotificationAsync(userId, "ticket.reply.created", Guid.NewGuid());

        var client = fixture.CreateAuthenticatedClient(userId, "owner@example.test", "Owner");
        var response = await client.GetFromJsonAsync<ListMyNotificationsResponse>("/v1/me/notifications");

        Assert.NotNull(notificationId);
        var item = Assert.Single(response!.Items);
        Assert.Equal(notificationId, item.Id);
        Assert.Equal("ticket.reply.created", item.Type);
        Assert.Equal(NotificationCategory.Product, item.Category);
        Assert.Equal(NotificationSeverity.Info, item.Severity);
        Assert.False(item.IsRead);
        Assert.Equal("/tickets/123", item.Link!.Href);
    }

    [Fact]
    public async Task CreateNotification_WithSameIdempotencyKey_DoesNotDuplicateBellNotification()
    {
        var idempotencyKey = Guid.NewGuid();

        var first = await CreateProductNotificationAsync(userId, "ticket.reply.created", idempotencyKey);
        var second = await CreateProductNotificationAsync(userId, "ticket.reply.created", idempotencyKey);

        var count = await fixture.QueryDbAsync<NotificationsDbContext, int>(
            (db, ct) => db.UserNotifications.CountAsync(n => n.RecipientUserId == userId, ct));

        Assert.Equal(first, second);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CreateNotification_WithSameIdempotencyKeyForDifferentRecipients_CreatesOnePerRecipient()
    {
        var idempotencyKey = Guid.NewGuid();

        var first = await CreateProductNotificationAsync(userId, "ticket.reply.created", idempotencyKey);
        var second = await CreateProductNotificationAsync(otherUserId, "ticket.reply.created", idempotencyKey);

        var count = await fixture.QueryDbAsync<NotificationsDbContext, int>(
            (db, ct) => db.UserNotifications.CountAsync(n => n.IdempotencyKey == idempotencyKey, ct));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task StreamPublisher_WhenSubscriberIsClosed_DoesNotThrow()
    {
        var publisher = new InMemoryNotificationStreamPublisher(Options.Create(new NotificationsOptions()));
        var channel = Channel.CreateUnbounded<NotificationStreamEvent>();
        var registration = new ChannelWriterRegistration(channel.Writer);
        publisher.Subscribe(userId, "tab-1", registration);
        channel.Writer.TryComplete();

        var exception = await Record.ExceptionAsync(async () =>
            await publisher.PublishAsync(
                userId,
                new NotificationStreamEvent("notification.created", "{}"),
                CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public void StreamPublisher_WhenUserHasMaximumDistinctStreamClients_RejectsAdditionalSubscriptions()
    {
        var publisher = new InMemoryNotificationStreamPublisher(Options.Create(new NotificationsOptions
        {
            Stream = new NotificationsOptions.StreamOptions
            {
                MaxActiveStreamsPerUser = 1,
                ChannelCapacity = 100,
            },
        }));
        var firstChannel = Channel.CreateBounded<NotificationStreamEvent>(1);
        var secondChannel = Channel.CreateBounded<NotificationStreamEvent>(1);
        var first = new ChannelWriterRegistration(firstChannel.Writer);
        var second = new ChannelWriterRegistration(secondChannel.Writer);

        var firstSubscription = publisher.Subscribe(userId, "tab-1", first);
        var secondSubscription = publisher.Subscribe(userId, "tab-2", second);

        Assert.False(firstSubscription.IsError);
        Assert.True(secondSubscription.IsError);
        Assert.True(secondChannel.Reader.Completion.IsCompleted);

        first.Dispose();
    }

    [Fact]
    public void StreamPublisher_WhenSameClientSubscribes_ReplacesPreviousSubscription()
    {
        var publisher = new InMemoryNotificationStreamPublisher(Options.Create(new NotificationsOptions
        {
            Stream = new NotificationsOptions.StreamOptions
            {
                MaxActiveStreamsPerUser = 1,
                ChannelCapacity = 100,
            },
        }));
        var firstChannel = Channel.CreateBounded<NotificationStreamEvent>(1);
        var secondChannel = Channel.CreateBounded<NotificationStreamEvent>(1);
        var first = new ChannelWriterRegistration(firstChannel.Writer);
        var second = new ChannelWriterRegistration(secondChannel.Writer);

        var firstSubscription = publisher.Subscribe(userId, "tab-1", first);
        var secondSubscription = publisher.Subscribe(userId, "tab-1", second);

        Assert.False(firstSubscription.IsError);
        Assert.False(secondSubscription.IsError);
        Assert.True(firstChannel.Reader.Completion.IsCompleted);
        Assert.False(secondChannel.Reader.Completion.IsCompleted);

        second.Dispose();
    }

    [Fact]
    public async Task StreamPublisher_AfterSameClientReplacement_DisposedOldSubscriptionDoesNotRemoveCurrentSubscription()
    {
        var publisher = new InMemoryNotificationStreamPublisher(Options.Create(new NotificationsOptions()));
        var firstChannel = Channel.CreateBounded<NotificationStreamEvent>(1);
        var secondChannel = Channel.CreateBounded<NotificationStreamEvent>(1);
        var first = new ChannelWriterRegistration(firstChannel.Writer);
        var second = new ChannelWriterRegistration(secondChannel.Writer);

        publisher.Subscribe(userId, "tab-1", first);
        publisher.Subscribe(userId, "tab-1", second);
        first.Dispose();

        await publisher.PublishAsync(
            userId,
            new NotificationStreamEvent("notification.created", "{}"),
            CancellationToken.None);

        Assert.False(firstChannel.Reader.TryRead(out _));
        Assert.True(secondChannel.Reader.TryRead(out var streamEvent));
        Assert.Equal("notification.created", streamEvent.EventName);

        second.Dispose();
    }

    [Fact]
    public void StreamPublisher_WhenSubscriptionIsDisposed_AllowsReplacementSubscription()
    {
        var publisher = new InMemoryNotificationStreamPublisher(Options.Create(new NotificationsOptions
        {
            Stream = new NotificationsOptions.StreamOptions
            {
                MaxActiveStreamsPerUser = 1,
                ChannelCapacity = 100,
            },
        }));
        var first = new ChannelWriterRegistration(Channel.CreateBounded<NotificationStreamEvent>(1).Writer);
        var second = new ChannelWriterRegistration(Channel.CreateBounded<NotificationStreamEvent>(1).Writer);

        var firstSubscription = publisher.Subscribe(userId, "tab-1", first);
        first.Dispose();
        var secondSubscription = publisher.Subscribe(userId, "tab-2", second);

        Assert.False(firstSubscription.IsError);
        Assert.False(secondSubscription.IsError);

        second.Dispose();
    }

    [Fact]
    public async Task CreateNotification_AccountDefault_DoesNotCreateBellNotification()
    {
        var bus = fixture.ApplicationHost.Services.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<ErrorOr<CreateNotificationResponse>>(
            new CreateNotificationCommand(
                userId,
                "account.password.changed",
                NotificationCategory.Account,
                NotificationSeverity.Info,
                "Password changed",
                "Your password was changed.",
                Link: null,
                Channels: null,
                Guid.NewGuid(),
                DateTimeOffset.UtcNow),
            CancellationToken.None);

        var count = await fixture.QueryDbAsync<NotificationsDbContext, int>(
            (db, ct) => db.UserNotifications.CountAsync(n => n.RecipientUserId == userId, ct));

        Assert.False(result.IsError);
        Assert.Null(result.Value.BellNotificationId);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task BellEndpoints_EnforceCurrentUserOwnership()
    {
        var notificationId = await CreateProductNotificationAsync(otherUserId, "ticket.reply.created", Guid.NewGuid());
        var client = fixture.CreateAuthenticatedClient(userId, "owner@example.test", "Owner");

        var read = await client.PatchAsync($"/v1/me/notifications/{notificationId}/read", content: null);
        var archive = await client.DeleteAsync($"/v1/me/notifications/{notificationId}");
        var list = await client.GetFromJsonAsync<ListMyNotificationsResponse>("/v1/me/notifications");

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, archive.StatusCode);
        Assert.Empty(list!.Items);
    }

    [Fact]
    public async Task MarkReadAndArchive_UpdateUnreadCountAndVisibility()
    {
        var notificationId = await CreateProductNotificationAsync(userId, "ticket.reply.created", Guid.NewGuid());
        var client = fixture.CreateAuthenticatedClient(userId, "owner@example.test", "Owner");

        var initialCount = await client.GetFromJsonAsync<GetUnreadNotificationCountResponse>("/v1/me/notifications/unread-count");
        var read = await client.PatchAsync($"/v1/me/notifications/{notificationId}/read", content: null);
        var afterRead = await client.GetFromJsonAsync<GetUnreadNotificationCountResponse>("/v1/me/notifications/unread-count");
        var archive = await client.DeleteAsync($"/v1/me/notifications/{notificationId}");
        var list = await client.GetFromJsonAsync<ListMyNotificationsResponse>("/v1/me/notifications");

        Assert.Equal(1, initialCount!.Count);
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
        Assert.Equal(0, afterRead!.Count);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        Assert.Empty(list!.Items);
    }

    [Fact]
    public async Task MarkRead_WhenAlreadyRead_DoesNotChangeReadTimestamp()
    {
        var notificationId = await CreateProductNotificationAsync(userId, "ticket.reply.created", Guid.NewGuid());
        var client = fixture.CreateAuthenticatedClient(userId, "owner@example.test", "Owner");

        var first = await client.PatchAsync($"/v1/me/notifications/{notificationId}/read", content: null);
        var firstReadAt = await fixture.QueryDbAsync<NotificationsDbContext, DateTimeOffset?>(
            (db, ct) => db.UserNotifications
                .Where(n => n.Id == new UserNotificationId(notificationId!.Value))
                .Select(n => n.ReadAt)
                .SingleAsync(ct));

        await Task.Delay(10);
        var second = await client.PatchAsync($"/v1/me/notifications/{notificationId}/read", content: null);
        var secondReadAt = await fixture.QueryDbAsync<NotificationsDbContext, DateTimeOffset?>(
            (db, ct) => db.UserNotifications
                .Where(n => n.Id == new UserNotificationId(notificationId!.Value))
                .Select(n => n.ReadAt)
                .SingleAsync(ct));

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(firstReadAt, secondReadAt);
    }

    [Fact]
    public async Task ListNotifications_WithTiedTimestamps_PaginatesWithoutSkipping()
    {
        var occurredAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        for (var index = 0; index < 3; index++)
        {
            await CreateProductNotificationAsync(userId, $"ticket.reply.{index}", Guid.NewGuid(), occurredAt);
        }

        var client = fixture.CreateAuthenticatedClient(userId, "owner@example.test", "Owner");
        var first = await client.GetFromJsonAsync<ListMyNotificationsResponse>("/v1/me/notifications?limit=2");
        var before = Uri.EscapeDataString($"{first!.NextBefore:O}");
        var second = await client.GetFromJsonAsync<ListMyNotificationsResponse>(
            $"/v1/me/notifications?limit=2&before={before}&beforeId={first.NextBeforeId}");

        Assert.Equal(2, first.Items.Count);
        Assert.NotNull(first.NextBefore);
        Assert.NotNull(first.NextBeforeId);
        Assert.Single(second!.Items);
        Assert.Empty(first.Items.Select(item => item.Id).Intersect(second.Items.Select(item => item.Id)));
    }

    [Fact]
    public async Task ListNotifications_WithBeforeButNoBeforeId_ReturnsBadRequest()
    {
        var client = fixture.CreateAuthenticatedClient(userId, "owner@example.test", "Owner");
        var before = Uri.EscapeDataString($"{DateTimeOffset.UtcNow:O}");

        var response = await client.GetAsync($"/v1/me/notifications?before={before}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListNotifications_WithBeforeIdButNoBefore_ReturnsBadRequest()
    {
        var client = fixture.CreateAuthenticatedClient(userId, "owner@example.test", "Owner");

        var response = await client.GetAsync($"/v1/me/notifications?beforeId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateNotification_WithInvalidEnumValue_ReturnsValidationFailure()
    {
        var bus = fixture.ApplicationHost.Services.GetRequiredService<IMessageBus>();

        var result = await bus.InvokeAsync<ErrorOr<CreateNotificationResponse>>(
            CreateProductNotificationCommand(userId, "ticket.reply.created", Guid.NewGuid(), DateTimeOffset.UtcNow) with
            {
                Category = (NotificationCategory)999,
            },
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public async Task CreateNotification_WithOversizedTitle_ReturnsValidationFailure()
    {
        var bus = fixture.ApplicationHost.Services.GetRequiredService<IMessageBus>();

        var result = await bus.InvokeAsync<ErrorOr<CreateNotificationResponse>>(
            CreateProductNotificationCommand(userId, "ticket.reply.created", Guid.NewGuid(), DateTimeOffset.UtcNow) with
            {
                Title = new string('x', 241),
            },
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public async Task CreateNotification_WithFutureTimestamp_ReturnsValidationFailure()
    {
        var bus = fixture.ApplicationHost.Services.GetRequiredService<IMessageBus>();

        var result = await bus.InvokeAsync<ErrorOr<CreateNotificationResponse>>(
            CreateProductNotificationCommand(userId, "ticket.reply.created", Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(1)),
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }

    [Fact]
    public async Task Preferences_DisableProductBellNotifications()
    {
        var client = fixture.CreateAuthenticatedClient(userId, "owner@example.test", "Owner");

        var update = await client.PutAsJsonAsync("/v1/me/notification-preferences", new UpdateMyNotificationPreferencesRequest(
        [
            new UpdateMyNotificationPreferenceRequest(NotificationCategory.Product, BellEnabled: false, EmailEnabled: false),
        ]));
        var preferences = await client.GetFromJsonAsync<GetMyNotificationPreferencesResponse>("/v1/me/notification-preferences");

        var created = await CreateProductNotificationAsync(userId, "ticket.reply.created", Guid.NewGuid());
        var count = await fixture.QueryDbAsync<NotificationsDbContext, int>(
            (db, ct) => db.UserNotifications.CountAsync(n => n.RecipientUserId == userId, ct));

        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        var product = Assert.Single(preferences!.Preferences, p => p.Category == NotificationCategory.Product);
        Assert.False(product.BellEnabled);
        Assert.False(product.EmailEnabled);
        Assert.Null(created);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Preferences_CannotChangeLockedAccountDefaults()
    {
        var client = fixture.CreateAuthenticatedClient(userId, "owner@example.test", "Owner");

        var update = await client.PutAsJsonAsync("/v1/me/notification-preferences", new UpdateMyNotificationPreferencesRequest(
        [
            new UpdateMyNotificationPreferenceRequest(NotificationCategory.Account, BellEnabled: true, EmailEnabled: false),
        ]));

        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    [Fact]
    public async Task PruneNotifications_DeletesRowsPastRetention()
    {
        await fixture.SeedDbAsync<NotificationsDbContext>(async (db, _) =>
        {
            db.UserNotifications.Add(UserNotification.Create(
                userId,
                "ticket.reply.created",
                BellNotificationCategory.Product,
                BellNotificationSeverity.Info,
                "Old reply",
                "An old reply.",
                linkHref: null,
                linkLabel: null,
                DateTimeOffset.UtcNow.AddDays(-120),
                expiresAt: null,
                DateTimeOffset.UtcNow.AddDays(-1),
                Guid.NewGuid()));

            await Task.CompletedTask;
        });

        var bus = fixture.ApplicationHost.Services.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new PruneNotifications(), CancellationToken.None);

        var remaining = await fixture.QueryDbAsync<NotificationsDbContext, int>(
            (db, ct) => db.UserNotifications.CountAsync(n => n.RecipientUserId == userId, ct));

        Assert.Equal(0, remaining);
    }

    private async Task<Guid?> CreateProductNotificationAsync(
        Guid recipientUserId,
        string type,
        Guid idempotencyKey,
        DateTimeOffset? occurredAt = null)
    {
        var bus = fixture.ApplicationHost.Services.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<ErrorOr<CreateNotificationResponse>>(
            CreateProductNotificationCommand(recipientUserId, type, idempotencyKey, occurredAt ?? DateTimeOffset.UtcNow),
            CancellationToken.None);

        Assert.False(result.IsError);
        return result.Value.BellNotificationId;
    }

    private static CreateNotificationCommand CreateProductNotificationCommand(
        Guid recipientUserId,
        string type,
        Guid idempotencyKey,
        DateTimeOffset occurredAt) =>
        new(
            recipientUserId,
            type,
            NotificationCategory.Product,
            NotificationSeverity.Info,
            "Alice replied to your ticket",
            "There is a new reply on Billing issue.",
            new NotificationLinkDto("/tickets/123", "Open ticket"),
            Channels: null,
            idempotencyKey,
            occurredAt);
}
