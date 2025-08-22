using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using yoloNet.ViewModels;

namespace yoloNet;

public partial class PreView : UserControl
{
    private readonly PreViewModel? _vm;
    public PreView()
    {
        InitializeComponent();

         
        _vm = App.ServiceProvider?.GetRequiredService<PreViewModel>();
        this.DataContext = _vm;
    }
    protected override async void OnInitialized()
    {
        base.OnInitialized();
        if (_vm != null)
            await _vm.OnInit(canvas);
    }
}