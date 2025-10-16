using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoETest.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TotalTimeMs",
                table: "Leaderboard",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalTimeMs",
                table: "Leaderboard");
        }
    }
}
