using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RpgWorld.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FactionsLeadershipTerritory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "factions",
                schema: "rpg_world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    leader_actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    wealth = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    military_power = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dissolved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    controlled_city_ids = table.Column<string>(type: "jsonb", nullable: false),
                    history = table.Column<string>(type: "jsonb", nullable: false),
                    member_actor_ids = table.Column<string>(type: "jsonb", nullable: false),
                    relations = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_factions", x => x.id);
                    table.CheckConstraint("ck_factions_dissolved_state", "(status = 'Dissolved' AND dissolved_at_utc IS NOT NULL AND leader_actor_id IS NULL) OR (status = 'Active' AND dissolved_at_utc IS NULL AND leader_actor_id IS NOT NULL)");
                    table.CheckConstraint("ck_factions_military_power", "military_power >= 0");
                    table.CheckConstraint("ck_factions_version", "version >= 0");
                    table.CheckConstraint("ck_factions_wealth", "wealth >= 0");
                    table.ForeignKey(
                        name: "FK_factions_worlds_world_id",
                        column: x => x.world_id,
                        principalSchema: "rpg_world",
                        principalTable: "worlds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "faction_territory_tiles",
                schema: "rpg_world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    faction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    x = table.Column<int>(type: "integer", nullable: false),
                    y = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    released_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_faction_territory_tiles", x => x.id);
                    table.CheckConstraint("ck_faction_territory_tiles_active", "(is_active AND released_at_utc IS NULL) OR (NOT is_active AND released_at_utc IS NOT NULL)");
                    table.CheckConstraint("ck_faction_territory_tiles_x", "x >= 0");
                    table.CheckConstraint("ck_faction_territory_tiles_y", "y >= 0");
                    table.ForeignKey(
                        name: "FK_faction_territory_tiles_factions_faction_id",
                        column: x => x.faction_id,
                        principalSchema: "rpg_world",
                        principalTable: "factions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_faction_territory_faction",
                schema: "rpg_world",
                table: "faction_territory_tiles",
                column: "faction_id");

            migrationBuilder.CreateIndex(
                name: "ux_faction_territory_world_active_position",
                schema: "rpg_world",
                table: "faction_territory_tiles",
                columns: new[] { "world_id", "x", "y" },
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_factions_leader",
                schema: "rpg_world",
                table: "factions",
                column: "leader_actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_factions_world_status",
                schema: "rpg_world",
                table: "factions",
                columns: new[] { "world_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_factions_world_name",
                schema: "rpg_world",
                table: "factions",
                columns: new[] { "world_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "faction_territory_tiles",
                schema: "rpg_world");

            migrationBuilder.DropTable(
                name: "factions",
                schema: "rpg_world");
        }
    }
}
