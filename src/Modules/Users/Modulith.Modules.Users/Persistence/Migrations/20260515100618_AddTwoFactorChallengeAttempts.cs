using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modulith.Modules.Users.Persistence.Migrations;

/// <inheritdoc />
public partial class AddTwoFactorChallengeAttempts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
                name: "attempt_count",
                schema: "users",
                table: "pending_two_factor_challenges",
                type: "integer",
                nullable: false,
                defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
                name: "attempt_count",
                schema: "users",
                table: "pending_two_factor_challenges");
    }
}
