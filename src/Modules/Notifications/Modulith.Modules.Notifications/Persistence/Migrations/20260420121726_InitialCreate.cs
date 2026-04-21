using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modulith.Modules.Notifications.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "notifications");

        migrationBuilder.CreateTable(
            name: "notification_logs",
            schema: "notifications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                recipient_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                notification_type = table.Column<int>(type: "integer", nullable: false),
                subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_notification_logs", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_notification_logs_user_id",
            schema: "notifications",
            table: "notification_logs",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_notification_logs_user_id_notification_type",
            schema: "notifications",
            table: "notification_logs",
            columns: new[] { "user_id", "notification_type" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "notification_logs",
            schema: "notifications");
    }
}
