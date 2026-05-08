using System.Security.Claims;

namespace Modulith.TestSupport;

public sealed class AuthenticatedClientBuilder(ApiTestFixture fixture)
{
    private Guid userId = Guid.NewGuid();
    private string email = "test-user@example.com";
    private string displayName = "Test User";
    private string role = "user";
    private readonly List<Claim> claims = [];

    public AuthenticatedClientBuilder WithUser(Guid id, string userEmail, string name)
    {
        userId = id;
        email = userEmail;
        displayName = name;
        return this;
    }

    public AuthenticatedClientBuilder WithEmail(string userEmail)
    {
        email = userEmail;
        return this;
    }

    public AuthenticatedClientBuilder WithDisplayName(string name)
    {
        displayName = name;
        return this;
    }

    public AuthenticatedClientBuilder WithRole(string userRole)
    {
        role = userRole;
        return this;
    }

    public AuthenticatedClientBuilder WithClaim(string type, string value)
    {
        claims.Add(new Claim(type, value));
        return this;
    }

    public HttpClient Build()
    {
        return fixture.CreateAuthenticatedClientWithToken(BuildToken());
    }

    public string BuildToken() => ApiTestFixture.GenerateTestToken(userId, email, displayName, role, claims);
}
