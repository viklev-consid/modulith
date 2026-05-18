using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modulith.Modules.Users.Persistence.Migrations;

/// <inheritdoc />
public partial class AddExternalLoginProviderEmail : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "provider_email",
            schema: "users",
            table: "external_logins",
            type: "character varying(254)",
            maxLength: 254,
            nullable: true);

        migrationBuilder.Sql("""
            -- Best-effort backfill: historical provider email was not stored before this migration.
            -- Use the current user email only so existing linked rows satisfy the new required column.
            UPDATE users.external_logins AS external_login
            SET provider_email = users.email
            FROM users.users AS users
            WHERE users.id = external_login.user_id;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "provider_email",
            schema: "users",
            table: "external_logins",
            type: "character varying(254)",
            maxLength: 254,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(254)",
            oldMaxLength: 254,
            oldNullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "provider_email",
            schema: "users",
            table: "external_logins");
    }
}
