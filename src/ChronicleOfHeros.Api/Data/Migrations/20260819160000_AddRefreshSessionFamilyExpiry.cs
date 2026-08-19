using ChronicleOfHeros.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChronicleOfHeros.Api.Data.Migrations
{
    [DbContext(typeof(ChronicleOfHerosDbContext))]
    [Migration("20260819160000_AddRefreshSessionFamilyExpiry")]
    public partial class AddRefreshSessionFamilyExpiry : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FamilyExpiresAtUtc",
                table: "RefreshSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "RefreshSessions"
                SET "FamilyExpiresAtUtc" = "CreatedAtUtc" + INTERVAL '90 days';
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "FamilyExpiresAtUtc",
                table: "RefreshSessions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FamilyExpiresAtUtc",
                table: "RefreshSessions");
        }
    }
}