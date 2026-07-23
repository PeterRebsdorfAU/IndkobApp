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
// Method = valgfri fremgangsmåde (fritekst, evt. flere linjer). Null = ingen angivet.
public record RecipeDto(int Id, string Name, string? Note, int Servings, List<IngredientLineDto> Ingredients, string? Method = null, bool IsPublic = false);
public record RecipeUpsertDto(string Name, string? Note, int Servings, List<IngredientLineInputDto> Ingredients, string? Method = null);

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
// Quantity = det fulde behov for varen (aggregeret på tværs af ugens retter/varegrupper/løse varer).
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
// RefreshToken er additiv (default null): ældre klienter ignorerer feltet; refresh-bevidste
// klienter (T2's frontend) gemmer det og kalder POST /api/auth/refresh når access-token udløber.
// DisplayName/UserId (T2) er ligeledes additive: null for legacy husstands-login uden bruger.
public record AuthResultDto(string Token, string ExpiresUtc, int HouseholdId, string HouseholdName,
    string? RefreshToken = null, string? DisplayName = null, int? UserId = null);
public record RefreshDto(string RefreshToken);
public record MeDto(int HouseholdId, string HouseholdName, string Email, string? DisplayName = null, int? UserId = null);
public record CreateHouseholdDto(string Name, string Email, string Password);
public record HouseholdDto(int Id, string Name, string Email);

// ---------- T2: individuelle brugerkonti ----------
// Signup i to varianter:
//  - Ny husstand: udfyld HouseholdName (+ DisplayName/Email/Password). InviteToken = null.
//  - Join eksisterende husstand: udfyld InviteToken (fra et invitationslink). HouseholdName ignoreres.
public record SignupDto(string Email, string Password, string DisplayName, string? HouseholdName = null, string? InviteToken = null);
public record ForgotPasswordDto(string Email);
public record ResetPasswordDto(string Token, string NewPassword);
public record ConfirmEmailDto(string Token);
// Svar på POST /api/auth/invite: et token + et færdigt link den eksisterende bruger kan dele.
public record InviteResultDto(string InviteToken, string InviteLink);

// ---------- Inspiration / katalog ----------
public record CatalogLineDto(string Name, decimal Quantity, Unit Unit);
// SharedBy = navnet på husstanden der har delt opskriften (null for kuraterede).
// Method = valgfri fremgangsmåde (fritekst, evt. flere linjer). Null = ingen angivet.
public record CatalogRecipeDto(int Id, string Title, string? Note, int Servings, List<string> Tags, List<CatalogLineDto> Ingredients, string? Method = null, string? SharedBy = null);
// Adoptér en katalog-opskrift: kopiér til egne opskrifter og læg evt. på en uge med det samme.
public record AdoptCatalogRecipeDto(int? WeekId, int? Servings, int? DayOfWeek);
public record AdoptResultDto(int RecipeId, string RecipeName, int? WeekId);

// ---------- Deling af indkøbsliste ----------
public record ShareTokenDto(string Token);

// ---------- Ordrer (butiks-flow) ----------
public record StoreDto(string Name);
public record OrderLineDto(int Id, string Name, decimal Quantity, Unit Unit, string? CategoryName, bool IsPacked, bool NotAvailable);
public record OrderDto(int Id, string HouseholdName, string StoreName, string Status, string? Note,
    string CreatedUtc, string? ReadyUtc, List<OrderLineDto> Lines);
public record CreateOrderDto(string StoreName, string? Note);
public record PackLineDto(bool IsPacked, bool NotAvailable);

// ---------- GDPR: data-eksport & -sletning ----------
// Bekræftet sletning af egen husstand: brugeren gen-indtaster sin adgangskode.
public record DeleteAccountDto(string Password);

// Flad, cykel-fri eksport af ALT husstandens data (retten til dataportabilitet).
// Adgangskode-hash og andre hemmeligheder tages IKKE med.
public record DataExportDto(
    string ExportedUtc,
    ExportHouseholdDto Household,
    List<ExportCategoryDto> Categories,
    List<ExportIngredientDto> Ingredients,
    List<ExportRecipeDto> Recipes,
    List<ExportItemGroupDto> ItemGroups,
    List<ExportWeekDto> Weeks,
    List<ExportTaskDto> Tasks,
    List<ExportOrderDto> Orders,
    List<ExportPublishedRecipeDto> PublishedToCatalog);

public record ExportHouseholdDto(int Id, string Name, string Email, string CreatedUtc);
public record ExportCategoryDto(int Id, string Name, int SortOrder);
public record ExportIngredientDto(int Id, string Name, string? Category);
public record ExportLineDto(string Ingredient, decimal Quantity, string Unit);
public record ExportRecipeDto(int Id, string Name, string? Note, int Servings, bool PublishedToCatalog, List<ExportLineDto> Ingredients);
public record ExportItemGroupDto(int Id, string Name, List<ExportLineDto> Ingredients);
public record ExportWeekRecipeDto(string Recipe, int? Servings, int? DayOfWeek, string? CookedUtc);
public record ExportWeekItemGroupDto(string ItemGroup);
public record ExportWeekManualItemDto(string Name, decimal Quantity, string Unit);
public record ExportWeekCheckDto(string LineKey, bool IsChecked);
public record ExportWeekDto(
    int Id, int Year, int WeekNumber,
    List<ExportWeekRecipeDto> Recipes,
    List<ExportWeekItemGroupDto> ItemGroups,
    List<ExportWeekManualItemDto> ManualItems,
    List<ExportWeekCheckDto> Checks);
public record ExportTaskDto(int Id, string Title, int? IntervalDays, string? NextDueDate, string? Assignees, bool IsDone, string? LastCompletedUtc, string CreatedUtc);
public record ExportOrderLineDto(string Name, decimal Quantity, string Unit, string? Category, bool IsPacked, bool NotAvailable);
public record ExportOrderDto(int Id, string StoreName, string Status, string? Note, string CreatedUtc, string? ReadyUtc, List<ExportOrderLineDto> Lines);
public record ExportPublishedRecipeDto(int CatalogId, string Title, int? SourceRecipeId);

// ---------- Hjemmets opgaver ----------
// IntervalDays null = engangsopgave. CurrentAssignee = hvis tur det er (fra rotation).
public record HouseholdTaskDto(
    int Id, string Title, int? IntervalDays, DateOnly? NextDueDate,
    List<string> Assignees, string? CurrentAssignee, bool IsDone, DateTime? LastCompletedUtc);
public record HouseholdTaskUpsertDto(string Title, int? IntervalDays, List<string>? Assignees);
public record TasksSummaryDto(int Overdue, int OpenTodos);
