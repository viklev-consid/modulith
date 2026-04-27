using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modulith.Modules.Users.Persistence.Migrations;

/// <inheritdoc />
public partial class AddThirdPartyAuth : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "password_hash",
            schema: "users",
            table: "users",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text");

        migrationBuilder.AddColumn<bool>(
            name: "has_completed_onboarding",
            schema: "users",
            table: "users",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<string>(
            name: "granted_from_ip",
            schema: "users",
            table: "consents",
            type: "character varying(45)",
            maxLength: 45,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "granted_user_agent",
            schema: "users",
            table: "consents",
            type: "character varying(512)",
            maxLength: 512,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "external_logins",
            schema: "users",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                is_existing_user = table.Column<bool>(type: "boolean", nullable: false),
                token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                created_from_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_pending_external_logins", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "terms_acceptances",
            schema: "users",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                accepted_from_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_terms_acceptances", x => x.id);
                table.ForeignKey(
                    name: "fk_terms_acceptances_users_user_id",
                    column: x => x.user_id,
                    principalSchema: "users",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
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
            name: "ix_pending_external_logins_expires_at",
            schema: "users",
            table: "pending_external_logins",
            column: "expires_at");

        migrationBuilder.CreateIndex(
            name: "ix_pending_external_logins_provider_subject",
            schema: "users",
            table: "pending_external_logins",
            columns: new[] { "provider", "subject" },
            filter: "consumed_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "ix_pending_external_logins_token_hash",
            schema: "users",
            table: "pending_external_logins",
            column: "token_hash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_terms_acceptances_user_id_version",
            schema: "users",
            table: "terms_acceptances",
            columns: new[] { "user_id", "version" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "external_logins",
            schema: "users");

        migrationBuilder.DropTable(
            name: "pending_external_logins",
            schema: "users");

        migrationBuilder.DropTable(
            name: "terms_acceptances",
            schema: "users");

        migrationBuilder.DropColumn(
            name: "has_completed_onboarding",
            schema: "users",
            table: "users");

        migrationBuilder.DropColumn(
            name: "granted_from_ip",
            schema: "users",
            table: "consents");

        migrationBuilder.DropColumn(
            name: "granted_user_agent",
            schema: "users",
            table: "consents");

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
}
