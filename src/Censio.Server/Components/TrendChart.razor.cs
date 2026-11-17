using System.Globalization;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using MudBlazor;

namespace Censio.Server.Components;

/// <summary>
/// A trend chart of a data page: three lines over time, drawn from one read.
/// </summary>
public sealed partial class TrendChart : IDisposable
{
    #region Constants

    /// <summary>
    /// The format the tooltip's time label takes: culture-free, to the tick, so the label names one point.
    /// </summary>
    private const string TooltipTimeFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffff";

    /// <summary>
    /// The format of the labels of the time axis: digits and a literal colon.
    /// </summary>
    private const string TimeLabelFormat = "HH\\:mm";

    /// <summary>
    /// The most intervals the time axis is divided into.
    /// </summary>
    private const int MaxLabelIntervals = 8;

    /// <summary>
    /// The height above which a hovered point leaves no room for a tooltip above it.
    /// </summary>
    private const double BelowLimit = 120;

    #endregion // Constants

    #region Fields

    /// <summary>
    /// The distances between two labels of the time axis the chart may take, the smallest first.
    /// </summary>
    private static readonly TimeSpan[] _timeLabelSpacings = [TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30), TimeSpan.FromHours(1), TimeSpan.FromHours(2), TimeSpan.FromHours(3)];

    /// <summary>
    /// Cancels the call to the browser when the component is disposed.
    /// </summary>
    private readonly CancellationTokenSource _cancellation = new();

    /// <summary>
    /// The read the component last looked at.
    /// </summary>
    private TrendRead? _lastRead;

    /// <summary>
    /// A value that indicates whether the last read holds no value at all.
    /// </summary>
    private bool _hasNoValues;

    /// <summary>
    /// The chart that is drawn, or <see langword="null"/> while there is none.
    /// </summary>
    private DrawnTrend? _drawn;

    /// <summary>
    /// The options of the drawn chart.
    /// </summary>
    private TimeSeriesChartOptions _options = new();

    /// <summary>
    /// The colours of the lines.
    /// </summary>
    private string[] _palette = [];

    /// <summary>
    /// The kind, theme, language and label spacing the options were made for.
    /// </summary>
    private (TrendKind Kind, UiTheme Theme, UiLanguage Language, TimeSpan Spacing)? _optionsKey;

    /// <summary>
    /// The chart component, or <see langword="null"/> while none is drawn.
    /// </summary>
    private MudChart<double>? _chart;

    /// <summary>
    /// A value that indicates whether the series changed in a way the chart does not notice itself.
    /// </summary>
    private bool _isRebuildPending;

    /// <summary>
    /// The instant the browser was last asked about.
    /// </summary>
    private DateTimeOffset? _askedInstant;

    /// <summary>
    /// The offset the browser gave for <see cref="_askedInstant"/>.
    /// </summary>
    private int? _askedOffset;

    /// <summary>
    /// A value that indicates whether the browser is being asked.
    /// </summary>
    private bool _isAsking;

    /// <summary>
    /// A value that indicates whether the component is disposed.
    /// </summary>
    private volatile bool _isDisposed;

    #endregion // Fields

    #region Properties

    /// <summary>
    /// Gets or sets the heading of the chart.
    /// </summary>
    [Parameter]
    public string Heading { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the kind of the chart.
    /// </summary>
    [Parameter]
    public TrendKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the read the chart shows, or <see langword="null"/> while there is none.
    /// </summary>
    [Parameter]
    public TrendRead? Read { get; set; }

    /// <summary>
    /// Gets or sets the receive time of the latest tick, or <see langword="null"/> for none.
    /// </summary>
    [Parameter]
    public DateTimeOffset? AsOf { get; set; }

    /// <summary>
    /// Gets or sets the number of the read, which grows with every read.
    /// </summary>
    [Parameter]
    public int ReadNumber { get; set; }

    /// <summary>
    /// Gets or sets the content shown under the heading.
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Gets or sets the language shown.
    /// </summary>
    [CascadingParameter]
    public UiLanguage Language { get; set; }

    /// <summary>
    /// Gets or sets the texts of the language shown.
    /// </summary>
    [CascadingParameter]
    public UiTexts Texts { get; set; } = UiTexts.For(UiLanguage.English);

    /// <summary>
    /// Gets or sets the theme shown.
    /// </summary>
    [CascadingParameter]
    public UiTheme Theme { get; set; }

    /// <summary>
    /// Gets or sets the JavaScript runtime.
    /// </summary>
    [Inject]
    private IJSRuntime JsRuntime { get; set; } = null!;

    /// <summary>
    /// Gets the names of the lines in the language shown.
    /// </summary>
    private string[] LineNames => Kind == TrendKind.Good
                                      ? [Texts.ColumnProduction, Texts.ColumnConsumption, Texts.ColumnDelta]
                                      : [Texts.ColumnBuildings, Texts.ColumnProducedGoods, Texts.ColumnGoodsInDeficit];

    #endregion // Properties

    #region Methods

    /// <summary>
    /// Tells whether no point of a view holds a value.
    /// </summary>
    /// <param name="view">The view</param>
    /// <returns><see langword="true"/> when no point holds a value</returns>
    private static bool HoldsNoValue(TrendView view)
    {
        return view.Points.All(point => point.Values.All(value => value is null));
    }

    /// <summary>
    /// Gives the smallest distance between two labels of the time axis that divides a span into few enough intervals.
    /// </summary>
    /// <param name="span">The time the chart shows</param>
    /// <returns>The distance</returns>
    private static TimeSpan LabelSpacing(TimeSpan span)
    {
        return _timeLabelSpacings.FirstOrDefault(spacing => span / spacing <= MaxLabelIntervals, _timeLabelSpacings[^1]);
    }

    /// <summary>
    /// Creates the options of a chart: only number and time text reaches the axis labels, which are written as markup.
    /// </summary>
    private void UpdateOptions()
    {
        var spacing = LabelSpacing(_drawn is { } drawn ? drawn.Last - drawn.First : TimeSpan.FromDays(1));
        var key = (Kind, Theme, Language, spacing);

        if (_optionsKey == key)
        {
            return;
        }

        _optionsKey = key;
        _palette = [.. TrendPalette.For(Kind, Theme)];
        _options = new TimeSeriesChartOptions
                   {
                       ChartPalette = _palette,
                       TimeLabelFormat = TimeLabelFormat,
                       TimeLabelSpacing = spacing,
                       TimeLabelSpacingRounding = true,
                       TooltipTimeLabelFormat = TooltipTimeFormat,
                       YAxisToStringFunc = FormatAxisValue,
                       ShowDataMarkers = false,
                       ShowToolTips = true,
                       ShowLegend = true
                   };
    }

    /// <summary>
    /// Formats a value of the value axis: a number and nothing else.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>The text</returns>
    private string FormatAxisValue(double value)
    {
        return Kind == TrendKind.Good || value % 1 != 0
                   ? PageFormat.Rate(value, Language)
                   : PageFormat.Count((long)value);
    }

    /// <summary>
    /// Formats a value of a line.
    /// </summary>
    /// <param name="line">The index of the line</param>
    /// <param name="value">The value, or <see langword="null"/></param>
    /// <returns>The text</returns>
    private string FormatValue(int line, double? value)
    {
        if (Kind == TrendKind.Figures)
        {
            return PageFormat.Count(value is { } count ? (long)count : null);
        }

        return line == 2
                   ? PageFormat.Delta(value, Language)
                   : PageFormat.Rate(value, Language);
    }

    /// <summary>
    /// Gives the lines of text of the tooltip of a hovered point.
    /// </summary>
    /// <param name="segment">The hovered point</param>
    /// <returns>The lines, none when the point is unknown</returns>
    private List<string> TooltipLines(SvgPath segment)
    {
        if (_drawn is not { } drawn
            || DateTime.TryParseExact(segment.LabelXValue, TooltipTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) == false
            || drawn.PointsByTime.TryGetValue(time, out var point) == false)
        {
            return [];
        }

        var names = LineNames;
        var lines = new List<string>
                    {
                        PageFormat.Time(point.Time, drawn.OffsetMinutes)
                    };

        if (point.IsHourly)
        {
            lines.Add(Texts.HourlyAverage);
        }

        lines.AddRange(names.Select((name, line) => $"{name}: {FormatValue(line, point.Values[line])}"));

        return lines;
    }

    /// <summary>
    /// Tells whether a hovered point lies in the right half of the time axis.
    /// </summary>
    /// <param name="segment">The hovered point</param>
    /// <returns><see langword="true"/> when the point is in the right half</returns>
    private bool IsInRightHalf(SvgPath segment)
    {
        if (_drawn is not { } drawn
            || DateTime.TryParseExact(segment.LabelXValue, TooltipTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) == false
            || drawn.Last <= drawn.First)
        {
            return false;
        }

        return time - drawn.First > (drawn.Last - drawn.First) / 2;
    }

    /// <summary>
    /// Renders the tooltip of a hovered point through the render tree, so no name or value is written as markup.
    /// </summary>
    /// <param name="context">The hovered point and the colour of its line</param>
    /// <returns>The tooltip</returns>
    private RenderFragment RenderTooltip((SvgPath Segment, string Color) context)
    {
        var lines = TooltipLines(context.Segment);

        var alignRight = IsInRightHalf(context.Segment);
        var below = context.Segment.LabelY < BelowLimit;

        return builder =>
               {
                   builder.OpenComponent<TrendTooltip>(0);
                   builder.AddComponentParameter(1, nameof(TrendTooltip.Lines), lines);
                   builder.AddComponentParameter(2, nameof(TrendTooltip.Color), context.Color);
                   builder.AddComponentParameter(3, nameof(TrendTooltip.AlignRight), alignRight);
                   builder.AddComponentParameter(4, nameof(TrendTooltip.Below), below);
                   builder.CloseComponent();
               };
    }

    /// <summary>
    /// Builds the drawn chart of a read at an offset.
    /// </summary>
    /// <param name="read">The read</param>
    /// <param name="readNumber">The number of the read</param>
    /// <param name="offsetMinutes">The UTC offset of the browser in minutes, or <see langword="null"/> for UTC</param>
    /// <returns>The chart</returns>
    private DrawnTrend CreateDrawn(TrendRead read, int readNumber, int? offsetMinutes)
    {
        var view = read.View!;
        var pointsByTime = new Dictionary<DateTime, TrendPoint>();

        foreach (var point in view.Points)
        {
            pointsByTime.TryAdd(TrendSeriesBuilder.LocalTime(point.Time, offsetMinutes), point);
        }

        return new DrawnTrend
               {
                   Read = read,
                   ReadNumber = readNumber,
                   OffsetMinutes = offsetMinutes,
                   PointsByTime = pointsByTime,
                   First = pointsByTime.Keys.Min(),
                   Last = pointsByTime.Keys.Max(),
                   Series = [.. TrendSeriesBuilder.Build(view, LineNames, offsetMinutes)],
                   SeriesTexts = Texts
               };
    }

    /// <summary>
    /// Asks the browser for its UTC offset at an instant, once per instant.
    /// </summary>
    /// <param name="instant">The instant</param>
    /// <returns>The offset, or <see langword="null"/> for none</returns>
    private async Task<int?> AskOffsetAsync(DateTimeOffset? instant)
    {
        if (instant is not { } asked)
        {
            return null;
        }

        if (asked != _askedInstant)
        {
            _askedOffset = await BrowserOffset.AskAsync(JsRuntime, asked, _cancellation.Token).ConfigureAwait(true);
            _askedInstant = asked;
        }

        return _askedOffset;
    }

    #endregion // Methods

    #region ComponentBase

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (ReferenceEquals(Read, _lastRead) == false)
        {
            _lastRead = Read;
            _hasNoValues = Read?.View is { } view && HoldsNoValue(view);
        }

        if (Read?.View is null || _hasNoValues)
        {
            _drawn = null;
        }
        else if (_drawn is { } drawn && ReferenceEquals(drawn.SeriesTexts, Texts) == false)
        {
            _drawn = CreateDrawn(drawn.Read, drawn.ReadNumber, drawn.OffsetMinutes);
            _isRebuildPending = true;
        }

        UpdateOptions();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_isRebuildPending && _chart is not null)
        {
            _isRebuildPending = false;
            _chart.RebuildChart();

            StateHasChanged();
        }

        if (_isAsking)
        {
            return;
        }

        _isAsking = true;

        try
        {
            while (_isDisposed == false && Read is { IsFailed: false, View: not null } read && _hasNoValues == false && ReferenceEquals(read, _drawn?.Read) == false)
            {
                var readNumber = ReadNumber;
                var offset = await AskOffsetAsync(AsOf).ConfigureAwait(true);

                if (_isDisposed || ReferenceEquals(read, Read) == false)
                {
                    continue;
                }

                _drawn = CreateDrawn(read, readNumber, offset);

                UpdateOptions();
                StateHasChanged();
            }
        }
        finally
        {
            _isAsking = false;
        }
    }

    #endregion // ComponentBase

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        _isDisposed = true;
        _cancellation.Cancel();
        _cancellation.Dispose();
    }

    #endregion // IDisposable
}