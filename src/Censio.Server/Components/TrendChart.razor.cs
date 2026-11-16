using Microsoft.AspNetCore.Components;

namespace Censio.Server.Components;

/// <summary>
/// A trend chart of a data page.
/// </summary>
public sealed partial class TrendChart
{
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

    #endregion // Properties
}