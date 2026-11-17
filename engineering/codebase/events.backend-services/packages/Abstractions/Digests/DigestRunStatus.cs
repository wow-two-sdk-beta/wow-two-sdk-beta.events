namespace WoW.Two.Sdk.Beta.Be.Events.Digests;

/// <summary>Says where a digest run stands.</summary>
public enum DigestRunStatus
{
    /// <summary>Messages are being sent, or wait for a retry.</summary>
    Running,

    /// <summary>Every recipient has a final result and none failed.</summary>
    Completed,

    /// <summary>Some recipients failed, others did not.</summary>
    Partial,

    /// <summary>Every recipient failed, or the run could not start (no channel, no recipient).</summary>
    Failed,

    /// <summary>No event matched; nothing went out.</summary>
    Empty,

    /// <summary>The occurrence passed beyond the catch-up window while the host was down; nothing went out.</summary>
    Missed,
}
