using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProHair.NL.Migrations
{
    public partial class AddDataProtectionKeysAndMondayClosed : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DataProtectionKeys table (for persistent antiforgery / key ring)
            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(nullable: true),
                    Xml = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            // Update seed: Monday closed, Sunday open
            migrationBuilder.Sql("UPDATE \"WeeklyOpenHours\" SET \"IsClosed\" = TRUE WHERE \"Day\" = 1;");
            migrationBuilder.Sql("UPDATE \"WeeklyOpenHours\" SET \"IsClosed\" = FALSE WHERE \"Day\" = 0;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "DataProtectionKeys");

            // revert to original seed: Sunday closed, Monday open
            migrationBuilder.Sql("UPDATE \"WeeklyOpenHours\" SET \"IsClosed\" = TRUE WHERE \"Day\" = 0;");
            migrationBuilder.Sql("UPDATE \"WeeklyOpenHours\" SET \"IsClosed\" = FALSE WHERE \"Day\" = 1;");
        }
    }
}
