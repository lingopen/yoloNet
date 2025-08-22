using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Emgu.CV.ML;
using Microsoft.Extensions.DependencyInjection;
using yoloNet.ViewModels;

namespace yoloNet;

public partial class HomeView : UserControl
{
    HomeViewModel? _vm;
    public HomeView()
    {
        InitializeComponent();
        _vm = App.ServiceProvider?.GetRequiredService<HomeViewModel>();
        this.DataContext = _vm;
    }
}