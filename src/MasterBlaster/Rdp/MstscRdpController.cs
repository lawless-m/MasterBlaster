namespace MasterBlaster.Rdp;

using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Extensions.Logging;

/// <summary>
/// RDP controller implementation backed by the MSTSCLib ActiveX control.
///
/// MSTSCLib is a COM/ActiveX component that requires:
///   1. A Single-Threaded Apartment (STA) thread.
///   2. A Windows Forms message pump (Application.Run) on that thread.
///   3. A host Form/Control to site the ActiveX control.
///
/// This class manages a dedicated STA background thread that runs a hidden
/// <see cref="RdpHostForm"/> with the ActiveX control. All COM interactions are
/// marshalled to that thread via Control.Invoke / Control.BeginInvoke, while the
/// public API exposes async Task methods for callers on arbitrary threads.
/// </summary>
public sealed class MstscRdpController : IRdpController
{
    private readonly ILogger<MstscRdpController> _logger;
    private readonly object _lock = new();

    private Thread? _staThread;
    private RdpHostForm? _hostForm;
    private TaskCompletionSource<bool>? _formReadyTcs;
    private volatile bool _isConnected;
    private bool _disposed;

    public bool IsConnected => _isConnected;

    public event EventHandler? Disconnected;
    public event EventHandler? Connected;

    public MstscRdpController(ILogger<MstscRdpController> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Connects to a remote desktop using the specified configuration.
    /// Spins up the STA thread, creates the host form, and initiates the RDP connection.
    /// </summary>
    public async Task ConnectAsync(RdpConnectionConfig config, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_isConnected)
        {
            _logger.LogWarning("ConnectAsync called while already connected. Disconnecting first.");
            await DisconnectAsync();
        }

        _logger.LogInformation(
            "Connecting to RDP server {Server}:{Port} as {Username} ({Width}x{Height}x{ColorDepth})",
            config.Server, config.Port, config.Username, config.Width, config.Height, config.ColorDepth);

        _formReadyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Start the dedicated STA thread with a WinForms message pump.
        _staThread = new Thread(() => StaThreadEntry(config))
        {
            Name = "MstscRdpController-STA",
            IsBackground = true,
        };
        _staThread.SetApartmentState(ApartmentState.STA);
        _staThread.Start();

        // Wait for the host form to be ready (or for cancellation).
        using var reg = ct.Register(() => _formReadyTcs.TrySetCanceled(ct));
        await _formReadyTcs.Task;

        _logger.LogInformation("RDP connection initiated successfully.");
    }

