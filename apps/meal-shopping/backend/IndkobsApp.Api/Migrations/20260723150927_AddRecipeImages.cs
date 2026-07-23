using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IndkobsApp.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Image",
                table: "Recipes",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageContentType",
                table: "Recipes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Image",
                table: "CatalogRecipes",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageContentType",
                table: "CatalogRecipes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Image",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "ImageContentType",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Image",
                table: "CatalogRecipes");

            migrationBuilder.DropColumn(
                name: "ImageContentType",
                table: "CatalogRecipes");
        }
    }
}
