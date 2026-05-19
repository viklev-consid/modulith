using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modulith.Modules.Users.Persistence.Migrations;

/// <inheritdoc />
public partial class RemoveExternalLogin : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "external_logins",
            schema: "users");

        migrationBuilder.DropTable(
            name: "pending_external_logins",
            schema: "users");

        migrationBuilder.AlterColumn<string>(
            name: "password_hash",
            schema: "users",
            table: "users",
            type: "text",
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "password_hash",
            schema: "users",
            table: "users",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text");

        migrationBuilder.CreateTable(
                name: "external_logins",
                schema: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provider_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_logins", x => x.id);
                    table.ForeignKey(
                        name: "fk_external_logins_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "users",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

        migrationBuilder.CreateTable(
            name: "pending_external_logins",
            schema: "users",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_from_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                is_existing_user = table.Column<bool>(type: "boolean", nullable: false),
                issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                provider_avatar_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_pending_external_logins", x => x.id);
            });

        migrationBuilder.CreateIndex(
                name: "ix_external_logins_provider_subject",
                schema: "users",
                table: "external_logins",
                columns: new[] { "provider", "subject" },
                unique: true);

        migrationBuilder.CreateIndex(
                name: "ix_external_logins_user_id",
                schema: "users",
                table: "external_logins",
                column: "user_id");

        migrationBuilder.CreateIndex(
                name: "ix_external_logins_user_id_provider",
                schema: "users",
                table: "external_logins",
                columns: new[] { "user_id", "provider" },
                unique: true);

        migrationBuilder.CreateIndex(
                name: "ix_pending_external_logins_expires_at",
                schema: "users",
                table: "pending_external_logins",
                column: "expires_at");

        migrationBuilder.CreateIndex(
                name: "ix_pending_external_logins_provider_subject",
                schema: "users",
                table: "pending_external_logins",
                columns: new[] { "provider", "subject" },
                unique: true,
                filter: "consumed_at IS NULL");

        migrationBuilder.CreateIndex(
                name: "ix_pending_external_logins_token_hash",
                schema: "users",
                table: "pending_external_logins",
                column: "token_hash",
                unique: true);
    }
}
