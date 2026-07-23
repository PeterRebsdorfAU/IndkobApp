using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IndkobsApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HouseholdId = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_HouseholdId",
                table: "Users",
                column: "HouseholdId");

            // DATABEVARENDE BACKFILL: opret én bruger pr. eksisterende husstand ud fra dens
            // NUVÆRENDE login (Email + PasswordHash), så intet eksisterende login går i stykker.
            //  - DisplayName = husstandens navn (kan ændres af brugeren bagefter).
            //  - EmailConfirmed = TRUE: eksisterende logins betragtes som bekræftede.
            //  - PasswordHash kopieres 1:1 (Identity-hashformatet er ens for Household/User).
            //  - NOT EXISTS gør det idempotent; på en frisk/tom database indsættes ingenting.
            migrationBuilder.Sql(@"
INSERT INTO ""Users"" (""HouseholdId"", ""Email"", ""PasswordHash"", ""DisplayName"", ""CreatedUtc"", ""EmailConfirmed"")
SELECT h.""Id"", h.""Email"", h.""PasswordHash"", h.""Name"", h.""CreatedUtc"", TRUE
FROM ""Households"" h
WHERE NOT EXISTS (SELECT 1 FROM ""Users"" u WHERE u.""Email"" = h.""Email"");
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
