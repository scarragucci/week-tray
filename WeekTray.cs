// WeekTray - tray icon that shows the current ISO 8601 week number.
// No network, no admin, no dependencies beyond .NET Framework (in-box on Windows).
//   WeekTray.exe                      run in the tray
//   WeekTray.exe --export-icons DIR   write week-01..week-53.ico + app.ico, then exit

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

static class Program
{
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--export-icons") return Icons.Export(args[1]);

        bool first;
        using (new Mutex(true, @"Local\WeekTray", out first))
        {
            if (!first) return 0; // already running
            SetProcessDPIAware();  // crisp icon at 125/150% scaling
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayContext());
        }
        return 0;
    }
}

static class Week
{
    static int MondayOffset(DateTime d) { return ((int)d.DayOfWeek + 6) % 7; } // Mon=0 .. Sun=6

    public static int Iso(DateTime d, out int year)
    {
        DateTime thursday = d.Date.AddDays(3 - MondayOffset(d)); // ISO week belongs to the year of its Thursday
        year = thursday.Year;
        return (thursday.DayOfYear - 1) / 7 + 1;
    }

    public static string Range(DateTime d)
    {
        DateTime mon = d.Date.AddDays(-MondayOffset(d)), sun = mon.AddDays(6);
        return mon.ToString("d MMM") + " \u2013 " + sun.ToString("d MMM") + " " + sun.Year;
    }
}

class TrayContext : ApplicationContext
{
    readonly NotifyIcon tray = new NotifyIcon();
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly ToolStripMenuItem header = new ToolStripMenuItem();
    readonly ToolStripMenuItem autostart = new ToolStripMenuItem("Start with Windows");
    Icon icon;
    int shownWeek = -1, shownSize = -1;
    WeekPopup popup;
    DateTime popupClosedAt;

    public TrayContext()
    {
        header.Enabled = false;
        autostart.Click += delegate { Autostart.Set(!Autostart.IsOn()); };
        var menu = new ContextMenuStrip();
        menu.Items.Add(header);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(autostart);
        menu.Items.Add("Exit", null, delegate { ExitThread(); });
        menu.Opening += delegate { autostart.Checked = Autostart.IsOn(); };
        tray.ContextMenuStrip = menu;
        tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) TogglePopup(); };

        timer.Interval = 30000; // cheap check; also covers sleep/resume and clock changes
        timer.Tick += delegate { Refresh(); };
        timer.Start();
        Refresh();
        tray.Visible = true;
    }

    void Refresh()
    {
        int year, week = Week.Iso(DateTime.Now, out year);
        int size = SystemInformation.SmallIconSize.Width;
        if (week == shownWeek && size == shownSize) return;

        Icon old = icon;
        icon = Icons.Create(week.ToString(), size);
        tray.Icon = icon;
        if (old != null) old.Dispose();
        tray.Text = "Week " + week + " \u00B7 " + Week.Range(DateTime.Now);
        header.Text = "Week " + week + " \u00B7 " + year;
        shownWeek = week;
        shownSize = size;
    }

    void TogglePopup()
    {
        if (popup != null) { popup.Close(); return; }
        // Clicking the tray icon while the popup is open deactivates (closes) it first; don't reopen.
        if ((DateTime.Now - popupClosedAt).TotalMilliseconds < 300) return;

        int year, week = Week.Iso(DateTime.Now, out year);
        popup = new WeekPopup(week, Week.Range(DateTime.Now));
        popup.FormClosed += delegate { popup = null; popupClosedAt = DateTime.Now; };
        popup.ShowNear(Cursor.Position);
    }

    protected override void ExitThreadCore()
    {
        timer.Stop();
        if (popup != null) popup.Close();
        tray.Visible = false;
        tray.Dispose();
        if (icon != null) icon.Dispose();
        base.ExitThreadCore();
    }
}

