using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using yoloNetv2.Controls;
using yoloNetv2.ViewModels;

namespace yoloNetv2;

public partial class PreView : UserControl
{
    private readonly PreViewModel? _vm;
    public PreView()
    {
        InitializeComponent();


        _vm = App.ServiceProvider?.GetRequiredService<PreViewModel>();
        this.DataContext = _vm;
    }
    protected override async void OnLoaded(RoutedEventArgs e)
    {
        VideoHelper.UICanvas = this.canvas;
        if (_vm != null)
            await _vm.OnInit();
        base.OnLoaded(e);
    } 
}