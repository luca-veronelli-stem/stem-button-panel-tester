using System.Diagnostics.CodeAnalysis;

namespace Core.Results;

/// <summary>
/// Represents the result of an operation that can either succeed with a value or fail with an error.
/// This is a discriminated union type that forces explicit handling of both success and failure cases.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
public readonly struct Result<T> : IEquatable<Result<T>>
{
    private readonly T? _value;
    private readonly Error? _error;

    /// <summary>
    /// Gets the success value. Throws if the result is a failure.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing Value on a failed result.</exception>
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"Cannot access Value on a failed Result. Error: {_error}");

    /// <summary>
    /// Gets the error. Throws if the result is a success.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing Error on a successful result.</exception>
    public Error Error => IsFailure ? _error!.Value : throw new InvalidOperationException("Cannot access Error on a successful Result.");

    /// <summary>
    /// Indicates whether the operation succeeded.
    /// </summary>
    [MemberNotNullWhen(true, nameof(_value))]
    public bool IsSuccess { get; }

    /// <summary>
    /// Indicates whether the operation failed.
    /// </summary>
    [MemberNotNullWhen(true, nameof(_error))]
    public bool IsFailure => !IsSuccess;

    private Result(T value)
    {
        _value = value;
        _error = null;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        _value = default;
        _error = error;
        IsSuccess = false;
    }

    /// <summary>
    /// Creates a successful result with the specified value.
    /// </summary>
    public static Result<T> Success(T value) => new(value);

    /// <summary>
    /// Creates a failed result with the specified error.
    /// </summary>
    public static Result<T> Failure(Error error) => new(error);

    /// <summary>
    /// Creates a failed result with code and message.
    /// </summary>
    public static Result<T> Failure(string code, string message) =>
        new(Error.Create(code, message));

    /// <summary>
    /// Creates a failed result from an exception.
    /// </summary>
    public static Result<T> Failure(Exception exception, string? code = null) =>
        new(Error.FromException(exception, code));

    /// <summary>
    /// Pattern matches on the result, executing the appropriate function.
    /// </summary>
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<Error, TResult> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(_error!.Value);

    /// <summary>
    /// Pattern matches on the result, executing the appropriate action.
    /// </summary>
    public void Match(
        Action<T> onSuccess,
        Action<Error> onFailure)
    {
        if (IsSuccess)
            onSuccess(_value!);
        else
            onFailure(_error!.Value);
    }

    /// <summary>
    /// Returns the value if successful, otherwise returns the default value.
    /// </summary>
    public T GetValueOrDefault(T defaultValue = default!) => IsSuccess ? _value! : defaultValue;

    /// <summary>
    /// Tries to get the value. Returns true if successful.
    /// </summary>
    public bool TryGetValue([NotNullWhen(true)] out T? value)
    {
        value = _value;
        return IsSuccess;
    }

    /// <summary>
    /// Tries to get the error. Returns true if failed.
    /// </summary>
    public bool TryGetError(out Error error)
    {
        error = _error ?? default;
        return IsFailure;
    }

    /// <summary>
    /// Transforms the success value using the specified function.
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper) =>
        IsSuccess ? Result<TNew>.Success(mapper(_value!)) : Result<TNew>.Failure(_error!.Value);

    /// <summary>
    /// Chains another result-producing operation.
    /// </summary>
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder) =>
        IsSuccess ? binder(_value!) : Result<TNew>.Failure(_error!.Value);

    /// <summary>
    /// Chains an async result-producing operation.
    /// </summary>
    public async Task<Result<TNew>> BindAsync<TNew>(Func<T, Task<Result<TNew>>> binder) =>
        IsSuccess ? await binder(_value!).ConfigureAwait(false) : Result<TNew>.Failure(_error!.Value);

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (IsSuccess)
            action(_value!);
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    public Result<T> OnFailure(Action<Error> action)
    {
        if (IsFailure)
            action(_error!.Value);
        return this;
    }

    /// <summary>
    /// Implicit conversion from value to successful result.
    /// </summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>
    /// Implicit conversion from error to failed result.
    /// </summary>
    public static implicit operator Result<T>(Error error) => Failure(error);

    public bool Equals(Result<T> other) =>
        IsSuccess == other.IsSuccess &&
        (IsSuccess ? EqualityComparer<T>.Default.Equals(_value, other._value) : _error.Equals(other._error));

    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

    public override int GetHashCode() =>
        IsSuccess ? HashCode.Combine(true, _value) : HashCode.Combine(false, _error);

    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);
    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

    public override string ToString() =>
        IsSuccess ? $"Success({_value})" : $"Failure({_error})";
}

/// <summary>
/// Represents the result of an operation that can succeed or fail without returning a value.
/// </summary>
public readonly struct Result : IEquatable<Result>
{
    private readonly Error? _error;

    /// <summary>
    /// Gets the error. Throws if the result is a success.
    /// </summary>
    public Error Error => IsFailure ? _error!.Value : throw new InvalidOperationException("Cannot access Error on a successful Result.");

    /// <summary>
    /// Indicates whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Indicates whether the operation failed.
    /// </summary>
    [MemberNotNullWhen(true, nameof(_error))]
    public bool IsFailure => !IsSuccess;

    private Result(bool isSuccess, Error? error = null)
    {
        IsSuccess = isSuccess;
        _error = error;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Success() => new(true);

    /// <summary>
    /// Creates a failed result with the specified error.
    /// </summary>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>
    /// Creates a failed result with code and message.
    /// </summary>
    public static Result Failure(string code, string message) =>
        new(false, Error.Create(code, message));

    /// <summary>
    /// Creates a failed result from an exception.
    /// </summary>
    public static Result Failure(Exception exception, string? code = null) =>
        new(false, Error.FromException(exception, code));

    /// <summary>
    /// Pattern matches on the result, executing the appropriate function.
    /// </summary>
    public TResult Match<TResult>(
        Func<TResult> onSuccess,
        Func<Error, TResult> onFailure) =>
        IsSuccess ? onSuccess() : onFailure(_error!.Value);

    /// <summary>
    /// Pattern matches on the result, executing the appropriate action.
    /// </summary>
    public void Match(
        Action onSuccess,
        Action<Error> onFailure)
    {
        if (IsSuccess)
            onSuccess();
        else
            onFailure(_error!.Value);
    }

    /// <summary>
    /// Tries to get the error. Returns true if failed.
    /// </summary>
    public bool TryGetError(out Error error)
    {
        error = _error ?? default;
        return IsFailure;
    }

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    public Result OnSuccess(Action action)
    {
        if (IsSuccess)
            action();
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    public Result OnFailure(Action<Error> action)
    {
        if (IsFailure)
            action(_error!.Value);
        return this;
    }

    /// <summary>
    /// Implicit conversion from error to failed result.
    /// </summary>
    public static implicit operator Result(Error error) => Failure(error);

    public bool Equals(Result other) =>
        IsSuccess == other.IsSuccess && (!IsFailure || _error.Equals(other._error));

    public override bool Equals(object? obj) => obj is Result other && Equals(other);

    public override int GetHashCode() =>
        IsSuccess ? HashCode.Combine(true) : HashCode.Combine(false, _error);

    public static bool operator ==(Result left, Result right) => left.Equals(right);
    public static bool operator !=(Result left, Result right) => !left.Equals(right);

    public override string ToString() =>
        IsSuccess ? "Success" : $"Failure({_error})";
}