class WeekPopup : Form
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    readonly string week, range;
    readonly Color fg, sub, accent;
    readonly Font small = new Font("Segoe UI", 10f);
    readonly Font big = new Font("Segoe UI Semibold", 40f);
    readonly System.Windows.Forms.Timer autoClose = new System.Windows.Forms.Timer();

    public WeekPopup(int week, string range)
    {
        this.week = week.ToString();
        this.range = range;
        bool dark = Theme.IsDark();
        BackColor = dark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(249, 249, 249);
        fg = dark ? Color.White : Color.FromArgb(26, 26, 26);
        sub = dark ? Color.FromArgb(200, 200, 200) : Color.FromArgb(96, 96, 96);
        accent = dark ? Color.FromArgb(76, 194, 255) : Icons.Accent;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
        DoubleBuffered = true;
        float k;
        using (Graphics g = CreateGraphics()) k = g.DpiX / 96f;
        ClientSize = new Size((int)(200 * k), (int)(150 * k));

        Deactivate += delegate { Close(); };
        Click += delegate { Close(); };
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        autoClose.Interval = 10000; // safety net if focus never arrives
        autoClose.Tick += delegate { Close(); };
    }

    protected override CreateParams CreateParams
    {
        get { CreateParams cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; } // CS_DROPSHADOW
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int round = 2; // DWMWA_WINDOW_CORNER_PREFERENCE = ROUND (Win11; ignored elsewhere)
        try { DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int)); } catch (DllNotFoundException) { }
    }

    public void ShowNear(Point p)
    {
        Rectangle wa = Screen.FromPoint(p).WorkingArea;
        int x = Math.Max(wa.Left + 8, Math.Min(p.X - Width / 2, wa.Right - Width - 8));
        int y = p.Y >= wa.Bottom ? wa.Bottom - Height - 12   // taskbar at bottom
              : p.Y < wa.Top ? wa.Top + 12                   // taskbar at top
              : p.Y - Height - 16;                           // overflow flyout
        y = Math.Max(wa.Top + 8, Math.Min(y, wa.Bottom - Height - 8));
        Location = new Point(x, y);
        Show();
        Activate();
        autoClose.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        int w = ClientSize.Width, h = ClientSize.Height;
        const TextFormatFlags center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
        TextRenderer.DrawText(e.Graphics, "WEEK", small, new Rectangle(0, h * 8 / 100, w, h * 16 / 100), sub, center);
        TextRenderer.DrawText(e.Graphics, week, big, new Rectangle(0, h * 22 / 100, w, h * 50 / 100), accent, center);
        TextRenderer.DrawText(e.Graphics, range, small, new Rectangle(0, h * 74 / 100, w, h * 16 / 100), fg, center);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { autoClose.Dispose(); small.Dispose(); big.Dispose(); }
        base.Dispose(disposing);
    }
}

static class Theme
{
    public static bool IsDark()
    {
        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
        {
            object v = k == null ? null : k.GetValue("SystemUsesLightTheme");
            return v is int && (int)v == 0;
        }
    }
}

static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", Name = "WeekTray";
    static string Command { get { return "\"" + Application.ExecutablePath + "\""; } }

    public static bool IsOn()
    {
        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
            return k != null && Command.Equals(k.GetValue(Name) as string, StringComparison.OrdinalIgnoreCase);
    }

    public static void Set(bool on)
    {
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            if (on) k.SetValue(Name, Command);
            else k.DeleteValue(Name, false);
        }
    }
}

static class Icons
{
    public static readonly Color Accent = Color.FromArgb(0, 103, 192); // Windows 11 default blue

    public static Icon Create(string text, int size)
    {
        using (Bitmap b = Draw(text, size))
            return new Icon(new MemoryStream(BuildIco(new[] { b })));
    }

