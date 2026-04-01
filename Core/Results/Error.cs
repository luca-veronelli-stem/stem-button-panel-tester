namespace Core.Results;

/// <summary>
/// Represents a structured error with code, message, and optional details.
/// Immutable value type for error representation.
/// </summary>
public readonly record struct Error
{
    /// <summary>
    /// Error code for programmatic handling.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Optional additional details or inner exception message.
    /// </summary>
    public string? Details { get; }

    /// <summary>
    /// Original exception if the error was created from one.
    /// </summary>
    public Exception? Exception { get; }

    private Error(string code, string message, string? details = null, Exception? exception = null)
    {
        Code = code;
        Message = message;
        Details = details;
        Exception = exception;
    }

    /// <summary>
    /// Creates an error with code and message.
    /// </summary>
    public static Error Create(string code, string message) => new(code, message);

    /// <summary>
    /// Creates an error with code, message, and details.
    /// </summary>
    public static Error Create(string code, string message, string details) => new(code, message, details);

    /// <summary>
    /// Creates an error from an exception.
    /// </summary>
    public static Error FromException(Exception exception, string? code = null) =>
        new(code ?? ErrorCodes.UnexpectedError, exception.Message, exception.InnerException?.Message, exception);

    /// <summary>
    /// Returns the error message with details if available.
    /// </summary>
    public override string ToString() =>
        string.IsNullOrEmpty(Details) ? $"[{Code}] {Message}" : $"[{Code}] {Message} - {Details}";
}

/// <summary>
/// Standard error codes used across the application.
/// </summary>
public static class ErrorCodes
{
    // General errors
    public const string UnexpectedError = "UNEXPECTED_ERROR";
    public const string ValidationError = "VALIDATION_ERROR";
    public const string InvalidOperation = "INVALID_OPERATION";
    public const string NotFound = "NOT_FOUND";
    public const string Timeout = "TIMEOUT";
    public const string Cancelled = "CANCELLED";

    // Communication errors
    public const string ConnectionFailed = "COMM_CONNECTION_FAILED";
    public const string Disconnected = "COMM_DISCONNECTED";
    public const string SendFailed = "COMM_SEND_FAILED";
    public const string ReceiveFailed = "COMM_RECEIVE_FAILED";
    public const string ProtocolError = "COMM_PROTOCOL_ERROR";
    public const string NoActiveChannel = "COMM_NO_ACTIVE_CHANNEL";
    public const string ChannelNotConnected = "COMM_CHANNEL_NOT_CONNECTED";

    // Device errors
    public const string DeviceNotFound = "DEVICE_NOT_FOUND";
    public const string DeviceNotResponding = "DEVICE_NOT_RESPONDING";
    public const string BaptizeFailed = "DEVICE_BAPTIZE_FAILED";

    // Test errors
    public const string TestAlreadyRunning = "TEST_ALREADY_RUNNING";
    public const string TestFailed = "TEST_FAILED";
    public const string TestInterrupted = "TEST_INTERRUPTED";

    // Data errors
    public const string DataNotFound = "DATA_NOT_FOUND";
    public const string DataInvalid = "DATA_INVALID";
    public const string FileNotFound = "FILE_NOT_FOUND";
}
