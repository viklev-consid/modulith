using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Modulith.Modules.Organizations.Persistence;

#nullable disable

namespace Modulith.Modules.Organizations.Persistence.Migrations;

[DbContext(typeof(OrganizationsDbContext))]
[Migration("20260602120000_AddOrganizationOwnerMutationConcurrency")]
public partial class AddOrganizationOwnerMutationConcurrency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "owner_mutation_version",
            schema: "organizations",
            table: "organizations",
            type: "integer",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "owner_mutation_version",
            schema: "organizations",
            table: "organizations");
    }
}
