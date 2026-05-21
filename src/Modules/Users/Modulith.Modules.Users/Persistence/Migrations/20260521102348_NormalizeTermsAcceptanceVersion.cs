using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modulith.Modules.Users.Persistence.Migrations;

/// <inheritdoc />
public partial class NormalizeTermsAcceptanceVersion : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_terms_acceptances_user_id_version",
            schema: "users",
            table: "terms_acceptances");

        // Backfill (document_type, version, content_hash) from the FK row for any
        // acceptance that has one. Strips the legacy "tos:"/"privacy:" composite
        // produced by older code paths and replaces it with the bare version.
        //
        // This deliberately targets ALL FK-bearing rows, not just ones that look
        // composite — both because the operation is idempotent (overwriting a
        // correct value with the same correct value) and because we can't assume
        // every legacy bare prefix matches the current "tos:"/"privacy:" pattern.
        // Trust the FK over any heuristic on the version string.
        migrationBuilder.Sql("""
            UPDATE users.terms_acceptances ta
            SET document_type = ld.document_type,
                version = ld.version,
                content_hash = COALESCE(ta.content_hash, ld.content_hash)
            FROM users.legal_documents ld
            WHERE ta.legal_document_id = ld.id;
            """);

        // For orphan rows (no FK) still in composite form, derive type and version
        // from the prefix.
        migrationBuilder.Sql("""
            UPDATE users.terms_acceptances
            SET document_type = 'TermsOfService',
                version = substring(version FROM 5)
            WHERE legal_document_id IS NULL
              AND document_type IS NULL
              AND version LIKE 'tos:%';
            """);

        migrationBuilder.Sql("""
            UPDATE users.terms_acceptances
            SET document_type = 'PrivacyPolicy',
                version = substring(version FROM 9)
            WHERE legal_document_id IS NULL
              AND document_type IS NULL
              AND version LIKE 'privacy:%';
            """);

        // The backfill can produce (user_id, document_type, version) duplicates if
        // a user had both a legacy composite row and a newer FK-bearing row, or
        // any other historical overlap. Keep the most recent acceptance per tuple,
        // breaking ties in favour of the row that retained the FK, then by id.
        // Rows with document_type IS NULL are unrecoverable legacy and are left
        // alone — the new unique index treats NULL as non-conflicting.
        migrationBuilder.Sql("""
            WITH ranked AS (
                SELECT id,
                    ROW_NUMBER() OVER (
                        PARTITION BY user_id, document_type, version
                        ORDER BY accepted_at DESC,
                                 (legal_document_id IS NULL),
                                 id DESC
                    ) AS rn
                FROM users.terms_acceptances
                WHERE document_type IS NOT NULL
            )
            DELETE FROM users.terms_acceptances
            WHERE id IN (SELECT id FROM ranked WHERE rn > 1);
            """);

        migrationBuilder.CreateIndex(
            name: "ix_terms_acceptances_user_id_document_type_version",
            schema: "users",
            table: "terms_acceptances",
            columns: new[] { "user_id", "document_type", "version" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_terms_acceptances_user_id_document_type_version",
            schema: "users",
            table: "terms_acceptances");

        // Re-encode bare versions into the composite form expected by older code
        // so the old unique index on (user_id, version) holds.
        migrationBuilder.Sql("""
            UPDATE users.terms_acceptances
            SET version = 'tos:' || version
            WHERE document_type = 'TermsOfService'
              AND version NOT LIKE 'tos:%';
            """);

        migrationBuilder.Sql("""
            UPDATE users.terms_acceptances
            SET version = 'privacy:' || version
            WHERE document_type = 'PrivacyPolicy'
              AND version NOT LIKE 'privacy:%';
            """);

        migrationBuilder.CreateIndex(
            name: "ix_terms_acceptances_user_id_version",
            schema: "users",
            table: "terms_acceptances",
            columns: new[] { "user_id", "version" },
            unique: true);
    }
}
