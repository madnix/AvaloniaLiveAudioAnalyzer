using Avalonia;
using Avalonia.Controls.Primitives;

namespace AvaloniaLiveAudioAnalyzer;

public class AppDefaultStyles : TemplatedControl
{
    public static readonly StyledProperty<string> LargeTextProperty =
        AvaloniaProperty.Register<AppDefaultStyles, string>(
            nameof(LargeText),"Large Text");

    public string LargeText
    {
        get => GetValue(LargeTextProperty);
        set => SetValue(LargeTextProperty, value);
    }
    
    public static readonly StyledProperty<string> SmallTextProperty = AvaloniaProperty.Register<LargeLabelControl, string>(
        nameof(SmallText), "Small Text");
    
    public string SmallText
    {
        get => GetValue(SmallTextProperty);
        set => SetValue(SmallTextProperty, value);
    }
}