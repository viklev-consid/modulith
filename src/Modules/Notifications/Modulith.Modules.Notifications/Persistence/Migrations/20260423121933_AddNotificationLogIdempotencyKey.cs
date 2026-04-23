using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modulith.Modules.Notifications.Persistence.Migrations;

/// <inheritdoc />
public partial class AddNotificationLogIdempotencyKey : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_notification_logs_user_id_notification_type",
            schema: "notifications",
            table: "notification_logs");

        migrationBuilder.AddColumn<Guid>(
            name: "idempotency_key",
            schema: "notifications",
            table: "notification_logs",
            type: "uuid",
            nullable: false,
            defaultValue: Guid.Empty);

        migrationBuilder.CreateIndex(
            name: "ix_notification_logs_idempotency_key",
            schema: "notifications",
            table: "notification_logs",
            column: "idempotency_key",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_notification_logs_idempotency_key",
            schema: "notifications",
            table: "notification_logs");

        migrationBuilder.DropColumn(
            name: "idempotency_key",
            schema: "notifications",
            table: "notification_logs");

        migrationBuilder.CreateIndex(
            name: "ix_notification_logs_user_id_notification_type",
            schema: "notifications",
            table: "notification_logs",
            columns: new[] { "user_id", "notification_type" });
    }
}
