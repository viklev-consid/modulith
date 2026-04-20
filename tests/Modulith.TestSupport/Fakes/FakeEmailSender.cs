using Modulith.Shared.Infrastructure.Notifications;

namespace Modulith.TestSupport.Fakes;

public sealed class FakeEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _sent = [];

    public IReadOnlyList<EmailMessage> SentMessages => _sent.AsReadOnly();

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        _sent.Add(message);
        return Task.CompletedTask;
    }
}
