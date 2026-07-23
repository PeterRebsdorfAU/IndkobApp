using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IndkobsApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Method",
                table: "Recipes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Method",
                table: "CatalogRecipes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Method",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Method",
                table: "CatalogRecipes");
        }
    }
}
