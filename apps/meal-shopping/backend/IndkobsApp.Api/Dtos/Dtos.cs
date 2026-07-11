using IndkobsApp.Api.Models;

namespace IndkobsApp.Api.Dtos;

// ---------- Kategorier ----------
public record CategoryDto(int Id, string Name, int SortOrder);

// ---------- Ingredienser ----------
public record IngredientDto(int Id, string Name, int? CategoryId, string? CategoryName);
public record IngredientUpsertDto(string Name, int? CategoryId);

// En ingredienslinje som klienten sender (bruges af både retter og varegrupper).
// IngredientName matches/oprettes normaliseret; IngredientId er valgfri genvej.
public record IngredientLineInputDto(int? IngredientId, string IngredientName, decimal Quantity, Unit Unit);
public record IngredientLineDto(int Id, int IngredientId, string IngredientName, string? CategoryName, decimal Quantity, Unit Unit);

// ---------- Retter ----------
public record RecipeDto(int Id, string Name, string? Note, int Servings, List<IngredientLineDto> Ingredients);
public record RecipeUpsertDto(string Name, string? Note, int Servings, List<IngredientLineInputDto> Ingredients);

// ---------- Varegrupper ----------
public record ItemGroupDto(int Id, string Name, List<IngredientLineDto> Ingredients);
public record ItemGroupUpsertDto(string Name, List<IngredientLineInputDto> Ingredients);

// ---------- Uger ----------
public record WeekDto(int Id, int Year, int WeekNumber);
public record WeekCreateDto(int Year, int WeekNumber);

public record WeekRecipeDto(int Id, int RecipeId, string RecipeName, int BaseServings, int? Servings, int? DayOfWeek);
public record WeekItemGroupDto(int Id, int ItemGroupId, string ItemGroupName);
public record WeekManualItemDto(int Id, int? IngredientId, string Name, decimal Quantity, Unit Unit);

public record WeekDetailDto(
    int Id, int Year, int WeekNumber,
    List<WeekRecipeDto> Recipes,
    List<WeekItemGroupDto> ItemGroups,
    List<WeekManualItemDto> ManualItems);

public record AddWeekRecipeDto(int RecipeId, int? Servings, int? DayOfWeek);
public record UpdateWeekRecipeDto(int? Servings, int? DayOfWeek);
public record AddWeekItemGroupDto(int ItemGroupId);
public record AddWeekManualItemDto(int? IngredientId, string? FreeText, decimal Quantity, Unit Unit);

// ---------- Indkøbsliste ----------
public record ShoppingLineDto(
    string LineKey,
    int? IngredientId,
    string Name,
    decimal Quantity,
    Unit Unit,
    bool IsChecked,
    bool IsManual,
    List<string> Sources);

public record ShoppingCategoryGroupDto(int? CategoryId, string CategoryName, int SortOrder, List<ShoppingLineDto> Lines);

public record ShoppingListDto(int WeekId, int Year, int WeekNumber, List<ShoppingCategoryGroupDto> Groups);

public record CheckLineDto(string LineKey, bool IsChecked);

// ---------- Auth / husstande ----------
public record LoginDto(string Email, string Password);
public record AuthResultDto(string Token, string ExpiresUtc, int HouseholdId, string HouseholdName);
public record MeDto(int HouseholdId, string HouseholdName, string Email);
public record CreateHouseholdDto(string Name, string Email, string Password);
public record HouseholdDto(int Id, string Name, string Email);
