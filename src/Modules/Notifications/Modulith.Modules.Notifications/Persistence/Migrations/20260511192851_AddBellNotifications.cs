using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modulith.Modules.Notifications.Persistence.Migrations;

/// <inheritdoc />
public partial class AddBellNotifications : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "notification_preferences",
            schema: "notifications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                category = table.Column<int>(type: "integer", nullable: false),
                bell_enabled = table.Column<bool>(type: "boolean", nullable: false),
                email_enabled = table.Column<bool>(type: "boolean", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_notification_preferences", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "user_notifications",
            schema: "notifications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                category = table.Column<int>(type: "integer", nullable: false),
                severity = table.Column<int>(type: "integer", nullable: false),
                title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                link_href = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                link_label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                retention_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                idempotency_key = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_user_notifications", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_notification_preferences_user_id_category",
            schema: "notifications",
            table: "notification_preferences",
            columns: new[] { "user_id", "category" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_user_notifications_expires_at",
            schema: "notifications",
            table: "user_notifications",
            column: "expires_at");

        migrationBuilder.CreateIndex(
            name: "ix_user_notifications_idempotency_key",
            schema: "notifications",
            table: "user_notifications",
            column: "idempotency_key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_user_notifications_recipient_user_id_archived_at_created_at",
            schema: "notifications",
            table: "user_notifications",
            columns: new[] { "recipient_user_id", "archived_at", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ix_user_notifications_recipient_user_id_created_at",
            schema: "notifications",
            table: "user_notifications",
            columns: new[] { "recipient_user_id", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ix_user_notifications_recipient_user_id_read_at_created_at",
            schema: "notifications",
            table: "user_notifications",
            columns: new[] { "recipient_user_id", "read_at", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ix_user_notifications_retention_until",
            schema: "notifications",
            table: "user_notifications",
            column: "retention_until");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "notification_preferences",
            schema: "notifications");

        migrationBuilder.DropTable(
            name: "user_notifications",
            schema: "notifications");
    }
}
