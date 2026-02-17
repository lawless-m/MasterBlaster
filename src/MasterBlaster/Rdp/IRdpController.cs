namespace MasterBlaster.Rdp;

/// <summary>
/// Abstraction over an RDP connection that supports screen capture, mouse input, and keyboard input.
/// Implementations manage the lifecycle of the underlying RDP client (connect, disconnect, dispose).
/// </summary>
public interface IRdpController : IAsyncDisposable
{
    /// <summary>
    /// Establishes an RDP connection using the specified configuration.
    /// </summary>
    /// <param name="config">Connection parameters including server, credentials, and display settings.</param>
    /// <param name="ct">Cancellation token to abort the connection attempt.</param>
    Task ConnectAsync(RdpConnectionConfig config, CancellationToken ct = default);

    /// <summary>
    /// Gracefully disconnects the active RDP session.
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Gets a value indicating whether an RDP session is currently active.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Captures a screenshot of the current RDP session as a PNG-encoded byte array.
    /// </summary>
    /// <returns>PNG image bytes of the remote desktop.</returns>
    Task<byte[]> CaptureScreenshotAsync();

    /// <summary>
    /// Performs a single left-click at the specified screen coordinates.
    /// </summary>
    Task ClickAsync(int x, int y);

    /// <summary>
    /// Performs a double left-click at the specified screen coordinates.
    /// </summary>
    Task DoubleClickAsync(int x, int y);

    /// <summary>
    /// Performs a right-click at the specified screen coordinates.
    /// </summary>
    Task RightClickAsync(int x, int y);

    /// <summary>
    /// Sends a sequence of keystrokes as text input to the remote session.
    /// </summary>
    /// <param name="text">The text to type.</param>
    Task SendKeysAsync(string text);

    /// <summary>
    /// Sends a key combination (e.g., "ctrl+c", "alt+tab") to the remote session.
    /// </summary>
    /// <param name="combo">A key combination string using '+' as a separator.</param>
    Task SendKeyComboAsync(string combo);

    /// <summary>
    /// Raised when the RDP session is unexpectedly disconnected.
    /// </summary>
    event EventHandler? Disconnected;

    /// <summary>
    /// Raised when the RDP session is successfully connected.
    /// </summary>
    event EventHandler? Connected;
}

/// <summary>
/// Immutable configuration record for establishing an RDP connection.
/// </summary>
public record RdpConnectionConfig
{
    public string Server { get; init; } = "";
    public int Port { get; init; } = 3389;
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public string Domain { get; init; } = "";
    public int Width { get; init; } = 1920;
    public int Height { get; init; } = 1080;
    public int ColorDepth { get; init; } = 32;
}
