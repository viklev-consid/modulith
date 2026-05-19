using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modulith.Modules.Users.Persistence.Migrations;

/// <inheritdoc />
public partial class AddLegalDocuments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "content_hash",
            schema: "users",
            table: "terms_acceptances",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "document_type",
            schema: "users",
            table: "terms_acceptances",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "legal_document_id",
            schema: "users",
            table: "terms_acceptances",
            type: "uuid",
            nullable: true);

            migrationBuilder.CreateTable(
                name: "legal_documents",
                schema: "users",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                document_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                markdown_content = table.Column<string>(type: "text", nullable: false),
                content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                effective_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                superseded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                is_required_for_onboarding = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                    table.PrimaryKey("pk_legal_documents", x => x.id);
                });

            migrationBuilder.InsertData(
                schema: "users",
                table: "legal_documents",
                columns:
                [
                    "id",
                    "document_type",
                    "version",
                    "title",
                    "markdown_content",
                    "content_hash",
                    "effective_at",
                    "published_at",
                    "superseded_at",
                    "is_required_for_onboarding",
                ],
                values: new object[,]
                {
                    {
                        new Guid("11111111-1111-4111-8111-111111111111"),
                        "TermsOfService",
                        "1.0",
                        "Terms of Service",
                        """
                        # Terms of Service

                        Version 1.0

                        These terms describe the rules for using Modulith. By creating an account and completing onboarding, you agree to use the service lawfully, keep your account credentials secure, and avoid actions that could harm the service or other users.

                        We may update these terms when the service changes. If a new version requires acceptance, the application will ask you to review and accept the updated terms before continuing.

                        """,
                        "46f2426afe6a937c8c214d0518dee4c6d406c31dbd7c38a620ff0dc7d288626c",
                        new DateTimeOffset(2026, 5, 19, 0, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2026, 5, 19, 0, 0, 0, TimeSpan.Zero),
                        null,
                        true,
                    },
                    {
                        new Guid("22222222-2222-4222-8222-222222222222"),
                        "PrivacyPolicy",
                        "1.0",
                        "Privacy Policy",
                        """
                        # Privacy Policy

                        Version 1.0

                        This policy explains how Modulith handles account information, profile details, security events, consent records, and legal acceptance records. We use this information to provide the service, protect accounts, comply with legal obligations, and respect communication preferences.

                        Marketing email consent is optional. You can use the service without consenting to marketing emails.

                        """,
                        "2bfe05beefbf578dd28cc4e32fc76a5ee150d5535d9a63f8a6c26e3b13e9ccd4",
                        new DateTimeOffset(2026, 5, 19, 0, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2026, 5, 19, 0, 0, 0, TimeSpan.Zero),
                        null,
                        true,
                    },
                });

            migrationBuilder.CreateIndex(
                name: "ix_terms_acceptances_legal_document_id",
                schema: "users",
            table: "terms_acceptances",
            column: "legal_document_id");

        migrationBuilder.CreateIndex(
            name: "ix_legal_documents_document_type_is_required_for_onboarding_su",
            schema: "users",
            table: "legal_documents",
            columns: new[] { "document_type", "is_required_for_onboarding", "superseded_at" });

        migrationBuilder.CreateIndex(
            name: "ix_legal_documents_document_type_version",
            schema: "users",
            table: "legal_documents",
            columns: new[] { "document_type", "version" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "fk_terms_acceptances_legal_documents_legal_document_id",
            schema: "users",
            table: "terms_acceptances",
            column: "legal_document_id",
            principalSchema: "users",
            principalTable: "legal_documents",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_terms_acceptances_legal_documents_legal_document_id",
            schema: "users",
            table: "terms_acceptances");

        migrationBuilder.DropTable(
            name: "legal_documents",
            schema: "users");

        migrationBuilder.DropIndex(
            name: "ix_terms_acceptances_legal_document_id",
            schema: "users",
            table: "terms_acceptances");

        migrationBuilder.DropColumn(
            name: "content_hash",
            schema: "users",
            table: "terms_acceptances");

        migrationBuilder.DropColumn(
            name: "document_type",
            schema: "users",
            table: "terms_acceptances");

        migrationBuilder.DropColumn(
            name: "legal_document_id",
            schema: "users",
            table: "terms_acceptances");
    }
}
