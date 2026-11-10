using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

using Censio.Companion.Core;

namespace Censio.Companion;

/// <summary>
/// The diagnostics window with the last tick.
/// </summary>
internal sealed partial class DiagnosticsWindow : Window, IDiagnosticsWindow
{
    #region Fields

    /// <summary>
    /// The name of the scroll viewer in the template of the grid
    /// </summary>
    private const string GridScrollViewerName = "DG_ScrollViewer";

    /// <summary>
    /// The index of the column with the region
    /// </summary>
    private const int RegionColumn = 0;

    /// <summary>
    /// The index of the column with the good
    /// </summary>
    private const int GoodColumn = 5;

    /// <summary>
    /// The converter of icons to images
    /// </summary>
    private static readonly GameIconImageConverter _converter = new();

    /// <summary>
    /// The dark backdrop of a region icon, whose glyph is light
    /// </summary>
    private static readonly Brush _regionBackdrop = CreateRegionBackdrop();

    /// <summary>
    /// Whether the window was shown
    /// </summary>
    private bool _shown;

    /// <summary>
    /// Whether the scroll offsets wait to be restored after a view was swapped
    /// </summary>
    private bool _restorePending;

    /// <summary>
    /// The vertical scroll offset to restore
    /// </summary>
    private double _verticalOffset;

    /// <summary>
    /// The horizontal scroll offset to restore
    /// </summary>
    private double _horizontalOffset;

    #endregion // Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticsWindow" /> class.
    /// </summary>
    public DiagnosticsWindow()
    {
        InitializeComponent();

        Title = DiagnosticsView.Title;

        for (var index = 0; index < DiagnosticsView.Columns.Count; index++)
        {
            if (index == RegionColumn || index == GoodColumn)
            {
                RowsGrid.Columns.Add(CreateIconColumn(index));

                continue;
            }

            RowsGrid.Columns.Add(new DataGridTextColumn
                                 {
                                     Header = DiagnosticsView.Columns[index],
                                     Binding = new Binding($"Cells[{index}]")
                                               {
                                                   Mode = BindingMode.OneTime
                                               },
                                     IsReadOnly = true,
                                     CanUserSort = false,
                                     MinWidth = 60
                                 });
        }
    }

    #endregion // Constructors

    #region Methods

    /// <summary>
    /// Creates the column of a name that shows an icon before the text.
    /// </summary>
    /// <param name="index">The index of the column</param>
    /// <returns>The column</returns>
    private static DataGridTemplateColumn CreateIconColumn(int index)
    {
        var isRegion = index == RegionColumn;
        var image = new FrameworkElementFactory(typeof(Image));

        image.SetBinding(Image.SourceProperty,
                         new Binding(isRegion ? nameof(DiagnosticsRow.RegionIcon) : nameof(DiagnosticsRow.GoodIcon))
                         {
                             Mode = BindingMode.OneTime,
                             Converter = _converter
                         });
        image.SetValue(FrameworkElement.WidthProperty, (double)GameIconImageConverter.CellSide);
        image.SetValue(FrameworkElement.HeightProperty, (double)GameIconImageConverter.CellSide);

        FrameworkElementFactory icon;

        if (isRegion)
        {
            var disc = new FrameworkElementFactory(typeof(Border));

            disc.SetValue(FrameworkElement.WidthProperty, (double)GameIconImageConverter.CellSide);
            disc.SetValue(FrameworkElement.HeightProperty, (double)GameIconImageConverter.CellSide);
            disc.SetValue(Border.CornerRadiusProperty, new CornerRadius(GameIconImageConverter.CellSide / 2d));
            disc.SetValue(Border.BackgroundProperty, _regionBackdrop);
            disc.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0));

            var withoutRegionIcon = new DataTrigger
                                    {
                                        Binding = new Binding(nameof(DiagnosticsRow.RegionIcon))
                                                  {
                                                      Converter = _converter
                                                  },
                                        Value = null
                                    };

            withoutRegionIcon.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed));

            var discStyle = new Style(typeof(Border));

            discStyle.Triggers.Add(withoutRegionIcon);
            disc.SetValue(FrameworkElement.StyleProperty, discStyle);
            disc.AppendChild(image);

            icon = disc;
        }
        else
        {
            image.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0));

            var withoutIcon = new Trigger
                              {
                                  Property = Image.SourceProperty,
                                  Value = null
                              };

            withoutIcon.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed));

            var style = new Style(typeof(Image));

            style.Triggers.Add(withoutIcon);
            image.SetValue(FrameworkElement.StyleProperty, style);

            icon = image;
        }

        var text = new FrameworkElementFactory(typeof(TextBlock));

        text.SetBinding(TextBlock.TextProperty,
                        new Binding($"Cells[{index}]")
                        {
                            Mode = BindingMode.OneTime
                        });
        text.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

        var panel = new FrameworkElementFactory(typeof(StackPanel));

        panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        panel.AppendChild(icon);
        panel.AppendChild(text);

        return new DataGridTemplateColumn
               {
                   Header = DiagnosticsView.Columns[index],
                   CellTemplate = new DataTemplate
                                  {
                                      VisualTree = panel
                                  },
                   IsReadOnly = true,
                   CanUserSort = false,
                   MinWidth = 60
               };
    }

    /// <summary>
    /// Creates the frozen backdrop brush of a region icon.
    /// </summary>
    /// <returns>The brush</returns>
    private static Brush CreateRegionBackdrop()
    {
        var brush = new SolidColorBrush(Color.FromRgb(0x3C, 0x3C, 0x3C));

        brush.Freeze();

        return brush;
    }

    /// <summary>
    /// Gets the scroll viewer of the grid.
    /// </summary>
    /// <returns>The scroll viewer, or <c>null</c> if the template is not applied</returns>
    private ScrollViewer? GetGridScrollViewer()
    {
        return RowsGrid.Template?.FindName(GridScrollViewerName, RowsGrid) as ScrollViewer;
    }

    /// <summary>
    /// Restores the scroll offsets after the layout of the swapped view.
    /// </summary>
    private void RestoreOffsets()
    {
        _restorePending = false;

        if (GetGridScrollViewer() is { } scroll)
        {
            scroll.ScrollToVerticalOffset(_verticalOffset);
            scroll.ScrollToHorizontalOffset(_horizontalOffset);
        }
    }

    #endregion // Methods

    #region Window

    /// <inheritdoc />
    protected override void OnClosed(EventArgs e)
    {
        RowsGrid.SelectedItem = null;
        RowsGrid.ItemsSource = null;
        HeaderList.ItemsSource = null;

        base.OnClosed(e);

        WindowClosed?.Invoke(this, EventArgs.Empty);
    }

    #endregion // Window

    #region IDiagnosticsWindow

    /// <inheritdoc />
    public event EventHandler? WindowClosed;

    /// <inheritdoc />
    public void ShowView(DiagnosticsView view)
    {
        if (_restorePending == false && GetGridScrollViewer() is { } scroll)
        {
            _verticalOffset = scroll.VerticalOffset;
            _horizontalOffset = scroll.HorizontalOffset;
        }

        HeaderList.ItemsSource = view.HeaderLines;
        RowsGrid.SelectedItem = null;
        RowsGrid.ItemsSource = new ListCollectionView(view);

        if (_restorePending == false)
        {
            _restorePending = true;

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, RestoreOffsets);
        }

        if (_shown == false)
        {
            _shown = true;
            Show();
        }
    }

    /// <inheritdoc />
    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    #endregion // IDiagnosticsWindow
}