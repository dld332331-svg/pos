namespace POS.Application.CQRS.Abstractions;

/// <summary>
/// Handles a query and returns a result of type <typeparamref name="TResult"/>.
/// Query handlers should NOT modify state — only read data.
/// </summary>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
