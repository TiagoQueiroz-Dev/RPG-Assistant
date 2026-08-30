using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RpgWorld.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistentWorldClock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "world_clocks",
                schema: "rpg_world",
                columns: table => new
                {
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_instant = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tick_duration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    real_time_multiplier = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    last_synchronized_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_world_clocks", x => x.world_id);
                    table.ForeignKey(
                        name: "FK_world_clocks_worlds_world_id",
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
                name: "world_clocks",
                schema: "rpg_world");
        }
    }
}
