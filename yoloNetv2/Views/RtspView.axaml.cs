using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
    protected override  async void OnLoaded(RoutedEventArgs e)
    {
        VideoHelper.UICanvas = this.canvas;
        if (_vm != null)
             await  _vm.OnInit();
        base.OnInitialized();
    }
   
}