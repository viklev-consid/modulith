using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modulith.Modules.Notifications.Persistence.Migrations;

/// <inheritdoc />
public partial class RenameNotificationLogSentAtToCreatedAt : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Rename the original sent_at column (which was always set at row-creation time)
        // to created_at, preserving all existing data.
        migrationBuilder.RenameColumn(
            name: "sent_at",
            schema: "notifications",
            table: "notification_logs",
            newName: "created_at");

        // Add the new nullable sent_at column that will be set only upon confirmed delivery.
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "sent_at",
            schema: "notifications",
            table: "notification_logs",
            type: "timestamp with time zone",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "sent_at",
            schema: "notifications",
            table: "notification_logs");

        migrationBuilder.RenameColumn(
            name: "created_at",
            schema: "notifications",
            table: "notification_logs",
            newName: "sent_at");
    }
}
