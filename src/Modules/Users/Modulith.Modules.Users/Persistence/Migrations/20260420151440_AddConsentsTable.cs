using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modulith.Modules.Users.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConsentsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consents",
                schema: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consent_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    granted = table.Column<bool>(type: "boolean", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consents", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_consents_user_id_consent_key",
                schema: "users",
                table: "consents",
                columns: new[] { "user_id", "consent_key" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consents",
                schema: "users");
        }
    }
}
