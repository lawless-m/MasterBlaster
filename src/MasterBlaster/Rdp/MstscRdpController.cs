namespace MasterBlaster.Rdp;

using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Extensions.Logging;

public sealed class MstscRdpController : IRdpController
{
    private readonly ILogger<MstscRdpController> _logger;

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

        _staThread = new Thread(() => StaThreadEntry(config))
        {
            Name = "MstscRdpController-STA",
            IsBackground = true,
        };
        _staThread.SetApartmentState(ApartmentState.STA);
        _staThread.Start();

        using var reg = ct.Register(() => _formReadyTcs.TrySetCanceled(ct));
        await _formReadyTcs.Task;

        _logger.LogInformation("RDP connection initiated successfully.");
    }

    private void StaThreadEntry(RdpConnectionConfig config)
    {
        try
        {
            _logger.LogDebug("STA thread started.");

            _hostForm = new RdpHostForm();
            _hostForm.SetDesktopSize(config.Width, config.Height);
            _hostForm.Show(); // Required for ActiveX control to initialize

            ConfigureRdpClient(config);

            _formReadyTcs?.TrySetResult(true);

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

    private void ConfigureRdpClient(RdpConnectionConfig config)
    {
        var rdpHost = _hostForm!.RdpControl;
        rdpHost.CreateControl();

        dynamic ocx = rdpHost.Ocx!;

        ocx.Server = config.Server;
        ocx.UserName = config.Username;
        ocx.Domain = config.Domain;
        ocx.DesktopWidth = config.Width;
        ocx.DesktopHeight = config.Height;
        ocx.ColorDepth = config.ColorDepth;

        dynamic advSettings = ocx.AdvancedSettings9;
        advSettings.RDPPort = config.Port;
        advSettings.ClearTextPassword = config.Password;
        advSettings.EnableCredSspSupport = true;
        advSettings.NegotiateSecurityLayer = true;
        advSettings.BitmapPersistence = 1;
        advSettings.Compress = 1;

        ocx.Connect();

        _isConnected = true;
        _logger.LogInformation("RDP session connected.");
        Connected?.Invoke(this, EventArgs.Empty);
    }

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
            try
            {
                dynamic ocx = _hostForm!.RdpControl.Ocx!;
                if ((int)ocx.Connected != 0)
                    ocx.Disconnect();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during RDP disconnect.");
            }

            _isConnected = false;
            _hostForm?.Close();
        });

        _staThread?.Join(TimeSpan.FromSeconds(5));
        _staThread = null;
        _hostForm = null;

        _logger.LogInformation("RDP session disconnected.");
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public async Task<byte[]> CaptureScreenshotAsync()
    {
        EnsureConnected();
        _logger.LogDebug("Capturing screenshot...");

        return await InvokeOnStaThreadAsync(() => _hostForm!.CaptureScreenshot());
    }

    public async Task ClickAsync(int x, int y)
    {
        EnsureConnected();
        _logger.LogDebug("Click at ({X}, {Y})", x, y);

        await InvokeOnStaThreadAsync(() =>
        {
            var hwnd = _hostForm!.GetInputWindowHandle();
            var lParam = NativeMethods.MakeLParam(x, y);
            NativeMethods.SendMessage(hwnd, NativeMethods.WM_LBUTTONDOWN, (IntPtr)NativeMethods.MK_LBUTTON, lParam);
            NativeMethods.SendMessage(hwnd, NativeMethods.WM_LBUTTONUP, IntPtr.Zero, lParam);
        });
    }

    public async Task DoubleClickAsync(int x, int y)
    {
        EnsureConnected();
        _logger.LogDebug("Double-click at ({X}, {Y})", x, y);

        await InvokeOnStaThreadAsync(() =>
        {
            var hwnd = _hostForm!.GetInputWindowHandle();
            var lParam = NativeMethods.MakeLParam(x, y);
            NativeMethods.SendMessage(hwnd, NativeMethods.WM_LBUTTONDOWN, (IntPtr)NativeMethods.MK_LBUTTON, lParam);
            NativeMethods.SendMessage(hwnd, NativeMethods.WM_LBUTTONUP, IntPtr.Zero, lParam);
            NativeMethods.SendMessage(hwnd, NativeMethods.WM_LBUTTONDBLCLK, (IntPtr)NativeMethods.MK_LBUTTON, lParam);
            NativeMethods.SendMessage(hwnd, NativeMethods.WM_LBUTTONUP, IntPtr.Zero, lParam);
        });
    }

    public async Task RightClickAsync(int x, int y)
    {
        EnsureConnected();
        _logger.LogDebug("Right-click at ({X}, {Y})", x, y);

        await InvokeOnStaThreadAsync(() =>
        {
            var hwnd = _hostForm!.GetInputWindowHandle();
            var lParam = NativeMethods.MakeLParam(x, y);
            NativeMethods.SendMessage(hwnd, NativeMethods.WM_RBUTTONDOWN, (IntPtr)NativeMethods.MK_RBUTTON, lParam);
            NativeMethods.SendMessage(hwnd, NativeMethods.WM_RBUTTONUP, IntPtr.Zero, lParam);
        });
    }

    public async Task SendKeysAsync(string text)
    {
        EnsureConnected();
        _logger.LogDebug("SendKeys: \"{Text}\"", text);

        await InvokeOnStaThreadAsync(() =>
        {
            var hwnd = _hostForm!.GetInputWindowHandle();
            foreach (var ch in text)
            {
                NativeMethods.SendMessage(hwnd, NativeMethods.WM_CHAR, (IntPtr)ch, IntPtr.Zero);
            }
        });
    }

    public async Task SendKeyComboAsync(string combo)
    {
        EnsureConnected();
        _logger.LogDebug("SendKeyCombo: \"{Combo}\"", combo);

        await InvokeOnStaThreadAsync(() =>
        {
            var hwnd = _hostForm!.GetInputWindowHandle();
            var parts = combo.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var keys = new List<(int vk, bool extended)>();
            foreach (var part in parts)
            {
                keys.Add(NativeMethods.ParseKeyName(part));
            }

            // Press all keys down
            foreach (var (vk, ext) in keys)
                NativeMethods.SendKeyDown(hwnd, vk, ext);

            // Release in reverse order
            for (int i = keys.Count - 1; i >= 0; i--)
                NativeMethods.SendKeyUp(hwnd, keys[i].vk, keys[i].extended);
        });
    }

    private Task InvokeOnStaThreadAsync(Action action)
    {
        if (_hostForm is null || _hostForm.IsDisposed)
            throw new InvalidOperationException("RDP host form is not available.");

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

    private Task<T> InvokeOnStaThreadAsync<T>(Func<T> func)
    {
        if (_hostForm is null || _hostForm.IsDisposed)
            throw new InvalidOperationException("RDP host form is not available.");

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

    private void EnsureConnected()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_isConnected)
            throw new InvalidOperationException("Not connected to an RDP session. Call ConnectAsync first.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

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
