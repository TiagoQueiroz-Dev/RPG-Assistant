using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RpgWorld.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConsequencePropagation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "causality_depth",
                schema: "rpg_world",
                table: "world_events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "causation_id",
                schema: "rpg_world",
                table: "world_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "correlation_id",
                schema: "rpg_world",
                table: "world_events",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql(
                "UPDATE rpg_world.world_events SET correlation_id = id " +
                "WHERE correlation_id = '00000000-0000-0000-0000-000000000000'::uuid;");

            migrationBuilder.AlterColumn<Guid>(
                name: "correlation_id",
                schema: "rpg_world",
                table: "world_events",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "world_consequences",
                schema: "rpg_world",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    world_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    magnitude = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    source_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_world_consequences", x => x.id);
                    table.CheckConstraint("ck_world_consequences_magnitude", "magnitude >= -100 AND magnitude <= 100");
                    table.ForeignKey(
                        name: "FK_world_consequences_worlds_world_id",
                        column: x => x.world_id,
                        principalSchema: "rpg_world",
                        principalTable: "worlds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_world_events_correlation",
                schema: "rpg_world",
                table: "world_events",
                columns: new[] { "world_id", "correlation_id", "causality_depth" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_world_events_causality_depth",
                schema: "rpg_world",
                table: "world_events",
                sql: "causality_depth >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_world_events_cause_depth",
                schema: "rpg_world",
                table: "world_events",
                sql: "(causality_depth = 0 AND causation_id IS NULL) OR (causality_depth > 0 AND causation_id IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_world_events_correlation",
                schema: "rpg_world",
                table: "world_events",
                sql: "correlation_id <> '00000000-0000-0000-0000-000000000000'::uuid");

            migrationBuilder.CreateIndex(
                name: "ix_world_consequences_world_time",
                schema: "rpg_world",
                table: "world_consequences",
                columns: new[] { "world_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_world_consequences_source_kind_target",
                schema: "rpg_world",
                table: "world_consequences",
                columns: new[] { "source_event_id", "kind", "target_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "world_consequences",
                schema: "rpg_world");

            migrationBuilder.DropIndex(
                name: "ix_world_events_correlation",
                schema: "rpg_world",
                table: "world_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_world_events_causality_depth",
                schema: "rpg_world",
                table: "world_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_world_events_cause_depth",
                schema: "rpg_world",
                table: "world_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_world_events_correlation",
                schema: "rpg_world",
                table: "world_events");

            migrationBuilder.DropColumn(
                name: "causality_depth",
                schema: "rpg_world",
                table: "world_events");

            migrationBuilder.DropColumn(
                name: "causation_id",
                schema: "rpg_world",
                table: "world_events");

            migrationBuilder.DropColumn(
                name: "correlation_id",
                schema: "rpg_world",
                table: "world_events");
        }
    }
}
