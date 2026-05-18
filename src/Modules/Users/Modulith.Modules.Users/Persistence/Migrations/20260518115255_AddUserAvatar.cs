using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modulith.Modules.Users.Persistence.Migrations;

/// <inheritdoc />
public partial class AddUserAvatar : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "avatar_container",
            schema: "users",
            table: "users",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "avatar_content_type",
            schema: "users",
            table: "users",
            type: "character varying(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "avatar_key",
            schema: "users",
            table: "users",
            type: "character varying(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "avatar_size_bytes",
            schema: "users",
            table: "users",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "avatar_updated_at",
            schema: "users",
            table: "users",
            type: "timestamp with time zone",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "avatar_container",
            schema: "users",
            table: "users");

        migrationBuilder.DropColumn(
            name: "avatar_content_type",
            schema: "users",
            table: "users");

        migrationBuilder.DropColumn(
            name: "avatar_key",
            schema: "users",
            table: "users");

        migrationBuilder.DropColumn(
            name: "avatar_size_bytes",
            schema: "users",
            table: "users");

        migrationBuilder.DropColumn(
            name: "avatar_updated_at",
            schema: "users",
            table: "users");
    }
}