    /// <summary>
    /// Entry point for the STA background thread.
    /// Creates the host form, configures the ActiveX control, and starts the message pump.
    /// </summary>
    private void StaThreadEntry(RdpConnectionConfig config)
    {
        try
        {
            _logger.LogDebug("STA thread started.");

            _hostForm = new RdpHostForm();
            _hostForm.FormClosed += (_, _) =>
            {
                _logger.LogDebug("RDP host form closed.");
            };

            // Configure the ActiveX RDP client control hosted inside the form.
            ConfigureRdpClient(config);

            // Signal that the form is ready so ConnectAsync can return.
            _formReadyTcs?.TrySetResult(true);

            // Run the message pump. This blocks until the form is closed.
            System.Windows.Forms.Application.Run(_hostForm);

            _logger.LogDebug("STA thread message pump exited.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "STA thread encountered an unhandled exception.");
            _formReadyTcs?.TrySetException(ex);
        }
        finally
        {
            _isConnected = false;
        }
    }

    /// <summary>
    /// Configures the MSTSCLib ActiveX control with connection parameters and event handlers.
    /// Must be called on the STA thread.
    /// </summary>
    private void ConfigureRdpClient(RdpConnectionConfig config)
    {
        // TODO: MSTSCLib integration
        // The following pseudocode shows where real MSTSCLib calls would go.
        //
        // var rdpClient = _hostForm!.RdpClient;  // AxMsRdpClient9NotSafeForScripting
        //
        // rdpClient.Server = config.Server;
        // rdpClient.AdvancedSettings9.RDPPort = config.Port;
        // rdpClient.UserName = config.Username;
        // rdpClient.AdvancedSettings9.ClearTextPassword = config.Password;
        // rdpClient.Domain = config.Domain;
        // rdpClient.DesktopWidth = config.Width;
        // rdpClient.DesktopHeight = config.Height;
        // rdpClient.ColorDepth = config.ColorDepth;
        //
        // // Performance settings for automation use.
        // rdpClient.AdvancedSettings9.BitmapPersistence = 1;
        // rdpClient.AdvancedSettings9.Compress = 1;
        //
        // // Wire up events.
        // rdpClient.OnConnected += (_, _) =>
        // {
        //     _isConnected = true;
        //     _logger.LogInformation("RDP session connected.");
        //     Connected?.Invoke(this, EventArgs.Empty);
        // };
        // rdpClient.OnDisconnected += (_, args) =>
        // {
        //     _isConnected = false;
        //     _logger.LogWarning("RDP session disconnected (reason: {Reason}).", args.discReason);
        //     Disconnected?.Invoke(this, EventArgs.Empty);
        // };
        //
        // rdpClient.Connect();

        _isConnected = true;
        _logger.LogWarning("MSTSCLib is not yet integrated. Simulating successful connection.");
        Connected?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gracefully disconnects the RDP session and shuts down the STA thread.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (!_isConnected && _hostForm is null)
        {
            _logger.LogDebug("DisconnectAsync called but not connected; no-op.");
            return;
        }

        _logger.LogInformation("Disconnecting RDP session...");

        await InvokeOnStaThreadAsync(() =>
        {
            // TODO: MSTSCLib integration
            // var rdpClient = _hostForm!.RdpClient;
            // if (rdpClient.Connected != 0)
            // {
            //     rdpClient.Disconnect();
            // }

            _isConnected = false;
            _hostForm?.Close();
        });

        // Give the STA thread time to exit gracefully.
        _staThread?.Join(TimeSpan.FromSeconds(5));
        _staThread = null;
        _hostForm = null;

        _logger.LogInformation("RDP session disconnected.");
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Captures a screenshot of the remote desktop as a PNG byte array.
    /// </summary>
    public async Task<byte[]> CaptureScreenshotAsync()
    {
        EnsureConnected();

        _logger.LogDebug("Capturing screenshot...");

        return await InvokeOnStaThreadAsync(() =>
        {
            // TODO: MSTSCLib integration
            // Use IMsRdpClientNonScriptable5.GetRemoteDesktopBitmap() or similar.
            //
            // var nonScriptable = (IMsRdpClientNonScriptable5)_hostForm!.RdpClient.GetOcx();
            // // Alternatively, capture the control's client area:
            // var control = _hostForm!.RdpClient;
            // var bmp = new Bitmap(control.Width, control.Height);
            // control.DrawToBitmap(bmp, new Rectangle(0, 0, control.Width, control.Height));
            // using var ms = new MemoryStream();
            // bmp.Save(ms, ImageFormat.Png);
            // return ms.ToArray();

            _logger.LogWarning("MSTSCLib is not yet integrated. Returning empty screenshot placeholder.");

            // Return a minimal 1x1 transparent PNG as a placeholder.
            using var bmp = new Bitmap(1, 1);
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        });
    }

    /// <summary>
    /// Sends a single left-click at the specified coordinates on the remote desktop.
    /// </summary>
    public async Task ClickAsync(int x, int y)
    {
        EnsureConnected();
        _logger.LogDebug("Click at ({X}, {Y})", x, y);

        await InvokeOnStaThreadAsync(() =>
        {
            // TODO: MSTSCLib integration
            // var nonScriptable = (IMsRdpClientNonScriptable5)_hostForm!.RdpClient.GetOcx();
            // nonScriptable.SendMouseInput(CYCLEMOUSE_CYCLEBUTTON_LEFT, x, y);
            //
            // Or use the IRemoteDesktopClientActions interface:
            // _hostForm!.RdpClient.AdvancedSettings9... (mouse events)
            //
            // Simplified approach using SendInput-style calls:
            // SendMouseEvent(x, y, MouseEvent.LeftDown);
            // SendMouseEvent(x, y, MouseEvent.LeftUp);

            _logger.LogWarning("MSTSCLib is not yet integrated. Click at ({X}, {Y}) was a no-op.", x, y);
        });
    }

    /// <summary>
    /// Sends a double left-click at the specified coordinates on the remote desktop.
    /// </summary>
    public async Task DoubleClickAsync(int x, int y)
    {
        EnsureConnected();
        _logger.LogDebug("Double-click at ({X}, {Y})", x, y);

        await InvokeOnStaThreadAsync(() =>
        {
            // TODO: MSTSCLib integration
            // Two rapid left-click sequences at the same position.
            // SendMouseEvent(x, y, MouseEvent.LeftDown);
            // SendMouseEvent(x, y, MouseEvent.LeftUp);
            // SendMouseEvent(x, y, MouseEvent.LeftDown);
            // SendMouseEvent(x, y, MouseEvent.LeftUp);

            _logger.LogWarning("MSTSCLib is not yet integrated. Double-click at ({X}, {Y}) was a no-op.", x, y);
        });
    }

    /// <summary>
    /// Sends a right-click at the specified coordinates on the remote desktop.
    /// </summary>
    public async Task RightClickAsync(int x, int y)
    {
        EnsureConnected();
        _logger.LogDebug("Right-click at ({X}, {Y})", x, y);

        await InvokeOnStaThreadAsync(() =>
        {
            // TODO: MSTSCLib integration
            // SendMouseEvent(x, y, MouseEvent.RightDown);
            // SendMouseEvent(x, y, MouseEvent.RightUp);

            _logger.LogWarning("MSTSCLib is not yet integrated. Right-click at ({X}, {Y}) was a no-op.", x, y);
        });
    }

    /// <summary>
    /// Sends text as keystrokes to the remote session.
    /// </summary>
    public async Task SendKeysAsync(string text)
    {
        EnsureConnected();
        _logger.LogDebug("SendKeys: \"{Text}\"", text);

        await InvokeOnStaThreadAsync(() =>
        {
            // TODO: MSTSCLib integration
            // Use IMsRdpClientNonScriptable::SendKeys or the virtual channel approach.
            //
            // Option A: IMsRdpClientNonScriptable5.SendKeys
            //   foreach (var ch in text)
            //   {
            //       var vk = NativeMethods.VkKeyScan(ch);
            //       nonScriptable.SendKeys(new[] { (int)vk }, new[] { true });   // key down
            //       nonScriptable.SendKeys(new[] { (int)vk }, new[] { false });  // key up
            //   }
            //
            // Option B: Send via the WinForms control
            //   System.Windows.Forms.SendKeys.Send(text);

            _logger.LogWarning("MSTSCLib is not yet integrated. SendKeys was a no-op.");
        });
    }

    /// <summary>
    /// Sends a key combination (e.g., "ctrl+c", "alt+f4") to the remote session.
    /// </summary>
    public async Task SendKeyComboAsync(string combo)
    {
        EnsureConnected();
        _logger.LogDebug("SendKeyCombo: \"{Combo}\"", combo);

        await InvokeOnStaThreadAsync(() =>
        {
            // TODO: MSTSCLib integration
            // Parse the combo string (e.g., "ctrl+shift+esc") into virtual key codes,
            // send all modifier key-downs, then the main key down+up, then modifier key-ups.
            //
            // var keys = ParseKeyCombo(combo); // returns list of VK codes
            // // Press all keys down
            // foreach (var vk in keys)
            //     nonScriptable.SendKeys(new[] { vk }, new[] { true });
            // // Release in reverse order
            // foreach (var vk in keys.Reverse())
            //     nonScriptable.SendKeys(new[] { vk }, new[] { false });

            _logger.LogWarning("MSTSCLib is not yet integrated. SendKeyCombo \"{Combo}\" was a no-op.", combo);
        });
    }

    /// <summary>
    /// Marshals an action to the STA thread via Control.Invoke and returns a completed Task.
    /// </summary>
    private Task InvokeOnStaThreadAsync(Action action)
    {
        if (_hostForm is null || _hostForm.IsDisposed)
        {
            throw new InvalidOperationException("RDP host form is not available.");
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            _hostForm.Invoke(() =>
            {
                try
                {
                    action();
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }

        return tcs.Task;
    }

    /// <summary>
    /// Marshals a function to the STA thread via Control.Invoke and returns the result.
    /// </summary>
    private Task<T> InvokeOnStaThreadAsync<T>(Func<T> func)
    {
        if (_hostForm is null || _hostForm.IsDisposed)
        {
            throw new InvalidOperationException("RDP host form is not available.");
        }

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            _hostForm.Invoke(() =>
            {
                try
                {
                    tcs.SetResult(func());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }

        return tcs.Task;
    }

    /// <summary>
    /// Throws if the controller is not currently connected.
    /// </summary>
    private void EnsureConnected()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_isConnected)
        {
            throw new InvalidOperationException("Not connected to an RDP session. Call ConnectAsync first.");
        }
    }

    /// <summary>
    /// Disposes the controller, disconnecting any active session and cleaning up the STA thread.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _logger.LogDebug("Disposing MstscRdpController...");

        try
        {
            await DisconnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disposal disconnect.");
        }

        _logger.LogDebug("MstscRdpController disposed.");
    }
}
