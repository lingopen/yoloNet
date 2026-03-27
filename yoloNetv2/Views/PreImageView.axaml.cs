using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using yoloNetv2.Controls;
using yoloNetv2.ViewModels;

namespace yoloNetv2;

public partial class PreImageView : UserControl
{
    private readonly PreImageViewModel? _vm;
    private Point? _startPoint;

    public PreImageView()
    {
        InitializeComponent();
        _vm = App.ServiceProvider?.GetRequiredService<PreImageViewModel>();
        this.DataContext = _vm; 
        this.KeyDown += OnKeyDown; // 注册键盘事件
    }
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // 左箭头
        if (e.Key == Key.Left)
        {
            _vm?.PrevImageCommand.Execute("1");//  // 上一张
            e.Handled = true;
        }
        // 右箭头
        else if (e.Key == Key.Right)
        {
            _vm?.NextImageCommand.Execute("1");//下一张
            e.Handled = true;
        }
        //// Ctrl + S 保存
        //else if (e.Key == Key.S && (e.KeyModifiers & KeyModifiers.Control) == KeyModifiers.Control)
        //{
        //    _vm?.SaveAnnotationCommand.Execute(null);
        //    e.Handled = true;
        //}
    }
     
    protected override void OnLoaded(RoutedEventArgs e)
    {
        ImageHelper.UICanvas = this.canvas;
        if (_vm != null)
        {
            _vm.LoadAllImage();
            _vm.CurrentIndex = 0;
            _vm.LoadCurrentImage();
        }
        base.OnLoaded(e);
    }
}
