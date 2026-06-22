using IndkobsApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<ItemGroup> ItemGroups => Set<ItemGroup>();
    public DbSet<ItemGroupIngredient> ItemGroupIngredients => Set<ItemGroupIngredient>();
    public DbSet<Week> Weeks => Set<Week>();
    public DbSet<WeekRecipe> WeekRecipes => Set<WeekRecipe>();
    public DbSet<WeekItemGroup> WeekItemGroups => Set<WeekItemGroup>();
    public DbSet<WeekManualItem> WeekManualItems => Set<WeekManualItem>();
    public DbSet<ShoppingListCheck> ShoppingListChecks => Set<ShoppingListCheck>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Gem Unit-enum som læsbar tekst i alle tabeller (pænt i SSMS).
        var unitConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion
            .EnumToStringConverter<Unit>();

        b.Entity<Category>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<Ingredient>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(150);
            e.Property(x => x.NormalizedName).IsRequired().HasMaxLength(150);
            // Sikrer at samme ingrediens (trimmet/lowercased) ikke kan oprettes to gange.
            e.HasIndex(x => x.NormalizedName).IsUnique();
            e.HasOne(x => x.Category)
                .WithMany(c => c.Ingredients)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Recipe>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(150);
            e.Property(x => x.Note).HasMaxLength(1000);
        });

        b.Entity<RecipeIngredient>(e =>
        {
            e.Property(x => x.Quantity).HasPrecision(10, 3);
            e.Property(x => x.Unit).HasConversion(unitConverter).HasMaxLength(20);
            e.HasOne(x => x.Recipe)
                .WithMany(r => r.Ingredients)
                .HasForeignKey(x => x.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Ingredient)
                .WithMany()
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ItemGroup>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(150);
        });

        b.Entity<ItemGroupIngredient>(e =>
        {
            e.Property(x => x.Quantity).HasPrecision(10, 3);
            e.Property(x => x.Unit).HasConversion(unitConverter).HasMaxLength(20);
            e.HasOne(x => x.ItemGroup)
                .WithMany(g => g.Ingredients)
                .HasForeignKey(x => x.ItemGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Ingredient)
                .WithMany()
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Week>(e =>
        {
            // Én plan pr. (år, ugenummer).
            e.HasIndex(x => new { x.Year, x.WeekNumber }).IsUnique();
        });

        b.Entity<WeekRecipe>(e =>
        {
            e.HasOne(x => x.Week)
                .WithMany(w => w.Recipes)
                .HasForeignKey(x => x.WeekId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Recipe)
                .WithMany()
                .HasForeignKey(x => x.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<WeekItemGroup>(e =>
        {
            e.HasOne(x => x.Week)
                .WithMany(w => w.ItemGroups)
                .HasForeignKey(x => x.WeekId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ItemGroup)
                .WithMany()
                .HasForeignKey(x => x.ItemGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<WeekManualItem>(e =>
        {
            e.Property(x => x.FreeText).HasMaxLength(150);
            e.Property(x => x.Quantity).HasPrecision(10, 3);
            e.Property(x => x.Unit).HasConversion(unitConverter).HasMaxLength(20);
            e.HasOne(x => x.Week)
                .WithMany(w => w.ManualItems)
                .HasForeignKey(x => x.WeekId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Ingredient)
                .WithMany()
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ShoppingListCheck>(e =>
        {
            e.Property(x => x.LineKey).IsRequired().HasMaxLength(200);
            e.HasIndex(x => new { x.WeekId, x.LineKey }).IsUnique();
            e.HasOne(x => x.Week)
                .WithMany(w => w.Checks)
                .HasForeignKey(x => x.WeekId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
