using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RpgWorld.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NpcPersonalityTraits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "trait_codes",
                schema: "rpg_world",
                table: "actors",
                type: "jsonb",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE rpg_world.actors SET trait_codes = '[]'::jsonb WHERE actor_type = 'npc';");

            migrationBuilder.AddCheckConstraint(
                name: "ck_actors_npc_traits_required",
                schema: "rpg_world",
                table: "actors",
                sql: "actor_type <> 'npc' OR trait_codes IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_actors_npc_traits_required",
                schema: "rpg_world",
                table: "actors");

            migrationBuilder.DropColumn(
                name: "trait_codes",
                schema: "rpg_world",
                table: "actors");
        }
    }
}
