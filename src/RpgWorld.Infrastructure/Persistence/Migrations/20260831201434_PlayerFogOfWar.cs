using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RpgWorld.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlayerFogOfWar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "player_tile_knowledge",
                schema: "rpg_world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    x = table.Column<int>(type: "integer", nullable: false),
                    y = table.Column<int>(type: "integer", nullable: false),
                    historical_state = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    discovered_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    known_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_visible_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_tile_knowledge", x => x.id);
                    table.CheckConstraint("ck_player_tile_knowledge_historical_state", "historical_state IN ('Discovered', 'Known')");
                    table.CheckConstraint("ck_player_tile_knowledge_position", "x >= 0 AND y >= 0");
                    table.CheckConstraint("ck_player_tile_knowledge_version", "version >= 0");
                    table.ForeignKey(
                        name: "FK_player_tile_knowledge_actors_player_actor_id",
                        column: x => x.player_actor_id,
                        principalSchema: "rpg_world",
                        principalTable: "actors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_player_tile_knowledge_worlds_world_id",
                        column: x => x.world_id,
                        principalSchema: "rpg_world",
                        principalTable: "worlds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_player_tile_knowledge_player_state",
                schema: "rpg_world",
                table: "player_tile_knowledge",
                columns: new[] { "player_actor_id", "historical_state" });

            migrationBuilder.CreateIndex(
                name: "IX_player_tile_knowledge_world_id",
                schema: "rpg_world",
                table: "player_tile_knowledge",
                column: "world_id");

            migrationBuilder.CreateIndex(
                name: "ux_player_tile_knowledge_player_position",
                schema: "rpg_world",
                table: "player_tile_knowledge",
                columns: new[] { "player_actor_id", "x", "y" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_tile_knowledge",
                schema: "rpg_world");
        }
    }
}
