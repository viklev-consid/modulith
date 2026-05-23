using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Modulith.Modules.Organizations.Persistence;

#nullable disable

namespace Modulith.Modules.Organizations.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(OrganizationsDbContext))]
[Migration("20260521211618_AnonymizeOrganizationUserReferences")]
public partial class AnonymizeOrganizationUserReferences : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>(
            name: "invited_by_user_id",
            schema: "organizations",
            table: "organization_invitations",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");

        migrationBuilder.AlterColumn<Guid>(
            name: "user_id",
            schema: "organizations",
            table: "organization_memberships",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM organizations.organization_invitations
            WHERE invited_by_user_id IS NULL;
            """);

        migrationBuilder.Sql("""
            DELETE FROM organizations.organization_memberships
            WHERE user_id IS NULL;
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "invited_by_user_id",
            schema: "organizations",
            table: "organization_invitations",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "user_id",
            schema: "organizations",
            table: "organization_memberships",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);
    }
}