    public static int Export(string dir)
    {
        Directory.CreateDirectory(dir);
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 256 };
        for (int week = 1; week <= 53; week++)
            File.WriteAllBytes(Path.Combine(dir, string.Format("week-{0:00}.ico", week)), MultiSize(week.ToString(), sizes));
        File.WriteAllBytes(Path.Combine(dir, "app.ico"), MultiSize("W", sizes));
        return 0;
    }

    static byte[] MultiSize(string text, int[] sizes)
    {
        var bmps = new List<Bitmap>();
        try
        {
            foreach (int s in sizes) bmps.Add(Draw(text, s));
            return BuildIco(bmps);
        }
        finally { foreach (Bitmap b in bmps) b.Dispose(); }
    }

    static Bitmap Draw(string text, int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        using (GraphicsPath bg = RoundRect(size, size * 0.22f))
        using (var fill = new SolidBrush(Accent))
        using (var glyphs = new GraphicsPath())
        using (var reference = new GraphicsPath())
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.FillPath(fill, bg);

            FontStyle style;
            using (FontFamily family = Family(out style))
            {
                glyphs.AddString(text, family, (int)style, 100f, PointF.Empty, StringFormat.GenericTypographic);
                reference.AddString("00", family, (int)style, 100f, PointF.Empty, StringFormat.GenericTypographic);
            }
            // Scale from "00" so every week gets the same digit height; snap height to whole pixels.
            RectangleF r = reference.GetBounds(), t = glyphs.GetBounds();
            float pad = size <= 16 ? 1.5f : size * 0.12f, avail = size - 2 * pad;
            float scale = Math.Min(avail / r.Width, avail / r.Height);
            scale = Math.Max(1f, (float)Math.Round(r.Height * scale)) / r.Height;
            float x = (size - t.Width * scale) / 2f - t.Left * scale;
            float y = (float)Math.Round((size - r.Height * scale) / 2f) - r.Top * scale;
            using (var m = new Matrix())
            {
                m.Scale(scale, scale, MatrixOrder.Append);
                m.Translate(x, y, MatrixOrder.Append);
                glyphs.Transform(m);
            }
            g.FillPath(Brushes.White, glyphs);
        }
        return bmp;
    }

    static FontFamily Family(out FontStyle style)
    {
        try
        {
            var f = new FontFamily("Bahnschrift SemiBold Condensed"); // narrow digits, in-box on Win10+
            if (f.IsStyleAvailable(FontStyle.Regular)) { style = FontStyle.Regular; return f; }
            f.Dispose();
        }
        catch (ArgumentException) { }
        style = FontStyle.Bold;
        return new FontFamily("Segoe UI");
    }

    static GraphicsPath RoundRect(float s, float r)
    {
        var p = new GraphicsPath();
        float d = r * 2;
        p.AddArc(0, 0, d, d, 180, 90);
        p.AddArc(s - d, 0, d, d, 270, 90);
        p.AddArc(s - d, s - d, d, d, 0, 90);
        p.AddArc(0, s - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // ICO container: 32-bit DIB entries, PNG for 256px.
    static byte[] BuildIco(IList<Bitmap> images)
    {
        var blobs = new List<byte[]>();
        foreach (Bitmap b in images) blobs.Add(b.Width >= 256 ? Png(b) : Dib(b));
        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)images.Count);
            int offset = 6 + 16 * images.Count;
            for (int i = 0; i < images.Count; i++)
            {
                byte dim = (byte)(images[i].Width >= 256 ? 0 : images[i].Width);
                w.Write(dim); w.Write(dim); w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32);
                w.Write(blobs[i].Length); w.Write(offset);
                offset += blobs[i].Length;
            }
            foreach (byte[] blob in blobs) w.Write(blob);
            w.Flush();
            return ms.ToArray();
        }
    }

    static byte[] Png(Bitmap b)
    {
        using (var ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); return ms.ToArray(); }
    }

    static byte[] Dib(Bitmap b)
    {
        int s = b.Width, row = s * 4, maskRow = (s + 31) / 32 * 4;
        var px = new byte[row * s];
        BitmapData data = b.LockBits(new Rectangle(0, 0, s, s), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < s; y++) Marshal.Copy(new IntPtr(data.Scan0.ToInt64() + (long)y * data.Stride), px, y * row, row);
        b.UnlockBits(data);

        var mask = new byte[maskRow * s]; // bottom-up AND mask: 1 = transparent
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                if (px[y * row + x * 4 + 3] < 128) mask[(s - 1 - y) * maskRow + x / 8] |= (byte)(0x80 >> (x % 8));

        using (var ms = new MemoryStream())
        using (var w = new BinaryWriter(ms))
        {
            w.Write(40); w.Write(s); w.Write(s * 2); w.Write((short)1); w.Write((short)32);
            w.Write(0); w.Write(px.Length + mask.Length); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
            for (int y = s - 1; y >= 0; y--) w.Write(px, y * row, row); // BGRA, bottom-up
            w.Write(mask);
            w.Flush();
            return ms.ToArray();
        }
    }
}
