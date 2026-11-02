using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

using Censio.Companion.Core;

namespace Censio.Companion;

/// <summary>
/// Draws the icons of the three looks of the notification area icon.
/// </summary>
internal sealed class TrayIconImages : IDisposable
{
    #region Constants

    /// <summary>
    /// The size of the header of an icon file with one entry.
    /// </summary>
    private const int HeaderLength = 22;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The icon of each look
    /// </summary>
    private readonly Dictionary<TrayIconLook, Icon> _icons = [];

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayIconImages" /> class and draws the icons at the small icon size.
    /// </summary>
    public TrayIconImages()
    {
        var size = SystemInformation.SmallIconSize;

        foreach (var look in Enum.GetValues<TrayIconLook>())
        {
            _icons[look] = Draw(look, Math.Clamp(size.Width, 16, 255), Math.Clamp(size.Height, 16, 255));
        }
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Gets the icon of a look.
    /// </summary>
    /// <param name="look">The look</param>
    /// <returns>The icon, owned by this instance</returns>
    public Icon Get(TrayIconLook look)
    {
        return _icons[look];
    }

    /// <summary>
    /// Draws the icon of a look: a grey ring, a green filled circle or a red filled triangle.
    /// </summary>
    /// <param name="look">The look</param>
    /// <param name="width">The width in pixels</param>
    /// <param name="height">The height in pixels</param>
    /// <returns>The icon</returns>
    private static Icon Draw(TrayIconLook look, int width, int height)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            var margin = Math.Max(1f, width / 8f);
            var area = new RectangleF(margin, margin, width - (2 * margin) - 1, height - (2 * margin) - 1);

            switch (look)
            {
                case TrayIconLook.Receiving:
                    {
                        using var brush = new SolidBrush(Color.FromArgb(0x2E, 0xA0, 0x43));

                        graphics.FillEllipse(brush, area);
                    }
                    break;

                case TrayIconLook.Problem:
                    {
                        using var brush = new SolidBrush(Color.FromArgb(0xD1, 0x24, 0x2F));

                        PointF[] points = [new PointF(area.Left + (area.Width / 2f), area.Top), new PointF(area.Right, area.Bottom), new PointF(area.Left, area.Bottom)];

                        graphics.FillPolygon(brush, points);
                    }
                    break;

                default:
                    {
                        using var pen = new Pen(Color.FromArgb(0x8C, 0x8C, 0x8C), Math.Max(2f, width / 8f));

                        area.Inflate(-pen.Width / 2f, -pen.Width / 2f);
                        graphics.DrawEllipse(pen, area);
                    }
                    break;
            }
        }

        using var png = new MemoryStream();

        bitmap.Save(png, ImageFormat.Png);

        using var file = new MemoryStream();
        using var writer = new BinaryWriter(file);

        writer.Write((short)0);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write((byte)width);
        writer.Write((byte)height);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((short)1);
        writer.Write((short)32);
        writer.Write((int)png.Length);
        writer.Write(HeaderLength);
        writer.Write(png.ToArray());
        writer.Flush();

        file.Position = 0;

        return new Icon(file);
    }

    #endregion // Methods

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var icon in _icons.Values)
        {
            icon.Dispose();
        }

        _icons.Clear();
    }

    #endregion // IDisposable
}