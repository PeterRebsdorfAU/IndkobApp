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
// IsPublic = opskriften er publiceret til den fælles inspirationsside.
public record RecipeDto(int Id, string Name, string? Note, int Servings, List<IngredientLineDto> Ingredients, bool IsPublic = false);
public record RecipeUpsertDto(string Name, string? Note, int Servings, List<IngredientLineInputDto> Ingredients);

// ---------- Varegrupper ----------
public record ItemGroupDto(int Id, string Name, List<IngredientLineDto> Ingredients);
public record ItemGroupUpsertDto(string Name, List<IngredientLineInputDto> Ingredients);

// ---------- Uger ----------
public record WeekDto(int Id, int Year, int WeekNumber);
public record WeekCreateDto(int Year, int WeekNumber);

// CookedUtc sat = retten er markeret "lavet" (ingredienser trukket fra lageret).
public record WeekRecipeDto(int Id, int RecipeId, string RecipeName, int BaseServings, int? Servings, int? DayOfWeek, DateTime? CookedUtc = null);
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
// Quantity = det der SKAL KØBES (behov minus lager). OnHand* viser hvad husstanden
// allerede har hjemme af varen (null hvis intet). Quantity 0 = fuldt dækket af lager.
public record ShoppingLineDto(
    string LineKey,
    int? IngredientId,
    string Name,
    decimal Quantity,
    Unit Unit,
    bool IsChecked,
    bool IsManual,
    List<string> Sources,
    decimal? OnHandQuantity = null,
    Unit? OnHandUnit = null);

public record ShoppingCategoryGroupDto(int? CategoryId, string CategoryName, int SortOrder, List<ShoppingLineDto> Lines);

public record ShoppingListDto(int WeekId, int Year, int WeekNumber, List<ShoppingCategoryGroupDto> Groups);

public record CheckLineDto(string LineKey, bool IsChecked);

// ---------- Auth / husstande ----------
public record LoginDto(string Email, string Password);
public record AuthResultDto(string Token, string ExpiresUtc, int HouseholdId, string HouseholdName);
public record MeDto(int HouseholdId, string HouseholdName, string Email);
public record CreateHouseholdDto(string Name, string Email, string Password);
public record HouseholdDto(int Id, string Name, string Email);

// ---------- Inspiration / katalog ----------
public record CatalogLineDto(string Name, decimal Quantity, Unit Unit);
// SharedBy = navnet på husstanden der har delt opskriften (null for kuraterede).
public record CatalogRecipeDto(int Id, string Title, string? Note, int Servings, List<string> Tags, List<CatalogLineDto> Ingredients, string? SharedBy = null);
// Adoptér en katalog-opskrift: kopiér til egne opskrifter og læg evt. på en uge med det samme.
public record AdoptCatalogRecipeDto(int? WeekId, int? Servings, int? DayOfWeek);
public record AdoptResultDto(int RecipeId, string RecipeName, int? WeekId);

// ---------- Køkkenlager (pantry) ----------
public record PantryItemDto(int Id, int IngredientId, string IngredientName, string? CategoryName, decimal Quantity, Unit Unit);
public record PantryUpsertDto(int? IngredientId, string? IngredientName, decimal Quantity, Unit Unit);
public record PantryUpdateDto(decimal Quantity, Unit Unit);

// ---------- Deling af indkøbsliste ----------
public record ShareTokenDto(string Token);

// ---------- Lager-kredsløb ----------
public record StockCheckedResultDto(int LinesStocked);

// ---------- Hjemmets opgaver ----------
// IntervalDays null = engangsopgave. CurrentAssignee = hvis tur det er (fra rotation).
public record HouseholdTaskDto(
    int Id, string Title, int? IntervalDays, DateOnly? NextDueDate,
    List<string> Assignees, string? CurrentAssignee, bool IsDone, DateTime? LastCompletedUtc);
public record HouseholdTaskUpsertDto(string Title, int? IntervalDays, List<string>? Assignees);
public record TasksSummaryDto(int Overdue, int OpenTodos);
