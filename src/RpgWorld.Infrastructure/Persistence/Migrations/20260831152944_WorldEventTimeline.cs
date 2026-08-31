using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RpgWorld.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorldEventTimeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "world_events",
                schema: "rpg_world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    timestamp_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    position_x = table.Column<int>(type: "integer", nullable: true),
                    position_y = table.Column<int>(type: "integer", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    payload_version = table.Column<int>(type: "integer", nullable: false),
                    actor_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_world_events", x => x.id);
                    table.CheckConstraint("ck_world_events_payload_version", "payload_version > 0");
                    table.CheckConstraint("ck_world_events_position", "(position_x IS NULL AND position_y IS NULL) OR (position_x IS NOT NULL AND position_y IS NOT NULL AND position_x >= 0 AND position_y >= 0)");
                    table.ForeignKey(
                        name: "FK_world_events_worlds_world_id",
                        column: x => x.world_id,
                        principalSchema: "rpg_world",
                        principalTable: "worlds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_world_events_actor_ids",
                schema: "rpg_world",
                table: "world_events",
                column: "actor_ids")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_world_events_world_position",
                schema: "rpg_world",
                table: "world_events",
                columns: new[] { "world_id", "position_x", "position_y" });

            migrationBuilder.CreateIndex(
                name: "ix_world_events_world_timeline",
                schema: "rpg_world",
                table: "world_events",
                columns: new[] { "world_id", "timestamp_utc", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_world_events_world_type_time",
                schema: "rpg_world",
                table: "world_events",
                columns: new[] { "world_id", "type", "timestamp_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "world_events",
                schema: "rpg_world");
        }
    }
}
