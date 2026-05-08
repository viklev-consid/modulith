using ErrorOr;
using Modulith.Modules.Users.Security;

namespace Modulith.Modules.Users.IntegrationTests.Fakes;

public sealed class FakeGoogleIdTokenVerifier : IGoogleIdTokenVerifier
{
    private ErrorOr<GoogleIdentity> result =
        new GoogleIdentity("google-sub-default", "fake@example.com", "Fake User");

    public void SetIdentity(string subject, string email, string name = "Test User") =>
        result = new GoogleIdentity(subject, email, name);

    public void SetError(Error error) =>
        result = error;

    public Task<ErrorOr<GoogleIdentity>> VerifyAsync(string idToken, CancellationToken ct = default) =>
        Task.FromResult(result);
}
