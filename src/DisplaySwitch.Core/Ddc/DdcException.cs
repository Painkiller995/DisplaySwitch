namespace DisplaySwitch.Core.Ddc;

/// <summary>Why a DDC/CI operation failed. Drives retry decisions and the wording shown to users.</summary>
public enum DdcError
{
    /// <summary>The monitor did not answer. Often transient, and expected right after an input switch.</summary>
    NoResponse,

    /// <summary>The reply was corrupt or did not match the request. Usually transient on noisy links.</summary>
    InvalidReply,

    /// <summary>The monitor explicitly rejected or does not implement the feature.</summary>
    Unsupported,

    /// <summary>The operation did not finish within its time limit.</summary>
    Timeout,

    /// <summary>The operating system refused access, for example missing i2c permissions on Linux.</summary>
    AccessDenied,

    /// <summary>The monitor handle is no longer valid, typically after a display was disconnected.</summary>
    Disconnected,
}

/// <summary>A failed DDC/CI exchange with a monitor.</summary>
public sealed class DdcException : Exception
{
    public DdcException(DdcError error, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Error = error;
    }

    public DdcError Error { get; }

    /// <summary>Whether repeating the same request has a reasonable chance of succeeding.</summary>
    public bool IsTransient => Error is DdcError.NoResponse or DdcError.InvalidReply or DdcError.Timeout;
}
