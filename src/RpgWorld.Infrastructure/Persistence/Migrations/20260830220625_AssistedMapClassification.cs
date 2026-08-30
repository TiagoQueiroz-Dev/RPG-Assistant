using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RpgWorld.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssistedMapClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "biome_classification_confidence",
                schema: "rpg_world",
                table: "tiles",
                type: "numeric(4,3)",
                precision: 4,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "biome_classification_origin",
                schema: "rpg_world",
                table: "tiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Manual");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "biome_classification_confidence",
                schema: "rpg_world",
                table: "tiles");

            migrationBuilder.DropColumn(
                name: "biome_classification_origin",
                schema: "rpg_world",
                table: "tiles");
        }
    }
}
