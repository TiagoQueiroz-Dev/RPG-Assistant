using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RpgWorld.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MerchantTradeCitySatisfaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "active_trade_route_count",
                schema: "rpg_world",
                table: "cities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "satisfaction",
                schema: "rpg_world",
                table: "cities",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 75m);

            migrationBuilder.AddCheckConstraint(
                name: "ck_cities_satisfaction",
                schema: "rpg_world",
                table: "cities",
                sql: "satisfaction >= 0 AND satisfaction <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_cities_trade_routes",
                schema: "rpg_world",
                table: "cities",
                sql: "active_trade_route_count >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_cities_satisfaction",
                schema: "rpg_world",
                table: "cities");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cities_trade_routes",
                schema: "rpg_world",
                table: "cities");

            migrationBuilder.DropColumn(
                name: "active_trade_route_count",
                schema: "rpg_world",
                table: "cities");

            migrationBuilder.DropColumn(
                name: "satisfaction",
                schema: "rpg_world",
                table: "cities");
        }
    }
}
