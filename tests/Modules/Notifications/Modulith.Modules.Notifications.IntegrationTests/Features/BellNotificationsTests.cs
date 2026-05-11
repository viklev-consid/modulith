using System.Net;
using System.Net.Http.Json;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulith.Modules.Notifications.Contracts.Commands;
using Modulith.Modules.Notifications.Contracts.Dtos;
using Modulith.Modules.Notifications.Domain;
using Modulith.Modules.Notifications.Features.GetMyNotificationPreferences;
using Modulith.Modules.Notifications.Features.GetUnreadNotificationCount;
using Modulith.Modules.Notifications.Features.ListMyNotifications;
using Modulith.Modules.Notifications.Features.UpdateMyNotificationPreferences;
using Modulith.Modules.Notifications.Jobs;
using Modulith.Modules.Notifications.Persistence;
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

    private async Task<Guid?> CreateProductNotificationAsync(Guid recipientUserId, string type, Guid idempotencyKey)
    {
        var bus = fixture.ApplicationHost.Services.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<ErrorOr<CreateNotificationResponse>>(
            new CreateNotificationCommand(
                recipientUserId,
                type,
                NotificationCategory.Product,
                NotificationSeverity.Info,
                "Alice replied to your ticket",
                "There is a new reply on Billing issue.",
                new NotificationLinkDto("/tickets/123", "Open ticket"),
                Channels: null,
                idempotencyKey,
                DateTimeOffset.UtcNow),
            CancellationToken.None);

        Assert.False(result.IsError);
        return result.Value.BellNotificationId;
    }
}
