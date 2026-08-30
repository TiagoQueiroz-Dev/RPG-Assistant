using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RpgWorld.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RegionSimulationLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "aggregate_economic_output",
                schema: "rpg_world",
                table: "chunks",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "aggregate_military_strength",
                schema: "rpg_world",
                table: "chunks",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "aggregate_population",
                schema: "rpg_world",
                table: "chunks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "aggregate_production_output",
                schema: "rpg_world",
                table: "chunks",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "simulation_level",
                schema: "rpg_world",
                table: "chunks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Abstract");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "aggregate_economic_output",
                schema: "rpg_world",
                table: "chunks");

            migrationBuilder.DropColumn(
                name: "aggregate_military_strength",
                schema: "rpg_world",
                table: "chunks");

            migrationBuilder.DropColumn(
                name: "aggregate_population",
                schema: "rpg_world",
                table: "chunks");

            migrationBuilder.DropColumn(
                name: "aggregate_production_output",
                schema: "rpg_world",
                table: "chunks");

            migrationBuilder.DropColumn(
                name: "simulation_level",
                schema: "rpg_world",
                table: "chunks");
        }
    }
}
