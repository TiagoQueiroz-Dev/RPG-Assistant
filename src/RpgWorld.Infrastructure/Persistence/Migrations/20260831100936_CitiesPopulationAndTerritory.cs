using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RpgWorld.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CitiesPopulationAndTerritory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "resident_city_id",
                schema: "rpg_world",
                table: "actors",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cities",
                schema: "rpg_world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    center_x = table.Column<int>(type: "integer", nullable: false),
                    center_y = table.Column<int>(type: "integer", nullable: false),
                    population = table.Column<int>(type: "integer", nullable: false),
                    wealth = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    governing_faction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    founded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    destroyed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    building_ids = table.Column<string>(type: "jsonb", nullable: false),
                    history = table.Column<string>(type: "jsonb", nullable: false),
                    resident_actor_ids = table.Column<string>(type: "jsonb", nullable: false),
                    resource_stocks = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cities", x => x.id);
                    table.CheckConstraint("ck_cities_destroyed_state", "(status = 'Destroyed' AND destroyed_at_utc IS NOT NULL AND population = 0) OR (status <> 'Destroyed' AND destroyed_at_utc IS NULL)");
                    table.CheckConstraint("ck_cities_population", "population >= 0");
                    table.CheckConstraint("ck_cities_version", "version >= 0");
                    table.CheckConstraint("ck_cities_wealth", "wealth >= 0");
                    table.ForeignKey(
                        name: "FK_cities_worlds_world_id",
                        column: x => x.world_id,
                        principalSchema: "rpg_world",
                        principalTable: "worlds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "city_territory_tiles",
                schema: "rpg_world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    x = table.Column<int>(type: "integer", nullable: false),
                    y = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    released_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_city_territory_tiles", x => x.id);
                    table.CheckConstraint("ck_city_territory_tiles_active", "(is_active AND released_at_utc IS NULL) OR (NOT is_active AND released_at_utc IS NOT NULL)");
                    table.CheckConstraint("ck_city_territory_tiles_x", "x >= 0");
                    table.CheckConstraint("ck_city_territory_tiles_y", "y >= 0");
                    table.ForeignKey(
                        name: "FK_city_territory_tiles_cities_city_id",
                        column: x => x.city_id,
                        principalSchema: "rpg_world",
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_actors_npc_resident_city",
                schema: "rpg_world",
                table: "actors",
                columns: new[] { "world_id", "resident_city_id" },
                filter: "actor_type = 'npc' AND resident_city_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_actors_resident_city_id",
                schema: "rpg_world",
                table: "actors",
                column: "resident_city_id");

            migrationBuilder.CreateIndex(
                name: "ix_cities_governing_faction",
                schema: "rpg_world",
                table: "cities",
                column: "governing_faction_id");

            migrationBuilder.CreateIndex(
                name: "ix_cities_world_status",
                schema: "rpg_world",
                table: "cities",
                columns: new[] { "world_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_cities_world_name",
                schema: "rpg_world",
                table: "cities",
                columns: new[] { "world_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_city_territory_city",
                schema: "rpg_world",
                table: "city_territory_tiles",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ux_city_territory_world_active_position",
                schema: "rpg_world",
                table: "city_territory_tiles",
                columns: new[] { "world_id", "x", "y" },
                unique: true,
                filter: "is_active");

            migrationBuilder.AddForeignKey(
                name: "FK_actors_cities_resident_city_id",
                schema: "rpg_world",
                table: "actors",
                column: "resident_city_id",
                principalSchema: "rpg_world",
                principalTable: "cities",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_actors_cities_resident_city_id",
                schema: "rpg_world",
                table: "actors");

            migrationBuilder.DropTable(
                name: "city_territory_tiles",
                schema: "rpg_world");

            migrationBuilder.DropTable(
                name: "cities",
                schema: "rpg_world");

            migrationBuilder.DropIndex(
                name: "ix_actors_npc_resident_city",
                schema: "rpg_world",
                table: "actors");

            migrationBuilder.DropIndex(
                name: "IX_actors_resident_city_id",
                schema: "rpg_world",
                table: "actors");

            migrationBuilder.DropColumn(
                name: "resident_city_id",
                schema: "rpg_world",
                table: "actors");
        }
    }
}
