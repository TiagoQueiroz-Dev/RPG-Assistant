using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RpgWorld.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AutonomousNpcHousing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "home_structure_id",
                schema: "rpg_world",
                table: "actors",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "housing_constructions",
                schema: "rpg_world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    x = table.Column<int>(type: "integer", nullable: false),
                    y = table.Column<int>(type: "integer", nullable: false),
                    required_wood = table.Column<int>(type: "integer", nullable: false),
                    required_stone = table.Column<int>(type: "integer", nullable: false),
                    consumed_wood = table.Column<int>(type: "integer", nullable: false),
                    consumed_stone = table.Column<int>(type: "integer", nullable: false),
                    progress = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resident_actor_ids = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_housing_constructions", x => x.id);
                    table.CheckConstraint("ck_housing_constructions_progress", "progress BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_housing_constructions_actors_owner_actor_id",
                        column: x => x.owner_actor_id,
                        principalSchema: "rpg_world",
                        principalTable: "actors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_housing_constructions_worlds_world_id",
                        column: x => x.world_id,
                        principalSchema: "rpg_world",
                        principalTable: "worlds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_housing_constructions_world_status",
                schema: "rpg_world",
                table: "housing_constructions",
                columns: new[] { "world_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_housing_constructions_active_owner",
                schema: "rpg_world",
                table: "housing_constructions",
                column: "owner_actor_id",
                unique: true,
                filter: "status = 'InProgress'");

            migrationBuilder.CreateIndex(
                name: "ux_housing_constructions_world_position",
                schema: "rpg_world",
                table: "housing_constructions",
                columns: new[] { "world_id", "x", "y" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "housing_constructions",
                schema: "rpg_world");

            migrationBuilder.DropColumn(
                name: "home_structure_id",
                schema: "rpg_world",
                table: "actors");
        }
    }
}
