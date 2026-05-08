using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modulith.Modules.Audit.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAuditEntryIdempotencyKey : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "idempotency_key",
            schema: "audit",
            table: "audit_entries",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_audit_entries_idempotency_key",
            schema: "audit",
            table: "audit_entries",
            column: "idempotency_key",
            unique: true,
            filter: "idempotency_key IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_audit_entries_idempotency_key",
            schema: "audit",
            table: "audit_entries");

        migrationBuilder.DropColumn(
            name: "idempotency_key",
            schema: "audit",
            table: "audit_entries");
    }
}
