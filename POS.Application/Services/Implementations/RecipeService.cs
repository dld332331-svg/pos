
using POS.Domain.BusinessRules;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.Services.Implementations;

public class RecipeService : IRecipeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public RecipeService(IUnitOfWork unitOfWork, IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<RecipeDto?> GetRecipeByProductAsync(Guid productId)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        if (product is null) return null;

        var recipes = await _unitOfWork.Recipes.FindAsync(r => r.ProductId == productId);
        var recipe = recipes.FirstOrDefault();
        if (recipe is null) return null;

        return await MapToDtoAsync(recipe);
    }

    public async Task<RecipeDto> SaveRecipeAsync(Guid productId, string name, string? instructions, List<RecipeIngredientDto> ingredients)
    {
        ArgumentNullException.ThrowIfNull(ingredients);
        var product = await _unitOfWork.Products.GetByIdAsync(productId)
            ?? throw new InvalidOperationException("المنتج غير موجود");

        var existing = await _unitOfWork.Recipes.FindAsync(r => r.ProductId == productId);
        var recipe = existing.FirstOrDefault();

        if (recipe is null)
        {
            recipe = new Recipe
            {
                ProductId = productId,
                Name = name,
                Instructions = instructions
            };
            await _unitOfWork.Recipes.AddAsync(recipe);
        }
        else
        {
            recipe.Name = name;
            recipe.Instructions = instructions;
            recipe.MarkAsModified();

            // Remove existing ingredients
            var existingIngredients = await _unitOfWork.RecipeIngredients.FindAsync(ri => ri.RecipeId == recipe.Id);
            foreach (var ing in existingIngredients)
                await _unitOfWork.RecipeIngredients.DeleteAsync(ing);
        }

        await _unitOfWork.SaveChangesAsync();

        // Add new ingredients
        foreach (var ing in ingredients)
        {
            var inventoryItem = await _unitOfWork.InventoryItems.GetByIdAsync(ing.InventoryItemId);
            if (inventoryItem is null)
                throw new InvalidOperationException($"عنصر المخزون (ID: {ing.InventoryItemId}) غير موجود");

            var ingredient = new RecipeIngredient
            {
                RecipeId = recipe.Id,
                InventoryItemId = ing.InventoryItemId,
                Quantity = ing.Quantity,
                Unit = ing.Unit
            };
            await _unitOfWork.RecipeIngredients.AddAsync(ingredient);
        }

        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(null, AuditActionType.RecipeChanged, "Recipe", recipe.Id,
            null, $"ProductId={productId},Name={name}", null);

        return (await MapToDtoAsync(recipe))!;
    }

    public async Task DeleteRecipeAsync(Guid recipeId)
    {
        var recipe = await _unitOfWork.Recipes.GetByIdAsync(recipeId)
            ?? throw new InvalidOperationException("الوصفة غير موجودة");

        // Remove ingredients
        var ingredients = await _unitOfWork.RecipeIngredients.FindAsync(ri => ri.RecipeId == recipeId);
        foreach (var ing in ingredients)
            await _unitOfWork.RecipeIngredients.DeleteAsync(ing);

        await _unitOfWork.Recipes.DeleteAsync(recipe);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<decimal> CalculateRecipeCostAsync(Guid recipeId)
    {
        var recipe = await _unitOfWork.Recipes.GetByIdAsync(recipeId)
            ?? throw new InvalidOperationException("الوصفة غير موجودة");

        var ingredients = await _unitOfWork.RecipeIngredients.FindAsync(ri => ri.RecipeId == recipeId);
        var inventoryItems = await _unitOfWork.InventoryItems.GetAllAsync();
        var inventoryMap = inventoryItems.ToDictionary(i => i.Id);

        decimal totalCost = 0;
        foreach (var ing in ingredients)
        {
            if (inventoryMap.TryGetValue(ing.InventoryItemId, out var invItem))
            {
                totalCost += ing.Quantity * invItem.Cost;
            }
        }

        return MoneyPolicy.RoundToJOD(totalCost);
    }

    private async Task<RecipeDto> MapToDtoAsync(Recipe recipe)
    {
        var ingredients = await _unitOfWork.RecipeIngredients.FindAsync(ri => ri.RecipeId == recipe.Id);
        var inventoryItems = await _unitOfWork.InventoryItems.GetAllAsync();
        var inventoryMap = inventoryItems.ToDictionary(i => i.Id);

        var ingredientDtos = new List<RecipeIngredientDto>();
        decimal totalCost = 0;

        foreach (var ing in ingredients)
        {
            inventoryMap.TryGetValue(ing.InventoryItemId, out var invItem);
            var itemName = invItem?.Name ?? "Unknown";
            var itemCost = invItem?.Cost ?? 0;
            totalCost += ing.Quantity * itemCost;

            ingredientDtos.Add(new RecipeIngredientDto(
                ing.InventoryItemId,
                itemName,
                ing.Quantity,
                ing.Unit));
        }

        return new RecipeDto(
            recipe.Id,
            recipe.ProductId,
            recipe.Name,
            recipe.Instructions,
            ingredientDtos,
            MoneyPolicy.RoundToJOD(totalCost));
    }
}
