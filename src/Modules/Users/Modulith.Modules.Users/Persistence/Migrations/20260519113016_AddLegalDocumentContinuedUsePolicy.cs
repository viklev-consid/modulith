using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modulith.Modules.Users.Persistence.Migrations;

/// <inheritdoc />
public partial class AddLegalDocumentContinuedUsePolicy : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "blocking_level",
            schema: "users",
            table: "legal_documents",
            type: "character varying(50)",
            maxLength: 50,
            nullable: false,
            defaultValue: "None");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "continued_use_required_at",
            schema: "users",
            table: "legal_documents",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "is_required_for_continued_use",
            schema: "users",
            table: "legal_documents",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "ix_legal_documents_is_required_for_continued_use_continued_use",
            schema: "users",
            table: "legal_documents",
            columns: new[] { "is_required_for_continued_use", "continued_use_required_at", "superseded_at" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_legal_documents_is_required_for_continued_use_continued_use",
            schema: "users",
            table: "legal_documents");

        migrationBuilder.DropColumn(
            name: "blocking_level",
            schema: "users",
            table: "legal_documents");

        migrationBuilder.DropColumn(
            name: "continued_use_required_at",
            schema: "users",
            table: "legal_documents");

        migrationBuilder.DropColumn(
            name: "is_required_for_continued_use",
            schema: "users",
            table: "legal_documents");
    }
}
