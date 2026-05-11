using System.Collections.Concurrent;
using ErrorOr;
using Microsoft.Extensions.Options;
using Modulith.Modules.Notifications.Errors;

namespace Modulith.Modules.Notifications.Streaming;

public sealed class InMemoryNotificationStreamPublisher(IOptions<NotificationsOptions> options) : INotificationStreamPublisher
{
    private readonly ConcurrentDictionary<Guid, UserStreamRegistrations> registrations = new();
    private readonly int maxActiveStreamsPerUser = options.Value.Stream.MaxActiveStreamsPerUser;

    public ErrorOr<Success> Subscribe(Guid userId, ChannelWriterRegistration registration)
    {
        var registrationId = Guid.NewGuid();
        var userRegistrations = registrations.GetOrAdd(userId, _ => new UserStreamRegistrations());

        lock (userRegistrations.SyncRoot)
        {
            if (userRegistrations.Count >= maxActiveStreamsPerUser)
            {
                registration.Dispose();
                return NotificationsErrors.TooManyNotificationStreams;
            }

            userRegistrations.Add(registrationId, registration);
        }

        registration.SetDispose(() =>
        {
            if (!registrations.TryGetValue(userId, out var current))
            {
                return;
            }

            lock (current.SyncRoot)
            {
                current.Remove(registrationId);

                if (current.Count == 0)
                {
                    registrations.TryRemove(userId, out _);
                }
            }
        });

        return Result.Success;
    }

    public ValueTask PublishAsync(Guid userId, NotificationStreamEvent streamEvent, CancellationToken ct)
    {
        if (!registrations.TryGetValue(userId, out var userRegistrations))
        {
            return ValueTask.CompletedTask;
        }

        IReadOnlyList<KeyValuePair<Guid, ChannelWriterRegistration>> currentRegistrations;
        lock (userRegistrations.SyncRoot)
        {
            currentRegistrations = userRegistrations.ToList();
        }

        foreach (var (registrationId, registration) in currentRegistrations)
        {
            if (!registration.Writer.TryWrite(streamEvent))
            {
                lock (userRegistrations.SyncRoot)
                {
                    userRegistrations.Remove(registrationId);
                }
            }
        }

        lock (userRegistrations.SyncRoot)
        {
            if (userRegistrations.Count == 0)
            {
                registrations.TryRemove(userId, out _);
            }
        }

        return ValueTask.CompletedTask;
    }

    private sealed class UserStreamRegistrations
    {
        private readonly Dictionary<Guid, ChannelWriterRegistration> registrations = [];

        public object SyncRoot { get; } = new();

        public int Count => registrations.Count;

        public void Add(Guid id, ChannelWriterRegistration registration) => registrations[id] = registration;

        public void Remove(Guid id) => registrations.Remove(id);

        public List<KeyValuePair<Guid, ChannelWriterRegistration>> ToList() => registrations.ToList();
    }
}
