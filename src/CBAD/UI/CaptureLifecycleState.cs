namespace CBAD.UI;

/// <summary>Represents the connection/capture lifecycle state for status display.</summary>
internal enum CaptureLifecycleState
{
    Idle,
    Starting,
    Running,
    Stopping,
    Error,
}
