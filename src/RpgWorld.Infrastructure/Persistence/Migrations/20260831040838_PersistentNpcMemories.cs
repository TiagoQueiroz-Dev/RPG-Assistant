using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RpgWorld.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistentNpcMemories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "npc_memories",
                schema: "rpg_world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    importance = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_npc_memories", x => x.id);
                    table.CheckConstraint("ck_npc_memories_importance", "importance BETWEEN 1 AND 100");
                    table.ForeignKey(
                        name: "FK_npc_memories_actors_actor_id",
                        column: x => x.actor_id,
                        principalSchema: "rpg_world",
                        principalTable: "actors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_npc_memories_actor_created",
                schema: "rpg_world",
                table: "npc_memories",
                columns: new[] { "actor_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_npc_memories_actor_target_importance",
                schema: "rpg_world",
                table: "npc_memories",
                columns: new[] { "actor_id", "target_id", "importance" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_npc_memories_expiration",
                schema: "rpg_world",
                table: "npc_memories",
                column: "expires_at",
                filter: "expires_at IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "npc_memories",
                schema: "rpg_world");
        }
    }
}
