using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProHair.NL.Migrations
{
    public partial class DataProtectionKeysAndMondayClosed : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DataProtectionKeys tablosu
            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(nullable: true),
                    Xml = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            // WeeklyOpenHours seed update (Pazartesi kapalı olacak şekilde)
            migrationBuilder.Sql("UPDATE \"WeeklyOpenHours\" SET \"IsClosed\" = TRUE WHERE \"Day\" = 1;");
            migrationBuilder.Sql("UPDATE \"WeeklyOpenHours\" SET \"IsClosed\" = FALSE WHERE \"Day\" = 0;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            // Eski duruma geri döner (Pazar kapalı)
            migrationBuilder.Sql("UPDATE \"WeeklyOpenHours\" SET \"IsClosed\" = TRUE WHERE \"Day\" = 0;");
            migrationBuilder.Sql("UPDATE \"WeeklyOpenHours\" SET \"IsClosed\" = FALSE WHERE \"Day\" = 1;");
        }
    }
}
