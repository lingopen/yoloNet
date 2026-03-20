using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace yoloNetv2.Controls; 
public partial class LoadingIndicator : UserControl
{
    public LoadingIndicator()
    {
        InitializeComponent();
    }

    // 提示文字属性
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<LoadingIndicator, string>(nameof(Text), defaultValue: "正在加载中，请稍后...");

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    // 背景色属性
    public new static readonly StyledProperty<IBrush> BackgroundProperty =
        AvaloniaProperty.Register<LoadingIndicator, IBrush>(nameof(Background), defaultValue: new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)));

    public new IBrush Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    // 文字颜色属性
    public static readonly StyledProperty<IBrush> TextColorProperty =
        AvaloniaProperty.Register<LoadingIndicator, IBrush>(nameof(TextColor), defaultValue: Brushes.White);

    public IBrush TextColor
    {
        get => GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }
}