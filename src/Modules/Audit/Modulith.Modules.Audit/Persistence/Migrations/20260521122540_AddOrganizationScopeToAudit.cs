using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modulith.Modules.Audit.Persistence.Migrations;

/// <inheritdoc />
public partial class AddOrganizationScopeToAudit : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "access_mode",
            schema: "audit",
            table: "audit_entries",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "organization_id",
            schema: "audit",
            table: "audit_entries",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_audit_entries_organization_id",
            schema: "audit",
            table: "audit_entries",
            column: "organization_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_audit_entries_organization_id",
            schema: "audit",
            table: "audit_entries");

        migrationBuilder.DropColumn(
            name: "access_mode",
            schema: "audit",
            table: "audit_entries");

        migrationBuilder.DropColumn(
            name: "organization_id",
            schema: "audit",
            table: "audit_entries");
    }
}
