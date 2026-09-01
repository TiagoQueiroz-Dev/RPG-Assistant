using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RpgWorld.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CampaignSimulationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "campaign_simulation_settings",
                schema: "rpg_world",
                columns: table => new
                {
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    npc_density = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    creature_spawn_rate = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    war_frequency = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    economic_difficulty = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    resource_scarcity = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    migration_rate = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    population_growth = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    simulation_speed = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_simulation_settings", x => x.world_id);
                    table.ForeignKey(
                        name: "FK_campaign_simulation_settings_worlds_world_id",
                        column: x => x.world_id,
                        principalSchema: "rpg_world",
                        principalTable: "worlds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "campaign_simulation_settings",
                schema: "rpg_world");
        }
    }
}
