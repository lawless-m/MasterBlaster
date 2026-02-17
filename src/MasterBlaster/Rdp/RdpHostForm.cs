namespace MasterBlaster.Rdp;

using System.Windows.Forms;

/// <summary>
/// A hidden Windows Forms form that hosts the MSTSCLib ActiveX control.
///
/// MSTSCLib (the Microsoft RDP ActiveX control) is a COM component that must be
/// sited on a Windows Forms control within a Single-Threaded Apartment (STA) thread
/// that runs a message pump. This form serves as that host container.
///
/// The form is configured to:
///   - Start hidden (not visible, no taskbar entry)
///   - Size itself to match the requested remote desktop dimensions
///   - Host the AxMsRdpClient ActiveX control as a child
///
/// Usage:
///   The <see cref="MstscRdpController"/> creates an instance of this form on a
///   dedicated STA thread and calls Application.Run(form) to start the message pump.
///   All RDP operations are marshalled to this thread via Control.Invoke.
/// </summary>
public sealed class RdpHostForm : Form
{
    // TODO: MSTSCLib integration
    // When the MSTSCLib COM reference is added, uncomment and use:
    //
    // private AxMSTSCLib.AxMsRdpClient9NotSafeForScripting? _rdpClient;
    //
    // /// <summary>
    // /// Gets the hosted MSTSCLib ActiveX RDP client control.
    // /// </summary>
    // public AxMSTSCLib.AxMsRdpClient9NotSafeForScripting RdpClient
    //     => _rdpClient ?? throw new InvalidOperationException("RDP client has not been initialized.");

    public RdpHostForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        // Form configuration: hidden, non-interactive.
        Text = "MasterBlaster RDP Host";
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        Visible = false;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Location = new System.Drawing.Point(-10000, -10000);
        Size = new System.Drawing.Size(1920, 1080);

        // TODO: MSTSCLib integration
        // Create and add the ActiveX control:
        //
        // SuspendLayout();
        //
        // _rdpClient = new AxMSTSCLib.AxMsRdpClient9NotSafeForScripting();
        // ((System.ComponentModel.ISupportInitialize)_rdpClient).BeginInit();
        //
        // _rdpClient.Dock = DockStyle.Fill;
        // _rdpClient.Enabled = true;
        // _rdpClient.Name = "rdpClient";
        //
        // Controls.Add(_rdpClient);
        //
        // ((System.ComponentModel.ISupportInitialize)_rdpClient).EndInit();
        //
        // ResumeLayout(false);
    }

    /// <summary>
    /// Updates the form size to match the requested remote desktop dimensions.
    /// Must be called before connecting the RDP client.
    /// </summary>
    /// <param name="width">Desktop width in pixels.</param>
    /// <param name="height">Desktop height in pixels.</param>
    public void SetDesktopSize(int width, int height)
    {
        Size = new System.Drawing.Size(width, height);

        // TODO: MSTSCLib integration
        // _rdpClient?.Size = new System.Drawing.Size(width, height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // TODO: MSTSCLib integration
            // if (_rdpClient is not null)
            // {
            //     if (_rdpClient.Connected != 0)
            //     {
            //         try { _rdpClient.Disconnect(); } catch { /* best-effort */ }
            //     }
            //     _rdpClient.Dispose();
            //     _rdpClient = null;
            // }
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Suppresses the form from becoming visible during normal operation.
    /// </summary>
    protected override void SetVisibleCore(bool value)
    {
        // Prevent the form from ever showing. The ActiveX control works fine
        // inside a hidden form; we just need the message pump.
        base.SetVisibleCore(false);
    }
}
