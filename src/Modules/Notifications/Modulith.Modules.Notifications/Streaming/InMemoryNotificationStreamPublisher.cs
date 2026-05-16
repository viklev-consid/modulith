using System.Collections.Concurrent;
using ErrorOr;
using Microsoft.Extensions.Options;
using Modulith.Modules.Notifications.Errors;

namespace Modulith.Modules.Notifications.Streaming;

public sealed class InMemoryNotificationStreamPublisher(IOptions<NotificationsOptions> options) : INotificationStreamPublisher
{
    private readonly ConcurrentDictionary<Guid, UserStreamRegistrations> registrations = new();
    private readonly int maxActiveStreamsPerUser = options.Value.Stream.MaxActiveStreamsPerUser;

    public ErrorOr<Success> Subscribe(Guid userId, string clientId, ChannelWriterRegistration registration)
    {
        var registrationId = Guid.NewGuid();
        ChannelWriterRegistration? replacedRegistration = null;

        while (true)
        {
            var userRegistrations = registrations.GetOrAdd(userId, _ => new UserStreamRegistrations());

            lock (userRegistrations.SyncRoot)
            {
                if (userRegistrations.IsRemoved)
                {
                    continue;
                }

                if (!userRegistrations.ContainsClient(clientId) && userRegistrations.Count >= maxActiveStreamsPerUser)
                {
                    registration.Dispose();
                    return NotificationsErrors.TooManyNotificationStreams;
                }

                replacedRegistration = userRegistrations.AddOrReplace(registrationId, clientId, registration);
            }

            break;
        }

        registration.SetDispose(() =>
        {
            if (!registrations.TryGetValue(userId, out var current))
            {
                return;
            }

            lock (current.SyncRoot)
            {
                current.Remove(clientId, registrationId);
            }

            RemoveIfEmpty(userId, current);
        });

        replacedRegistration?.Dispose();

        return Result.Success;
    }

    public ValueTask PublishAsync(Guid userId, NotificationStreamEvent streamEvent, CancellationToken ct)
    {
        if (!registrations.TryGetValue(userId, out var userRegistrations))
        {
            return ValueTask.CompletedTask;
        }

        IReadOnlyList<UserStreamRegistration> currentRegistrations;
        lock (userRegistrations.SyncRoot)
        {
            currentRegistrations = userRegistrations.ToList();
        }

        foreach (var registration in currentRegistrations
                     .Where(registration => !registration.WriterRegistration.Writer.TryWrite(streamEvent)))
        {
            lock (userRegistrations.SyncRoot)
            {
                userRegistrations.Remove(registration.ClientId, registration.Id);
            }
        }

        RemoveIfEmpty(userId, userRegistrations);

        return ValueTask.CompletedTask;
    }

    private void RemoveIfEmpty(Guid userId, UserStreamRegistrations userRegistrations)
    {
        lock (userRegistrations.SyncRoot)
        {
            if (userRegistrations.Count != 0)
            {
                return;
            }

            userRegistrations.MarkRemoved();
        }

        registrations.TryRemove(new KeyValuePair<Guid, UserStreamRegistrations>(userId, userRegistrations));
    }

    private sealed class UserStreamRegistrations
    {
        private readonly Dictionary<string, UserStreamRegistration> registrations = new(StringComparer.Ordinal);

        public object SyncRoot { get; } = new();

        public bool IsRemoved { get; private set; }

        public int Count => registrations.Count;

        public bool ContainsClient(string clientId) => registrations.ContainsKey(clientId);

        public void MarkRemoved() => IsRemoved = true;

        public ChannelWriterRegistration? AddOrReplace(Guid id, string clientId, ChannelWriterRegistration registration)
        {
            var replaced = registrations.TryGetValue(clientId, out var current)
                ? current.WriterRegistration
                : null;

            registrations[clientId] = new UserStreamRegistration(id, clientId, registration);
            return replaced;
        }

        public void Remove(string clientId, Guid id)
        {
            if (registrations.TryGetValue(clientId, out var registration) && registration.Id == id)
            {
                registrations.Remove(clientId);
            }
        }

        public List<UserStreamRegistration> ToList() => registrations.Values.ToList();
    }

    private sealed record UserStreamRegistration(
        Guid Id,
        string ClientId,
        ChannelWriterRegistration WriterRegistration);
}
