using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RpgWorld.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SpatialWorldModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "worlds",
                schema: "rpg_world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    chunk_size = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_worlds", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "chunks",
                schema: "rpg_world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    coordinate_x = table.Column<int>(type: "integer", nullable: false),
                    coordinate_y = table.Column<int>(type: "integer", nullable: false),
                    origin_x = table.Column<int>(type: "integer", nullable: false),
                    origin_y = table.Column<int>(type: "integer", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chunks", x => x.id);
                    table.ForeignKey(
                        name: "FK_chunks_worlds_world_id",
                        column: x => x.world_id,
                        principalSchema: "rpg_world",
                        principalTable: "worlds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tiles",
                schema: "rpg_world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    x = table.Column<int>(type: "integer", nullable: false),
                    y = table.Column<int>(type: "integer", nullable: false),
                    terrain_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    biome_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    elevation = table.Column<short>(type: "smallint", nullable: false),
                    temperature_celsius = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    humidity = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: false),
                    resource_deposit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    structure_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occupant_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_tiles_worlds_world_id",
                        column: x => x.world_id,
                        principalSchema: "rpg_world",
                        principalTable: "worlds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_chunks_world_coordinate",
                schema: "rpg_world",
                table: "chunks",
                columns: new[] { "world_id", "coordinate_x", "coordinate_y" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_tiles_world_position",
                schema: "rpg_world",
                table: "tiles",
                columns: new[] { "world_id", "x", "y" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chunks",
                schema: "rpg_world");

            migrationBuilder.DropTable(
                name: "tiles",
                schema: "rpg_world");

            migrationBuilder.DropTable(
                name: "worlds",
                schema: "rpg_world");
        }
    }
}
