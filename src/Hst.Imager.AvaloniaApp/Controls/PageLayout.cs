using Avalonia;
using Avalonia.Controls;

namespace Hst.Imager.AvaloniaApp.Controls;

/// <summary>
/// Page layout with a fixed header (title and description), scrollable content using the full width
/// and a fixed action bar at the bottom. Template is defined in App.axaml.
/// </summary>
public class PageLayout : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<PageLayout, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<PageLayout, string?>(nameof(Description));

    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<PageLayout, object?>(nameof(Actions));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }
}
