using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RpgWorld.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NaturalResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "resource_deposits",
                schema: "rpg_world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    inventory_item_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    scope = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    tile_id = table.Column<Guid>(type: "uuid", nullable: true),
                    region_x = table.Column<int>(type: "integer", nullable: false),
                    region_y = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    capacity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    regeneration_per_world_hour = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    is_discovered = table.Column<bool>(type: "boolean", nullable: false),
                    discovered_by_actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    discovered_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_consumer_kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    last_consumer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_world_event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_regenerated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_deposits", x => x.id);
                    table.CheckConstraint("ck_resource_deposits_discovery", "(is_discovered AND discovered_by_actor_id IS NOT NULL AND discovered_at_utc IS NOT NULL) OR (NOT is_discovered AND discovered_by_actor_id IS NULL AND discovered_at_utc IS NULL)");
                    table.CheckConstraint("ck_resource_deposits_location", "(scope = 'Tile' AND tile_id IS NOT NULL) OR (scope = 'Region' AND tile_id IS NULL)");
                    table.CheckConstraint("ck_resource_deposits_quantity", "quantity >= 0 AND capacity > 0 AND quantity <= capacity");
                    table.CheckConstraint("ck_resource_deposits_regeneration", "regeneration_per_world_hour >= 0");
                    table.CheckConstraint("ck_resource_deposits_version", "version >= 0");
                    table.ForeignKey(
                        name: "FK_resource_deposits_tiles_tile_id",
                        column: x => x.tile_id,
                        principalSchema: "rpg_world",
                        principalTable: "tiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_resource_deposits_worlds_world_id",
                        column: x => x.world_id,
                        principalSchema: "rpg_world",
                        principalTable: "worlds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_resource_deposits_world_discovered_resource",
                schema: "rpg_world",
                table: "resource_deposits",
                columns: new[] { "world_id", "is_discovered", "resource_code" });

            migrationBuilder.CreateIndex(
                name: "ix_resource_deposits_world_region_resource",
                schema: "rpg_world",
                table: "resource_deposits",
                columns: new[] { "world_id", "region_x", "region_y", "resource_code" });

            migrationBuilder.CreateIndex(
                name: "ux_resource_deposits_tile",
                schema: "rpg_world",
                table: "resource_deposits",
                column: "tile_id",
                unique: true,
                filter: "tile_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resource_deposits",
                schema: "rpg_world");
        }
    }
}
