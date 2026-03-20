using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using yoloNetv2.Controls;
using yoloNetv2.ViewModels;

namespace yoloNetv2;

public partial class RtspView : UserControl
{
    private readonly RtspViewModel? _vm;
    public RtspView()
    {
        InitializeComponent(); 
        _vm = App.ServiceProvider?.GetRequiredService<RtspViewModel>();
        this.DataContext = _vm;
    }
    protected override  void OnInitialized()
    {
        VideoHelper.UICanvas = this.canvas;
        if (_vm != null)
              _vm.OnInit();
        base.OnInitialized();
    }
}