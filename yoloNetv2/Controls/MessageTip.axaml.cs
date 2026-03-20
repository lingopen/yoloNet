using Avalonia;
using Avalonia.Controls;
using Avalonia.Media; 

namespace yoloNetv2.Controls; 
public partial class MessageTip : UserControl
{
    public MessageTip()
    {
        InitializeComponent();
    }

    // 提示文字属性
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<LoadingIndicator, string>(nameof(Text), defaultValue: "就绪");

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    // 背景色属性
    public new static readonly StyledProperty<IBrush> TextBackgoundProperty =
        AvaloniaProperty.Register<LoadingIndicator, IBrush>(nameof(Background), defaultValue: new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)));

    public new IBrush TextBackgound
    {
        get => GetValue(TextBackgoundProperty);
        set => SetValue(TextBackgoundProperty, value);
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