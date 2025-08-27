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
        Title = "��ʾ";
        txtMsg.Text = "û����Ϣ";
    }
    public MessageBox(string message, string title = "��ʾ")
    {
        InitializeComponent();
        Title = title;
        txtMsg.Text = message;
        
         
    }
    //�����϶�
    private void Border_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // ������ʱ�������϶�
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }
    public static async Task Show(string message, string title = "��ʾ")
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