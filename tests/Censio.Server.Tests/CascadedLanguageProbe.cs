using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Censio.Server.Tests;

/// <summary>
/// A page stand-in that renders the language and the start page's navigation text it receives as cascaded values.
/// </summary>
public sealed class CascadedLanguageProbe : ComponentBase
{
    #region Properties

    /// <summary>
    /// Gets or sets the cascaded language.
    /// </summary>
    [CascadingParameter]
    public UiLanguage Language { get; set; }

    /// <summary>
    /// Gets or sets the cascaded texts, or <c>null</c> when none are cascaded.
    /// </summary>
    [CascadingParameter]
    public UiTexts? Texts { get; set; }

    #endregion // Properties

    #region ComponentBase

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", "probe-language");
        builder.AddContent(2, Language.ToString());
        builder.CloseElement();
        builder.OpenElement(3, "span");
        builder.AddAttribute(4, "class", "probe-home-entry");
        builder.AddContent(5, Texts?.HomeEntry);
        builder.CloseElement();
    }

    #endregion // ComponentBase
}