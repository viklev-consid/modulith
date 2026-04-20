using Modulith.Modules.Users.Domain;

namespace Modulith.Modules.Users.Features.DeleteAccount;

public sealed record DeleteAccountCommand(UserId UserId);
