namespace POS.Application.Services;

/// <summary>
/// Service for managing product recipes and their ingredients.
/// </summary>
public interface IRecipeService
{
    /// <summary>
    /// Gets the recipe for a specific product.
    /// </summary>
    Task<RecipeDto?> GetRecipeByProductAsync(Guid productId);

    /// <summary>
    /// Creates or updates a recipe for a product.
    /// </summary>
    Task<RecipeDto> SaveRecipeAsync(Guid productId, string name, string? instructions, List<RecipeIngredientDto> ingredients);

    /// <summary>
    /// Deletes a recipe.
    /// </summary>
    Task DeleteRecipeAsync(Guid recipeId);

    /// <summary>
    /// Calculates the total cost of a recipe based on its ingredients' current costs.
    /// </summary>
    Task<decimal> CalculateRecipeCostAsync(Guid recipeId);
}

public record RecipeDto(Guid Id, Guid ProductId, string Name, string? Instructions, List<RecipeIngredientDto> Ingredients, decimal TotalCost);
public record RecipeIngredientDto(Guid InventoryItemId, string ItemName, decimal Quantity, string Unit);
