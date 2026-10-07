# A full-screen backdrop behind the recorded windows, so the desktop never shows.
# It can never be activated and keeps itself at the bottom of the window order, so clicks and
# drags over it can't bring it in front of the apps being recorded.
# record.ps1 starts it in its own process and stops it at the end.
param([string]$Image)
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -ReferencedAssemblies System.Windows.Forms, System.Drawing @'
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class Backdrop : Form
{
    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr v);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int cy, uint flags);

    readonly Timer keepAtBottom = new Timer { Interval = 150 };

    public static void Run(string image)
    {
        SetProcessDpiAwarenessContext(new IntPtr(-4));
        Application.Run(new Backdrop(image));
    }

    Backdrop(string image)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Bounds = Screen.PrimaryScreen.Bounds;
        BackgroundImage = Image.FromFile(image);
        BackgroundImageLayout = ImageLayout.Stretch;
        Text = "Pouchy trailer backdrop";
        keepAtBottom.Tick += (s, e) => ToBottom();
        Shown += (s, e) => { ToBottom(); keepAtBottom.Start(); };
    }

    void ToBottom()
    {
        var b = Screen.PrimaryScreen.Bounds;
        SetWindowPos(Handle, new IntPtr(1), b.X, b.Y, b.Width, b.Height, 0x0010 | 0x0200); // HWND_BOTTOM, NOACTIVATE | NOOWNERZORDER
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 | 0x00000080; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0021) { m.Result = new IntPtr(3); return; } // WM_MOUSEACTIVATE -> MA_NOACTIVATE
        base.WndProc(ref m);
    }
}
'@
[Backdrop]::Run($Image)
