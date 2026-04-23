using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modulith.Modules.Users.Persistence.Migrations;

/// <inheritdoc />
public partial class AddUserForeignKeyCascades : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "ix_user_tokens_user_id",
            schema: "users",
            table: "user_tokens",
            column: "user_id");

        migrationBuilder.AddForeignKey(
            name: "fk_pending_email_changes_users_user_id",
            schema: "users",
            table: "pending_email_changes",
            column: "user_id",
            principalSchema: "users",
            principalTable: "users",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_refresh_tokens_users_user_id",
            schema: "users",
            table: "refresh_tokens",
            column: "user_id",
            principalSchema: "users",
            principalTable: "users",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_user_tokens_users_user_id",
            schema: "users",
            table: "user_tokens",
            column: "user_id",
            principalSchema: "users",
            principalTable: "users",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_pending_email_changes_users_user_id",
            schema: "users",
            table: "pending_email_changes");

        migrationBuilder.DropForeignKey(
            name: "fk_refresh_tokens_users_user_id",
            schema: "users",
            table: "refresh_tokens");

        migrationBuilder.DropForeignKey(
            name: "fk_user_tokens_users_user_id",
            schema: "users",
            table: "user_tokens");

        migrationBuilder.DropIndex(
            name: "ix_user_tokens_user_id",
            schema: "users",
            table: "user_tokens");
    }
}
