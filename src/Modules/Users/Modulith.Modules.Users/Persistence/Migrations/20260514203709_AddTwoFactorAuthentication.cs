using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modulith.Modules.Users.Persistence.Migrations;

/// <inheritdoc />
public partial class AddTwoFactorAuthentication : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
                name: "pending_two_factor_challenges",
                schema: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pending_two_factor_challenges", x => x.id);
                });

        migrationBuilder.CreateTable(
            name: "recovery_codes",
            schema: "users",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                code_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_recovery_codes", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "two_factor_credentials",
            schema: "users",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                protected_secret = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                disabled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_accepted_time_step = table.Column<long>(type: "bigint", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_two_factor_credentials", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_pending_two_factor_challenges_token_hash",
            schema: "users",
            table: "pending_two_factor_challenges",
            column: "token_hash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_pending_two_factor_challenges_user_id",
            schema: "users",
            table: "pending_two_factor_challenges",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_recovery_codes_user_id_code_hash",
            schema: "users",
            table: "recovery_codes",
            columns: new[] { "user_id", "code_hash" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_two_factor_credentials_user_id_method",
            schema: "users",
            table: "two_factor_credentials",
            columns: new[] { "user_id", "method" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
                name: "pending_two_factor_challenges",
                schema: "users");

        migrationBuilder.DropTable(
                name: "recovery_codes",
                schema: "users");

        migrationBuilder.DropTable(
                name: "two_factor_credentials",
                schema: "users");
    }
}
