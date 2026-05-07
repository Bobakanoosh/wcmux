// Generates docs/wcmux.ico (multi-resolution) and docs/wcmux.png (256x256 preview)
// from a programmatic vector design. Run with `dotnet run --project tools/IconGen`.
//
// Design:
//   - Rounded dark square background (matches app #1E1E1E)
//   - Sidebar stripe down the left third (the vertical tab sidebar)
//   - Active-tab accent bar in terminal-green
//   - Cursor block + prompt chevron in the main pane (terminal feel)

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

var bgColor      = Color.FromArgb(255, 30, 30, 30);     // #1E1E1E
var sidebarColor = Color.FromArgb(255, 25, 25, 25);     // #191919
var accentColor  = Color.FromArgb(255, 0, 200, 150);    // #00C896 terminal-green
var cursorColor  = Color.FromArgb(255, 230, 230, 230);  // #E6E6E6
var dividerColor = Color.FromArgb(255, 60, 60, 60);     // #3C3C3C
var dimAccent    = Color.FromArgb(120, 200, 200, 200);

Bitmap RenderLogo(int size)
{
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.Clear(Color.Transparent);

    int radius = Math.Max(2, (int)(size * 0.18));
    int d = radius * 2;
    var rect = new Rectangle(0, 0, size, size);
    using var path = new GraphicsPath();
    path.AddArc(rect.X, rect.Y, d, d, 180, 90);
    path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
    path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
    path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
    path.CloseFigure();

    using (var b = new SolidBrush(bgColor)) g.FillPath(b, path);
    g.SetClip(path);

    int sidebarWidth = (int)(size * 0.30);
    using (var b = new SolidBrush(sidebarColor))
        g.FillRectangle(b, 0, 0, sidebarWidth, size);

    int accentWidth = Math.Max(1, (int)(size * 0.06));
    int accentY = (int)(size * 0.18);
    int accentH = (int)(size * 0.22);
    using (var b = new SolidBrush(accentColor))
        g.FillRectangle(b, 0, accentY, accentWidth, accentH);

    if (size >= 32)
    {
        int tabH2 = (int)(size * 0.10);
        int tabGap = (int)(size * 0.06);
        using var b = new SolidBrush(dimAccent);
        g.FillRectangle(b, 0, accentY + accentH + tabGap, accentWidth, tabH2);
        g.FillRectangle(b, 0, accentY + accentH + tabGap * 2 + tabH2, accentWidth, tabH2);
    }

    using (var pen = new Pen(dividerColor, Math.Max(1, size * 0.015f)))
        g.DrawLine(pen, sidebarWidth, 0, sidebarWidth, size);

    int cursorW = (int)(size * 0.20);
    int cursorH = (int)(size * 0.28);
    int cursorX = sidebarWidth + (int)(size * 0.18);
    int cursorY = (size - cursorH) / 2;
    using (var b = new SolidBrush(cursorColor))
        g.FillRectangle(b, cursorX, cursorY, cursorW, cursorH);

    if (size >= 48)
    {
        using var pen = new Pen(accentColor, Math.Max(1.5f, size * 0.03f))
        {
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        int cx1 = sidebarWidth + (int)(size * 0.06);
        int cy1 = (int)(size * 0.40);
        int cx2 = sidebarWidth + (int)(size * 0.13);
        int cy2 = (int)(size * 0.50);
        int cy3 = (int)(size * 0.60);
        g.DrawLines(pen, new[] {
            new Point(cx1, cy1),
            new Point(cx2, cy2),
            new Point(cx1, cy3),
        });
    }

    g.ResetClip();
    return bmp;
}

int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
var bitmaps = sizes.Select(RenderLogo).ToArray();

string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
string docsDir = Path.Combine(repoRoot, "docs");
Directory.CreateDirectory(docsDir);
string icoPath = Path.Combine(docsDir, "wcmux.ico");
string pngPath = Path.Combine(docsDir, "wcmux.png");

// Pack into a single ICO (PNG-encoded entries for size>=64, BMP-otherwise — but PNG works for all sizes on modern Windows)
byte[][] pngBlobs = bitmaps.Select(b =>
{
    using var ms = new MemoryStream();
    b.Save(ms, ImageFormat.Png);
    return ms.ToArray();
}).ToArray();

using (var fs = File.Create(icoPath))
using (var bw = new BinaryWriter(fs))
{
    bw.Write((ushort)0);
    bw.Write((ushort)1);
    bw.Write((ushort)sizes.Length);

    int offset = 6 + 16 * sizes.Length;
    for (int i = 0; i < sizes.Length; i++)
    {
        int s = sizes[i];
        byte dim = (byte)(s >= 256 ? 0 : s);
        bw.Write(dim);                  // width
        bw.Write(dim);                  // height
        bw.Write((byte)0);              // color count
        bw.Write((byte)0);              // reserved
        bw.Write((ushort)1);            // planes
        bw.Write((ushort)32);           // bpp
        bw.Write((uint)pngBlobs[i].Length);
        bw.Write((uint)offset);
        offset += pngBlobs[i].Length;
    }
    foreach (var blob in pngBlobs) bw.Write(blob);
}

bitmaps[^1].Save(pngPath, ImageFormat.Png);

foreach (var b in bitmaps) b.Dispose();

Console.WriteLine($"Wrote {icoPath}");
Console.WriteLine($"Wrote {pngPath}");
