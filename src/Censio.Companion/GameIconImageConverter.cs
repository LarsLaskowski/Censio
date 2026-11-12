using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Media.Imaging;

using Censio.GameData;

namespace Censio.Companion;

/// <summary>
/// Converts a game icon to a frozen image decoded at twice the size of a table cell; each distinct icon is decoded once.
/// </summary>
internal sealed class GameIconImageConverter : IValueConverter
{
    #region Constants

    /// <summary>
    /// The side of the image in a cell in device-independent pixels
    /// </summary>
    internal const int CellSide = 20;

    /// <summary>
    /// The factor of the decoded side to the displayed side, so the image stays sharp up to 200 % display scaling
    /// </summary>
    private const int DecodeFactor = 2;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The decoded images by icon
    /// </summary>
    private static readonly ConditionalWeakTable<GameIcon, BitmapImage?> _images = [];

    #endregion // Fields

    #region Methods

    /// <summary>
    /// Decodes an icon at the cell size.
    /// </summary>
    /// <param name="icon">The icon</param>
    /// <returns>The frozen image, or <c>null</c> when the PNG cannot be decoded</returns>
    private static BitmapImage? Decode(GameIcon icon)
    {
        try
        {
            using var stream = new MemoryStream(icon.Png.ToArray(), false);

            var image = new BitmapImage();

            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;

            if (icon.Width >= icon.Height)
            {
                image.DecodePixelWidth = CellSide * DecodeFactor;
            }
            else
            {
                image.DecodePixelHeight = CellSide * DecodeFactor;
            }

            image.EndInit();
            image.Freeze();

            return image;
        }
        catch (Exception exception) when (exception is NotSupportedException or IOException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    #endregion // Methods

    #region IValueConverter

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is GameIcon icon ? _images.GetValue(icon, Decode) : null;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    #endregion // IValueConverter
}