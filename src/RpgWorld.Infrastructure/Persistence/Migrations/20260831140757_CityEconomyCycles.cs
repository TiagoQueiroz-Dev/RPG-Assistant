using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RpgWorld.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CityEconomyCycles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "economic_cycle_count",
                schema: "rpg_world",
                table: "cities",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_economic_cycle_at_utc",
                schema: "rpg_world",
                table: "cities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "resource_markets",
                schema: "rpg_world",
                table: "cities",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cities_economic_cycle_count",
                schema: "rpg_world",
                table: "cities",
                sql: "economic_cycle_count >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_cities_economic_cycle_count",
                schema: "rpg_world",
                table: "cities");

            migrationBuilder.DropColumn(
                name: "economic_cycle_count",
                schema: "rpg_world",
                table: "cities");

            migrationBuilder.DropColumn(
                name: "last_economic_cycle_at_utc",
                schema: "rpg_world",
                table: "cities");

            migrationBuilder.DropColumn(
                name: "resource_markets",
                schema: "rpg_world",
                table: "cities");
        }
    }
}
