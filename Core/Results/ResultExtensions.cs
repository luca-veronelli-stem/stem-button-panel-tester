namespace Core.Results;

/// <summary>
/// Extension methods for working with Result types.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Combines multiple results into a single result. Fails if any result fails.
    /// </summary>
    public static Result Combine(params Result[] results)
    {
        foreach (var result in results)
        {
            if (result.IsFailure)
                return result;
        }
        return Result.Success();
    }

    /// <summary>
    /// Combines multiple results into a single result with all values. Fails if any result fails.
    /// </summary>
    public static Result<T[]> Combine<T>(params Result<T>[] results)
    {
        var values = new T[results.Length];
        for (int i = 0; i < results.Length; i++)
        {
            if (results[i].IsFailure)
                return Result<T[]>.Failure(results[i].Error);
            values[i] = results[i].Value;
        }
        return Result<T[]>.Success(values);
    }

    /// <summary>
    /// Wraps a function that may throw in a Result.
    /// </summary>
    public static Result<T> Try<T>(Func<T> func, string? errorCode = null)
    {
        try
        {
            return Result<T>.Success(func());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(ex, errorCode);
        }
    }

    /// <summary>
    /// Wraps an async function that may throw in a Result.
    /// </summary>
    public static async Task<Result<T>> TryAsync<T>(Func<Task<T>> func, string? errorCode = null)
    {
        try
        {
            return Result<T>.Success(await func().ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            return Result<T>.Failure(ErrorCodes.Cancelled, "Operation was cancelled.");
        }
        catch (TimeoutException ex)
        {
            return Result<T>.Failure(ex, ErrorCodes.Timeout);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(ex, errorCode);
        }
    }

    /// <summary>
    /// Wraps an action that may throw in a Result.
    /// </summary>
    public static Result Try(Action action, string? errorCode = null)
    {
        try
        {
            action();
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex, errorCode);
        }
    }

    /// <summary>
    /// Wraps an async action that may throw in a Result.
    /// </summary>
    public static async Task<Result> TryAsync(Func<Task> func, string? errorCode = null)
    {
        try
        {
            await func().ConfigureAwait(false);
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            return Result.Failure(ErrorCodes.Cancelled, "Operation was cancelled.");
        }
        catch (TimeoutException ex)
        {
            return Result.Failure(ex, ErrorCodes.Timeout);
        }
        catch (Exception ex)
        {
            return Result.Failure(ex, errorCode);
        }
    }

    /// <summary>
    /// Converts a Result to Result{T} with a specified value on success.
    /// </summary>
    public static Result<T> ToResult<T>(this Result result, T value) =>
        result.IsSuccess ? Result<T>.Success(value) : Result<T>.Failure(result.Error);

    /// <summary>
    /// Converts a Result{T} to Result, discarding the value.
    /// </summary>
    public static Result ToResult<T>(this Result<T> result) =>
        result.IsSuccess ? Result.Success() : Result.Failure(result.Error);

    /// <summary>
    /// Ensures a condition is met on the result value.
    /// </summary>
    public static Result<T> Ensure<T>(
        this Result<T> result,
        Func<T, bool> predicate,
        string errorCode,
        string errorMessage)
    {
        if (result.IsFailure)
            return result;

        return predicate(result.Value)
            ? result
            : Result<T>.Failure(errorCode, errorMessage);
    }

    /// <summary>
    /// Ensures a condition is met on the result value.
    /// </summary>
    public static Result<T> Ensure<T>(
        this Result<T> result,
        Func<T, bool> predicate,
        Error error)
    {
        if (result.IsFailure)
            return result;

        return predicate(result.Value) ? result : Result<T>.Failure(error);
    }

    /// <summary>
    /// Taps into a successful result to perform a side effect.
    /// </summary>
    public static async Task<Result<T>> TapAsync<T>(
        this Result<T> result,
        Func<T, Task> action)
    {
        if (result.IsSuccess)
            await action(result.Value).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Taps into a successful result to perform a side effect.
    /// </summary>
    public static async Task<Result> TapAsync(
        this Result result,
        Func<Task> action)
    {
        if (result.IsSuccess)
            await action().ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Converts a nullable value to a Result.
    /// </summary>
    public static Result<T> ToResult<T>(this T? value, string errorCode, string errorMessage) where T : class =>
        value is not null ? Result<T>.Success(value) : Result<T>.Failure(errorCode, errorMessage);

    /// <summary>
    /// Converts a nullable value to a Result.
    /// </summary>
    public static Result<T> ToResult<T>(this T? value, string errorCode, string errorMessage) where T : struct =>
        value.HasValue ? Result<T>.Success(value.Value) : Result<T>.Failure(errorCode, errorMessage);

    /// <summary>
    /// Awaits a Task{Result{T}} and binds another async operation.
    /// </summary>
    public static async Task<Result<TNew>> BindAsync<T, TNew>(
        this Task<Result<T>> resultTask,
        Func<T, Task<Result<TNew>>> binder)
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.BindAsync(binder).ConfigureAwait(false);
    }

    /// <summary>
    /// Awaits a Task{Result{T}} and maps the value.
    /// </summary>
    public static async Task<Result<TNew>> MapAsync<T, TNew>(
        this Task<Result<T>> resultTask,
        Func<T, TNew> mapper)
    {
        var result = await resultTask.ConfigureAwait(false);
        return result.Map(mapper);
    }

    /// <summary>
    /// Awaits a Task{Result{T}} and matches on success/failure.
    /// </summary>
    public static async Task<TResult> MatchAsync<T, TResult>(
        this Task<Result<T>> resultTask,
        Func<T, TResult> onSuccess,
        Func<Error, TResult> onFailure)
    {
        var result = await resultTask.ConfigureAwait(false);
        return result.Match(onSuccess, onFailure);
    }
}
