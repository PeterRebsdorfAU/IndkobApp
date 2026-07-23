using IndkobsApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace IndkobsApp.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Household> Households => Set<Household>();
    public DbSet<User> Users => Set<User>();
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
    public DbSet<CatalogRecipe> CatalogRecipes => Set<CatalogRecipe>();
    public DbSet<CatalogRecipeIngredient> CatalogRecipeIngredients => Set<CatalogRecipeIngredient>();
    public DbSet<WeekShareToken> WeekShareTokens => Set<WeekShareToken>();
    public DbSet<RecipeShare> RecipeShares => Set<RecipeShare>();
    public DbSet<HouseholdTask> HouseholdTasks => Set<HouseholdTask>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Enheder er fri tekst (string). Kolonnen rummer både de kendte enheder ("g", "stk",
        // "dåse" …) og brugerens egne (fx "glas", "kviste"); derfor lidt ekstra plads.
        b.Entity<Household>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(150);
            e.Property(x => x.Email).IsRequired().HasMaxLength(200);
            e.Property(x => x.PasswordHash).IsRequired();
            e.HasIndex(x => x.Email).IsUnique(); // ét login pr. email
        });

        b.Entity<User>(e =>
        {
            e.Property(x => x.Email).IsRequired().HasMaxLength(200);
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.DisplayName).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.Email).IsUnique(); // email er unikt login på tværs af alle brugere
            // Slettes husstanden, ryger dens brugere med (cascade). HouseholdEraser rydder
            // også eksplicit op, så det virker på både relationelle providers og InMemory.
            e.HasOne(x => x.Household).WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.HouseholdId);
        });

        b.Entity<Category>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            // Unik pr. husstand (hver husstand har sin egen butiksopsætning).
            e.HasIndex(x => new { x.HouseholdId, x.Name }).IsUnique();
            // Restrict: husstands-sletning rydder eksplicit op i AdminController (afhængighedsorden).
            e.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Ingredient>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(150);
            e.Property(x => x.NormalizedName).IsRequired().HasMaxLength(150);
            // Sikrer at samme ingrediens (trimmet/lowercased) ikke kan oprettes to gange — PR. HUSSTAND.
            e.HasIndex(x => new { x.HouseholdId, x.NormalizedName }).IsUnique();
            e.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category)
                .WithMany(c => c.Ingredients)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Recipe>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(150);
            e.Property(x => x.Note).HasMaxLength(1000);
            // Billede gemmes som bytea (byte[] mapper automatisk); begræns kun MIME-typen.
            e.Property(x => x.ImageContentType).HasMaxLength(100);
            e.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.HouseholdId);
        });

        b.Entity<RecipeIngredient>(e =>
        {
            e.Property(x => x.Quantity).HasPrecision(10, 3);
            e.Property(x => x.Unit).IsRequired().HasMaxLength(Units.MaxLength);
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
            e.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.HouseholdId);
        });

        b.Entity<ItemGroupIngredient>(e =>
        {
            e.Property(x => x.Quantity).HasPrecision(10, 3);
            e.Property(x => x.Unit).IsRequired().HasMaxLength(Units.MaxLength);
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
            // Én plan pr. (husstand, år, ugenummer) — så to husstande kan have samme uge.
            e.HasIndex(x => new { x.HouseholdId, x.Year, x.WeekNumber }).IsUnique();
            e.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
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
            e.Property(x => x.Unit).IsRequired().HasMaxLength(Units.MaxLength);
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

        b.Entity<CatalogRecipe>(e =>
        {
            e.Property(x => x.Title).IsRequired().HasMaxLength(150);
            e.Property(x => x.Note).HasMaxLength(1000);
            e.Property(x => x.Tags).HasMaxLength(300);
            e.Property(x => x.ImageContentType).HasMaxLength(100);
            // Community-publicerede opskrifter: fjernes automatisk hvis kilde-husstanden
            // eller kilde-opskriften slettes (snapshot følger kilden ud).
            e.HasOne<Household>().WithMany().HasForeignKey(x => x.SourceHouseholdId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Recipe>().WithMany().HasForeignKey(x => x.SourceRecipeId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.SourceRecipeId).IsUnique(); // én katalog-kopi pr. kilde-opskrift
        });

        b.Entity<CatalogRecipeIngredient>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(150);
            e.Property(x => x.Quantity).HasPrecision(10, 3);
            e.Property(x => x.Unit).IsRequired().HasMaxLength(Units.MaxLength);
            e.HasOne(x => x.CatalogRecipe)
                .WithMany(r => r.Ingredients)
                .HasForeignKey(x => x.CatalogRecipeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<WeekShareToken>(e =>
        {
            e.Property(x => x.Token).IsRequired().HasMaxLength(64);
            e.HasIndex(x => x.Token).IsUnique();
            e.HasOne(x => x.Week)
                .WithMany()
                .HasForeignKey(x => x.WeekId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RecipeShare>(e =>
        {
            // Én deling pr. (opskrift, modtager-husstand) — gentagen deling er idempotent.
            e.HasIndex(x => new { x.RecipeId, x.TargetHouseholdId }).IsUnique();
            e.HasIndex(x => x.TargetHouseholdId); // hurtigt opslag af "delt med mig"
            // Slettes opskriften, ryger dens delinger med (cascade).
            e.HasOne(x => x.Recipe).WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
            // Modtager-husstanden: Restrict (som Category/Ingredient) — husstands-sletning rydder
            // eksplicit op i HouseholdEraser, så vi undgår flere cascade-stier til Household.
            e.HasOne<Household>().WithMany().HasForeignKey(x => x.TargetHouseholdId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<HouseholdTask>(e =>
        {
            e.Property(x => x.Title).IsRequired().HasMaxLength(200);
            e.Property(x => x.Assignees).HasMaxLength(200);
            e.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.HouseholdId);
        });

        b.Entity<Order>(e =>
        {
            e.Property(x => x.HouseholdName).IsRequired().HasMaxLength(150);
            e.Property(x => x.StoreName).IsRequired().HasMaxLength(100);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.HouseholdId);
            e.HasIndex(x => x.StoreName);
        });

        b.Entity<OrderLine>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(150);
            e.Property(x => x.Quantity).HasPrecision(10, 3);
            e.Property(x => x.Unit).IsRequired().HasMaxLength(Units.MaxLength);
            e.Property(x => x.CategoryName).HasMaxLength(100);
            e.HasOne(x => x.Order).WithMany(o => o.Lines).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
