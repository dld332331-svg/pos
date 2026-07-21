namespace POS.Application.CQRS.Abstractions;

/// <summary>
/// Mediates the execution of commands and queries to their respective handlers.
/// Acts as the single entry point for all CQRS operations in the application.
/// </summary>
public interface IDispatcher
{
    /// <summary>
    /// Sends a command that returns no result.
    /// </summary>
    Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand;

    /// <summary>
    /// Sends a command and returns a result.
    /// </summary>
    Task<TResult> SendAsync<TCommand, TResult>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand<TResult>;

    /// <summary>
    /// Executes a query and returns the result.
    /// </summary>
    Task<TResult> QueryAsync<TQuery, TResult>(TQuery query, CancellationToken cancellationToken = default)
        where TQuery : IQuery<TResult>;
}
