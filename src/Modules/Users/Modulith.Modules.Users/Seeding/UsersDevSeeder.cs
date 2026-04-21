using Microsoft.EntityFrameworkCore;
using Modulith.Modules.Users.Domain;
using Modulith.Modules.Users.Persistence;
using Modulith.Modules.Users.Security;
using Modulith.Shared.Infrastructure.Seeding;

namespace Modulith.Modules.Users.Seeding;

internal sealed class UsersDevSeeder(UsersDbContext db, IPasswordHasher passwordHasher) : IModuleSeeder
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
        foreach (var (email, password, displayName) in SeedUsers)
        {
            var emailResult = Email.Create(email);
            if (emailResult.IsError)
            {
                continue;
            }

            if (await db.Users.AnyAsync(u => u.Email == emailResult.Value, cancellationToken))
            {
                continue;
            }

            var passwordHash = new PasswordHash(passwordHasher.Hash(password));
            var userResult = User.Create(emailResult.Value, passwordHash, displayName);
            if (userResult.IsError)
            {
                continue;
            }

            db.Users.Add(userResult.Value);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
