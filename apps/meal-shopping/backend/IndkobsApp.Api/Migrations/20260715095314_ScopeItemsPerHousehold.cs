using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IndkobsApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class ScopeItemsPerHousehold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Ingredients_NormalizedName",
                table: "Ingredients");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Name",
                table: "Categories");

            migrationBuilder.AddColumn<int>(
                name: "HouseholdId",
                table: "Ingredients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HouseholdId",
                table: "Categories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // DATABEVARENDE BACKFILL (kører før indeks/FK oprettes; hele migrationen er én transaktion):
            //  1) Alt eksisterende (kategorier + ingredienser) tilfalder hovedhusstanden (laveste Id).
            //  2) Alle andre husstande får en KOPI af hovedhusstandens kategorisæt.
            //  3) Ingredienser som andre husstande refererer (opskrifter/varegrupper/løse varer/lager)
            //     klones til dem (med kategori mappet via navn), og deres rækker peges om til klonen.
            // På en frisk/tom database gør blokken ingenting.
            migrationBuilder.Sql(@"
DO $$
DECLARE main_id integer;
BEGIN
  SELECT MIN(""Id"") INTO main_id FROM ""Households"";
  IF main_id IS NULL THEN RETURN; END IF;

  -- 1) Alt eksisterende tilhoerer hovedhusstanden
  UPDATE ""Categories""  SET ""HouseholdId"" = main_id;
  UPDATE ""Ingredients"" SET ""HouseholdId"" = main_id;

  -- 2) Kopiér kategorisættet til alle andre husstande
  INSERT INTO ""Categories"" (""Name"", ""SortOrder"", ""HouseholdId"")
  SELECT c.""Name"", c.""SortOrder"", h.""Id""
  FROM ""Categories"" c
  CROSS JOIN ""Households"" h
  WHERE c.""HouseholdId"" = main_id AND h.""Id"" <> main_id;

  -- 3a) Find (husstand, ingrediens)-par hvor en ANDEN husstand refererer ingrediensen
  CREATE TEMP TABLE _refs ON COMMIT DROP AS
  SELECT DISTINCT x.hid, x.iid FROM (
    SELECT r.""HouseholdId"" AS hid, ri.""IngredientId"" AS iid
      FROM ""RecipeIngredients"" ri JOIN ""Recipes"" r ON r.""Id"" = ri.""RecipeId""
    UNION
    SELECT g.""HouseholdId"", gi.""IngredientId""
      FROM ""ItemGroupIngredients"" gi JOIN ""ItemGroups"" g ON g.""Id"" = gi.""ItemGroupId""
    UNION
    SELECT w.""HouseholdId"", m.""IngredientId""
      FROM ""WeekManualItems"" m JOIN ""Weeks"" w ON w.""Id"" = m.""WeekId""
      WHERE m.""IngredientId"" IS NOT NULL
    UNION
    SELECT p.""HouseholdId"", p.""IngredientId"" FROM ""PantryItems"" p
  ) x WHERE x.hid <> main_id;

  -- 3b) Klon ingredienserne til de andre husstande (kategori mappes via navn)
  INSERT INTO ""Ingredients"" (""Name"", ""NormalizedName"", ""CategoryId"", ""HouseholdId"")
  SELECT i.""Name"", i.""NormalizedName"",
         (SELECT c2.""Id"" FROM ""Categories"" c2
            JOIN ""Categories"" c1 ON c1.""Id"" = i.""CategoryId""
           WHERE c2.""HouseholdId"" = f.hid AND c2.""Name"" = c1.""Name"" LIMIT 1),
         f.hid
  FROM _refs f JOIN ""Ingredients"" i ON i.""Id"" = f.iid;

  -- 3c) Peg de andre husstandes raekker om til deres egne kloner
  UPDATE ""RecipeIngredients"" ri SET ""IngredientId"" = ni.""Id""
  FROM ""Recipes"" r, ""Ingredients"" oi, ""Ingredients"" ni
  WHERE r.""Id"" = ri.""RecipeId"" AND r.""HouseholdId"" <> main_id
    AND oi.""Id"" = ri.""IngredientId""
    AND ni.""HouseholdId"" = r.""HouseholdId""
    AND ni.""NormalizedName"" = oi.""NormalizedName"" AND ni.""Id"" <> oi.""Id"";

  UPDATE ""ItemGroupIngredients"" gi SET ""IngredientId"" = ni.""Id""
  FROM ""ItemGroups"" g, ""Ingredients"" oi, ""Ingredients"" ni
  WHERE g.""Id"" = gi.""ItemGroupId"" AND g.""HouseholdId"" <> main_id
    AND oi.""Id"" = gi.""IngredientId""
    AND ni.""HouseholdId"" = g.""HouseholdId""
    AND ni.""NormalizedName"" = oi.""NormalizedName"" AND ni.""Id"" <> oi.""Id"";

  UPDATE ""WeekManualItems"" m SET ""IngredientId"" = ni.""Id""
  FROM ""Weeks"" w, ""Ingredients"" oi, ""Ingredients"" ni
  WHERE w.""Id"" = m.""WeekId"" AND w.""HouseholdId"" <> main_id
    AND m.""IngredientId"" IS NOT NULL AND oi.""Id"" = m.""IngredientId""
    AND ni.""HouseholdId"" = w.""HouseholdId""
    AND ni.""NormalizedName"" = oi.""NormalizedName"" AND ni.""Id"" <> oi.""Id"";

  UPDATE ""PantryItems"" p SET ""IngredientId"" = ni.""Id""
  FROM ""Ingredients"" oi, ""Ingredients"" ni
  WHERE p.""HouseholdId"" <> main_id AND oi.""Id"" = p.""IngredientId""
    AND ni.""HouseholdId"" = p.""HouseholdId""
    AND ni.""NormalizedName"" = oi.""NormalizedName"" AND ni.""Id"" <> oi.""Id"";
END $$;
");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_HouseholdId_NormalizedName",
                table: "Ingredients",
                columns: new[] { "HouseholdId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_HouseholdId_Name",
                table: "Categories",
                columns: new[] { "HouseholdId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Households_HouseholdId",
                table: "Categories",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Ingredients_Households_HouseholdId",
                table: "Ingredients",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Households_HouseholdId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Ingredients_Households_HouseholdId",
                table: "Ingredients");

            migrationBuilder.DropIndex(
                name: "IX_Ingredients_HouseholdId_NormalizedName",
                table: "Ingredients");

            migrationBuilder.DropIndex(
                name: "IX_Categories_HouseholdId_Name",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "HouseholdId",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "HouseholdId",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_NormalizedName",
                table: "Ingredients",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);
        }
    }
}
