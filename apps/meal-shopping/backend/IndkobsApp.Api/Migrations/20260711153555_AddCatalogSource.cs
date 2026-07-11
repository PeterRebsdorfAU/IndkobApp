using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IndkobsApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceHouseholdId",
                table: "CatalogRecipes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceRecipeId",
                table: "CatalogRecipes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CatalogRecipes_SourceHouseholdId",
                table: "CatalogRecipes",
                column: "SourceHouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogRecipes_SourceRecipeId",
                table: "CatalogRecipes",
                column: "SourceRecipeId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CatalogRecipes_Households_SourceHouseholdId",
                table: "CatalogRecipes",
                column: "SourceHouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CatalogRecipes_Recipes_SourceRecipeId",
                table: "CatalogRecipes",
                column: "SourceRecipeId",
                principalTable: "Recipes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CatalogRecipes_Households_SourceHouseholdId",
                table: "CatalogRecipes");

            migrationBuilder.DropForeignKey(
                name: "FK_CatalogRecipes_Recipes_SourceRecipeId",
                table: "CatalogRecipes");

            migrationBuilder.DropIndex(
                name: "IX_CatalogRecipes_SourceHouseholdId",
                table: "CatalogRecipes");

            migrationBuilder.DropIndex(
                name: "IX_CatalogRecipes_SourceRecipeId",
                table: "CatalogRecipes");

            migrationBuilder.DropColumn(
                name: "SourceHouseholdId",
                table: "CatalogRecipes");

            migrationBuilder.DropColumn(
                name: "SourceRecipeId",
                table: "CatalogRecipes");
        }
    }
}
