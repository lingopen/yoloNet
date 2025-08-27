using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

namespace yoloNet;

public partial class MessageBox : Window
{
    public MessageBox()
    {
        InitializeComponent();
        Title = "提示";
        txtMsg.Text = "没有消息";
    }
    public MessageBox(string message, string title = "提示")
    {
        InitializeComponent();
        Title = title;
        txtMsg.Text = message;
        
         
    }
    //窗体拖动
    private void Border_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // 鼠标左键时才允许拖动
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }
    public static async Task Show(string message, string title = "提示")
    {
        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var msgBox = new MessageBox(message, title);
            await msgBox.ShowDialog(desktop.MainWindow);
        }
    }

    private void Ok_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true);
    }
    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }
}