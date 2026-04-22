using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.Shared.Infrastructure.Seeding;

namespace Modulith.Modules.Users.Seeding;

internal sealed class UsersDevSeeder(
    UsersDbContext db,
    IPasswordHasher passwordHasher,
    IOptions<UsersDevOptions> devOpts) : IModuleSeeder
{
    private static readonly (string Email, string Password, string DisplayName)[] SeedUsers =
    [
        ("alice@example.com",   "Password1!", "Alice Example"),
        ("bob@example.com",     "Password1!", "Bob Example"),
        ("charlie@example.com", "Password1!", "Charlie Example"),
        ("diana@example.com",   "Password1!", "Diana Example"),
        ("eve@example.com",     "Password1!", "Eve Example"),
        ("frank@example.com",   "Password1!", "Frank Example"),
        ("grace@example.com",   "Password1!", "Grace Example"),
        ("henry@example.com",   "Password1!", "Henry Example"),
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Seed regular users.
        foreach (var (email, password, displayName) in SeedUsers)
        {
            await EnsureUserAsync(email, password, displayName, Role.User, cancellationToken);
        }

        // Ensure the dev admin account exists.
        var opts = devOpts.Value;
        await EnsureUserAsync(opts.AdminEmail, "Admin1!Admin1!", opts.AdminDisplayName, Role.Admin, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureUserAsync(
        string email,
        string password,
        string displayName,
        Role role,
        CancellationToken ct)
    {
        var emailResult = Email.Create(email);
        if (emailResult.IsError)
        {
            return;
        }

        var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Email == emailResult.Value, ct);
        if (existingUser is not null)
        {
            // If the user exists but has the wrong role (e.g. seeder config changed),
            // promote idempotently.
            if (existingUser.Role != role)
            {
                existingUser.ChangeRole(role, existingUser.Id);
            }
            return;
        }

        var passwordHash = new PasswordHash(passwordHasher.Hash(password));
        var userResult = User.Create(emailResult.Value, passwordHash, displayName, role);
        if (userResult.IsError)
        {
            return;
        }

        db.Users.Add(userResult.Value);
    }
}
