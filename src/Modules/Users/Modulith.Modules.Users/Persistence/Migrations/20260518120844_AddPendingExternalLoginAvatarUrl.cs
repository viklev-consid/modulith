using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modulith.Modules.Users.Persistence.Migrations;

/// <inheritdoc />
public partial class AddPendingExternalLoginAvatarUrl : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "provider_avatar_url",
            schema: "users",
            table: "pending_external_logins",
            type: "character varying(2048)",
            maxLength: 2048,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "provider_avatar_url",
            schema: "users",
            table: "pending_external_logins");
    }
}
