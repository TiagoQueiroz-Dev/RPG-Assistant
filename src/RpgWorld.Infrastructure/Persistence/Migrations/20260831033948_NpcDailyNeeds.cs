using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RpgWorld.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NpcDailyNeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "energy",
                schema: "rpg_world",
                table: "actors",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "family_ids",
                schema: "rpg_world",
                table: "actors",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "goals",
                schema: "rpg_world",
                table: "actors",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "home_x",
                schema: "rpg_world",
                table: "actors",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "home_y",
                schema: "rpg_world",
                table: "actors",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "hunger",
                schema: "rpg_world",
                table: "actors",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "job",
                schema: "rpg_world",
                table: "actors",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "money",
                schema: "rpg_world",
                table: "actors",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "needs_updated_at",
                schema: "rpg_world",
                table: "actors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE rpg_world.actors
                SET hunger = 0,
                    energy = 100,
                    money = 0,
                    needs_updated_at = created_at_utc,
                    family_ids = '[]'::jsonb,
                    goals = '[]'::jsonb
                WHERE actor_type = 'npc';
                """);

            migrationBuilder.CreateIndex(
                name: "ix_actors_npc_energy",
                schema: "rpg_world",
                table: "actors",
                columns: new[] { "world_id", "energy" },
                filter: "actor_type = 'npc' AND status <> 'Dead'");

            migrationBuilder.CreateIndex(
                name: "ix_actors_npc_hunger",
                schema: "rpg_world",
                table: "actors",
                columns: new[] { "world_id", "hunger" },
                descending: new[] { false, true },
                filter: "actor_type = 'npc' AND status <> 'Dead'");

            migrationBuilder.CreateIndex(
                name: "ix_actors_npc_job",
                schema: "rpg_world",
                table: "actors",
                columns: new[] { "world_id", "job" },
                filter: "actor_type = 'npc'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_actors_npc_energy",
                schema: "rpg_world",
                table: "actors",
                sql: "actor_type <> 'npc' OR (energy IS NOT NULL AND energy BETWEEN 0 AND 100)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_actors_npc_hunger",
                schema: "rpg_world",
                table: "actors",
                sql: "actor_type <> 'npc' OR (hunger IS NOT NULL AND hunger BETWEEN 0 AND 100)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_actors_npc_money",
                schema: "rpg_world",
                table: "actors",
                sql: "actor_type <> 'npc' OR (money IS NOT NULL AND money >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_actors_npc_state_required",
                schema: "rpg_world",
                table: "actors",
                sql: "actor_type <> 'npc' OR (needs_updated_at IS NOT NULL AND family_ids IS NOT NULL AND goals IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_actors_npc_energy",
                schema: "rpg_world",
                table: "actors");

            migrationBuilder.DropIndex(
                name: "ix_actors_npc_hunger",
                schema: "rpg_world",
                table: "actors");

            migrationBuilder.DropIndex(
                name: "ix_actors_npc_job",
                schema: "rpg_world",
                table: "actors");

            migrationBuilder.DropCheckConstraint(
                name: "ck_actors_npc_energy",
                schema: "rpg_world",
                table: "actors");

            migrationBuilder.DropCheckConstraint(
                name: "ck_actors_npc_hunger",
                schema: "rpg_world",
                table: "actors");

            migrationBuilder.DropCheckConstraint(
                name: "ck_actors_npc_money",
                schema: "rpg_world",
                table: "actors");

            migrationBuilder.DropCheckConstraint(
                name: "ck_actors_npc_state_required",
                schema: "rpg_world",
                table: "actors");

            migrationBuilder.DropColumn(
                name: "energy",
                schema: "rpg_world",
                table: "actors");

            migrationBuilder.DropColumn(
                name: "family_ids",
                schema: "rpg_world",
                table: "actors");

            migrationBuilder.DropColumn(
                name: "goals",
                schema: "rpg_world",
                table: "actors");

            migrationBuilder.DropColumn(
                name: "home_x",
                schema: "rpg_world",
                table: "actors");

            migrationBuilder.DropColumn(
                name: "home_y",
                schema: "rpg_world",
                table: "actors");

            migrationBuilder.DropColumn(
                name: "hunger",
                schema: "rpg_world",
                table: "actors");

            migrationBuilder.DropColumn(
                name: "job",
                schema: "rpg_world",
                table: "actors");

            migrationBuilder.DropColumn(
                name: "money",
                schema: "rpg_world",
                table: "actors");

            migrationBuilder.DropColumn(
                name: "needs_updated_at",
                schema: "rpg_world",
                table: "actors");
        }
    }
}
