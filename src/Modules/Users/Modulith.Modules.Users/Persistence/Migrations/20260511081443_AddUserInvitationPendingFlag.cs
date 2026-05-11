using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modulith.Modules.Users.Persistence.Migrations;
    /// <inheritdoc />
    public partial class AddUserInvitationPendingFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_invitations_email",
                schema: "users",
                table: "user_invitations");

            migrationBuilder.AddColumn<bool>(
                name: "is_pending",
                schema: "users",
                table: "user_invitations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_user_invitations_email",
                schema: "users",
                table: "user_invitations",
                column: "email",
                unique: true,
                filter: "is_pending = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_invitations_email",
                schema: "users",
                table: "user_invitations");

            migrationBuilder.DropColumn(
                name: "is_pending",
                schema: "users",
                table: "user_invitations");

            migrationBuilder.CreateIndex(
                name: "ix_user_invitations_email",
                schema: "users",
                table: "user_invitations",
                column: "email",
                unique: true,
                filter: "accepted_at IS NULL AND revoked_at IS NULL");
        }
    }
