using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modulith.Modules.Users.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "email_confirmed_at",
                schema: "users",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "email_confirmed_at",
                schema: "users",
                table: "users");
        }
    }
}
