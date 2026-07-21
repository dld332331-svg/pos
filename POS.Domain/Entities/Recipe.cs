namespace POS.Domain.Entities;

public class Recipe : BaseEntity
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Instructions { get; set; }

    // Navigation
    public Product? Product { get; set; }
    private readonly List<RecipeIngredient> _ingredients = new();
    public IReadOnlyCollection<RecipeIngredient> Ingredients => _ingredients.AsReadOnly();
    public void AddIngredient(RecipeIngredient ingredient) => _ingredients.Add(ingredient);
    public void RemoveIngredient(RecipeIngredient ingredient) => _ingredients.Remove(ingredient);
}
