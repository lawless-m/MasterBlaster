namespace MasterBlaster.Rdp;

using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

public sealed class RdpHostForm : Form
{
    private RdpAxHost? _rdpHost;

    /// <summary>
    /// The hosted ActiveX RDP control.
    /// </summary>
    public RdpAxHost RdpControl => _rdpHost ?? throw new InvalidOperationException("RDP control not initialized.");

    public RdpHostForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "MasterBlaster RDP Host";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(0, 0);
        Size = new Size(1920, 1080);

        SuspendLayout();

        _rdpHost = new RdpAxHost();
        ((System.ComponentModel.ISupportInitialize)_rdpHost).BeginInit();
        _rdpHost.Dock = DockStyle.Fill;
        _rdpHost.Name = "rdpClient";
        Controls.Add(_rdpHost);
        ((System.ComponentModel.ISupportInitialize)_rdpHost).EndInit();

        ResumeLayout(false);
    }

    public void SetDesktopSize(int width, int height)
    {
        Size = new Size(width, height);
        _rdpHost?.SetBounds(0, 0, width, height);
    }

    public byte[] CaptureScreenshot()
    {
        if (_rdpHost is null) throw new InvalidOperationException("RDP control not initialized.");

        var loc = PointToScreen(Point.Empty);
        var width = _rdpHost.Width;
        var height = _rdpHost.Height;

        using var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(loc.X, loc.Y, 0, 0, new Size(width, height));
        }

        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    /// <summary>
    /// Get the inner RDP input window handle for sending mouse/key messages.
    /// </summary>
    public IntPtr GetInputWindowHandle()
    {
        if (_rdpHost is null || !_rdpHost.IsHandleCreated)
            throw new InvalidOperationException("RDP control handle not created.");
        return NativeMethods.FindDeepestChild(_rdpHost.Handle);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _rdpHost?.Dispose();
            _rdpHost = null;
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Custom AxHost subclass that hosts the MsRdpClient ActiveX control
/// without requiring aximp-generated interop assemblies.
/// </summary>
public class RdpAxHost : AxHost
{
    // MsRdpClient10NotSafeForScripting — RDP client v11, available on Windows 10+
    private const string RdpClientClsid = "A0C63C30-F08D-4AB4-907C-34905D770C7D";

    public RdpAxHost() : base(RdpClientClsid) { }

    /// <summary>
    /// Returns the underlying COM object. Use dynamic to access RDP properties/methods.
    /// </summary>
    public object? Ocx => GetOcx();
}
