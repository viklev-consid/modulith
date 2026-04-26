using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modulith.Modules.Users.Persistence.Migrations;

/// <inheritdoc />
public partial class MakePendingExternalLoginProviderSubjectUnique : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_pending_external_logins_provider_subject",
            schema: "users",
            table: "pending_external_logins");

        migrationBuilder.CreateIndex(
            name: "ix_pending_external_logins_provider_subject",
            schema: "users",
            table: "pending_external_logins",
            columns: new[] { "provider", "subject" },
            unique: true,
            filter: "consumed_at IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_pending_external_logins_provider_subject",
            schema: "users",
            table: "pending_external_logins");

        migrationBuilder.CreateIndex(
            name: "ix_pending_external_logins_provider_subject",
            schema: "users",
            table: "pending_external_logins",
            columns: new[] { "provider", "subject" },
            filter: "consumed_at IS NULL");
    }
}
