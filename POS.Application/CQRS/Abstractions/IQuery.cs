namespace POS.Application.CQRS.Abstractions;

/// <summary>
/// A query that returns a result of type <typeparamref name="TResult"/>.
/// Queries are read-only operations that do not modify state.
/// </summary>
public interface IQuery<TResult> { }
