using System.Linq.Expressions;
using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

/// <summary>
/// Generic repository interface for entities that inherit from <see cref="BaseEntity"/>.
/// Supports CRUD operations, soft delete, pagination, and querying.
/// </summary>
/// <typeparam name="T">The entity type, must inherit from <see cref="BaseEntity"/>.</typeparam>
public interface IRepository<T> where T : BaseEntity
{
    /// <summary>Gets an entity by its ID. Returns null if not found or soft-deleted.</summary>
    Task<T?> GetByIdAsync(Guid id);

    /// <summary>Gets all non-deleted entities.</summary>
    Task<IReadOnlyList<T>> GetAllAsync();

    /// <summary>Adds a new entity to the repository.</summary>
    Task AddAsync(T entity);

    /// <summary>Updates an existing entity in the repository.</summary>
    Task UpdateAsync(T entity);

    /// <summary>Soft-deletes an entity by marking it as deleted.</summary>
    Task DeleteAsync(T entity);

    /// <summary>Gets a paged list of non-deleted entities.</summary>
    /// <param name="pageNumber">1-based page number.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <returns>A tuple containing the page items and the total count.</returns>
    Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize);

    /// <summary>Finds entities matching the specified predicate. Excludes soft-deleted entities.</summary>
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate);
}