using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IndkobsApp.Api.Migrations
{
    /// <summary>
    /// Enheder gøres til FRI TEKST. Tidligere blev <c>Unit</c>-enum'en gemt som sit
    /// medlemsnavn ("G", "Kg", "Daase" …). Nu er kolonnen ren fri tekst, og vi konverterer
    /// de eksisterende enum-navne til de menneskevenlige, små-bogstavs skrivemåder ("g", "kg",
    /// "dåse" …) — samme skrivemåde som enheds-vælgeren nu foreslår. Konverteringen er
    /// DATA-BEVARENDE (kun en oversættelse af eksisterende værdier; ingen rækker slettes) og
    /// idempotent (rører kun de gamle enum-navne; allerede-fritekst enheder bevares uændret).
    /// Kolonnen udvides samtidig fra 20 til 40 tegn, så egne enheder får plads.
    /// </summary>
    /// <inheritdoc />
    public partial class FreeTextUnits : Migration
    {
        // Alle tabeller med en Unit-kolonne.
        private static readonly string[] Tables =
        {
            "RecipeIngredients", "ItemGroupIngredients", "WeekManualItems",
            "CatalogRecipeIngredients", "OrderLines",
        };

        // Gammelt enum-navn → ny fri-tekst skrivemåde (og omvendt i Down).
        private static readonly (string Enum, string Text)[] Mapping =
        {
            ("Stk", "stk"), ("G", "g"), ("Kg", "kg"), ("Ml", "ml"), ("L", "l"),
            ("Spsk", "spsk"), ("Tsk", "tsk"), ("Daase", "dåse"), ("Pakke", "pakke"),
            ("Knivspids", "knivspids"), ("Bundt", "bundt"), ("Fed", "fed"),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Udvid kolonnen (20 → 40) så egne enheder får plads.
            foreach (var t in Tables)
                migrationBuilder.AlterColumn<string>(
                    name: "Unit", table: t,
                    type: "character varying(40)", maxLength: 40, nullable: false,
                    oldClrType: typeof(string), oldType: "character varying(20)", oldMaxLength: 20);

            // 2) Oversæt eksisterende enum-navne til fri-tekst skrivemåder (bevarer alle rækker).
            foreach (var t in Tables)
                migrationBuilder.Sql(BuildConvertSql(t, from: e => e.Enum, to: e => e.Text));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1) Oversæt de kendte fri-tekst enheder tilbage til enum-navne (egne enheder bevares
            //    som de er, hvis de passer i 20 tegn).
            foreach (var t in Tables)
                migrationBuilder.Sql(BuildConvertSql(t, from: e => e.Text, to: e => e.Enum));

            // 2) Skrump kolonnen tilbage til 20 tegn.
            foreach (var t in Tables)
                migrationBuilder.AlterColumn<string>(
                    name: "Unit", table: t,
                    type: "character varying(20)", maxLength: 20, nullable: false,
                    oldClrType: typeof(string), oldType: "character varying(40)", oldMaxLength: 40);
        }

        // Bygger en CASE-baseret UPDATE der kun rører de kendte værdier (WHERE ... IN),
        // og lader alt andet (fx egne enheder) stå urørt via ELSE.
        private static string BuildConvertSql(
            string table, System.Func<(string Enum, string Text), string> from,
            System.Func<(string Enum, string Text), string> to)
        {
            var cases = string.Join(" ", System.Linq.Enumerable.Select(Mapping,
                m => $"WHEN '{from(m)}' THEN '{Escape(to(m))}'"));
            var inList = string.Join(", ", System.Linq.Enumerable.Select(Mapping,
                m => $"'{from(m)}'"));
            return $"UPDATE \"{table}\" SET \"Unit\" = CASE \"Unit\" {cases} ELSE \"Unit\" END " +
                   $"WHERE \"Unit\" IN ({inList});";
        }

        private static string Escape(string s) => s.Replace("'", "''");
    }
}
