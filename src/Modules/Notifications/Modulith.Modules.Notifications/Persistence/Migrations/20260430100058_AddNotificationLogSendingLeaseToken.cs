using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modulith.Modules.Notifications.Persistence.Migrations;

/// <inheritdoc />
public partial class AddNotificationLogSendingLeaseToken : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "sending_lease_token",
            schema: "notifications",
            table: "notification_logs",
            type: "uuid",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "sending_lease_token",
            schema: "notifications",
            table: "notification_logs");
    }
}
