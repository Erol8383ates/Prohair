using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProHair.NL.Migrations
{
    /// <inheritdoc />
    public partial class Availability_Baseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Baseline only — the tables already exist in the database.
            // No schema changes here on purpose.
            //
            // If you want to ensure the single BusinessSettings row exists,
            // you can uncomment this safe upsert:
            //
            // migrationBuilder.Sql(@"
            // INSERT INTO ""BusinessSettings"" (""Id"",""TimeZone"",""SlotMinutes"",""MinNoticeHours"",""MaxSimultaneousBookings"")
            // SELECT 1,'Europe/Brussels',30,2,1
            // WHERE NOT EXISTS (SELECT 1 FROM ""BusinessSettings"" WHERE ""Id""=1);
            // ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: nothing to roll back for a baseline migration.
        }
    }
}
