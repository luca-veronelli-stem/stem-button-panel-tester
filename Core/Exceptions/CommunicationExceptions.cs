using Core.Results;

namespace Core.Exceptions;

/// <summary>
/// Exception thrown when a communication error occurs.
/// Contains structured error information for better error handling.
/// </summary>
public class CommunicationException : Exception
{
    /// <summary>
    /// The error code associated with this exception.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Additional details about the error.
    /// </summary>
    public string? Details { get; }

    public CommunicationException(string message)
        : base(message)
    {
        ErrorCode = ErrorCodes.UnexpectedError;
    }

    public CommunicationException(string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = ErrorCodes.UnexpectedError;
    }

    public CommunicationException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public CommunicationException(string errorCode, string message, string? details)
        : base(message)
    {
        ErrorCode = errorCode;
        Details = details;
    }

    public CommunicationException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Creates a CommunicationException from an Error.
    /// </summary>
    public static CommunicationException FromError(Error error) =>
        error.Exception is not null
            ? new CommunicationException(error.Code, error.Message, error.Exception)
            : new CommunicationException(error.Code, error.Message, error.Details);

    /// <summary>
    /// Converts this exception to an Error.
    /// </summary>
    public Error ToError() =>
        Error.Create(ErrorCode, Message, Details ?? InnerException?.Message);
}

/// <summary>
/// Exception thrown when a connection cannot be established.
/// </summary>
public class ConnectionException : CommunicationException
{
    public ConnectionException(string message)
        : base(ErrorCodes.ConnectionFailed, message)
    {
    }

    public ConnectionException(string message, Exception innerException)
        : base(ErrorCodes.ConnectionFailed, message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when no active communication channel is available.
/// </summary>
public class NoActiveChannelException : CommunicationException
{
    public NoActiveChannelException()
        : base(ErrorCodes.NoActiveChannel, "No active communication channel is configured.")
    {
    }

    public NoActiveChannelException(string message)
        : base(ErrorCodes.NoActiveChannel, message)
    {
    }
}

/// <summary>
/// Exception thrown when the communication channel is not connected.
/// </summary>
public class ChannelNotConnectedException : CommunicationException
{
    public ChannelNotConnectedException()
        : base(ErrorCodes.ChannelNotConnected, "The communication channel is not connected.")
    {
    }

    public ChannelNotConnectedException(string message)
        : base(ErrorCodes.ChannelNotConnected, message)
    {
    }
}

/// <summary>
/// Exception thrown when sending data fails.
/// </summary>
public class SendFailedException : CommunicationException
{
    public SendFailedException(string message)
        : base(ErrorCodes.SendFailed, message)
    {
    }

    public SendFailedException(string message, Exception innerException)
        : base(ErrorCodes.SendFailed, message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a protocol error occurs.
/// </summary>
public class ProtocolException : CommunicationException
{
    public ProtocolException(string message)
        : base(ErrorCodes.ProtocolError, message)
    {
    }

    public ProtocolException(string message, Exception innerException)
        : base(ErrorCodes.ProtocolError, message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a device-related error occurs.
/// </summary>
public class DeviceException : Exception
{
    /// <summary>
    /// The error code associated with this exception.
    /// </summary>
    public string ErrorCode { get; }

    public DeviceException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public DeviceException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Converts this exception to an Error.
    /// </summary>
    public Error ToError() =>
        Error.Create(ErrorCode, Message, InnerException?.Message);
}
