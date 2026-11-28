namespace OneGov.Domain.Common;

/// <summary>A domain error with a stable machine-readable code.</summary>
/// <param name="Details">Optional per-field problems, for example a form field and the reason it was refused.</param>
public sealed record Error(string Code, string Message, IReadOnlyDictionary<string, string>? Details = null);

/// <summary>Outcome of an operation that can fail for expected business reasons.</summary>
public sealed class Result<T>
{
    private readonly T? _value;

    private Result(T? value, Error? error)
    {
        _value = value;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public Error? Error { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Result failed: {Error!.Code}");

    public static Result<T> Success(T value) => new(value, null);

    public static Result<T> Failure(Error error) => new(default, error);

    public static Result<T> Failure(string code, string message) => new(default, new Error(code, message));
}
