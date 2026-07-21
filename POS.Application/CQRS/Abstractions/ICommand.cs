namespace POS.Application.CQRS.Abstractions;

/// <summary>
/// Marker interface for a command that returns no result.
/// </summary>
public interface ICommand { }

/// <summary>
/// A command that returns a result of type <typeparamref name="TResult"/>.
/// </summary>
public interface ICommand<TResult> : ICommand { }
