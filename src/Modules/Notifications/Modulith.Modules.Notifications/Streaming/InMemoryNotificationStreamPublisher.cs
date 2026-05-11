using System.Collections.Concurrent;

namespace Modulith.Modules.Notifications.Streaming;

public sealed class InMemoryNotificationStreamPublisher : INotificationStreamPublisher
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, ChannelWriterRegistration>> registrations = new();

    public void Subscribe(Guid userId, ChannelWriterRegistration registration)
    {
        var registrationId = Guid.NewGuid();
        var userRegistrations = registrations.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, ChannelWriterRegistration>());
        userRegistrations[registrationId] = registration;

        registration.SetDispose(() =>
        {
            if (registrations.TryGetValue(userId, out var current))
            {
                current.TryRemove(registrationId, out _);
            }
        });
    }

    public ValueTask PublishAsync(Guid userId, NotificationStreamEvent streamEvent, CancellationToken ct)
    {
        if (!registrations.TryGetValue(userId, out var userRegistrations))
        {
            return ValueTask.CompletedTask;
        }

        foreach (var (registrationId, registration) in userRegistrations)
        {
            if (!registration.Writer.TryWrite(streamEvent))
            {
                userRegistrations.TryRemove(registrationId, out _);
            }
        }

        if (userRegistrations.IsEmpty)
        {
            registrations.TryRemove(userId, out _);
        }

        return ValueTask.CompletedTask;
    }
}
