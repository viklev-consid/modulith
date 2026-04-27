using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modulith.Modules.Users.Persistence.Migrations;

/// <inheritdoc />
public partial class EnforceOneExternalLoginPerProvider : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "ix_external_logins_user_id_provider",
            schema: "users",
            table: "external_logins",
            columns: new[] { "user_id", "provider" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_external_logins_user_id_provider",
            schema: "users",
            table: "external_logins");
    }
}
