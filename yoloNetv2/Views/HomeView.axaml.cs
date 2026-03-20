using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using yoloNetv2.ViewModels;

namespace yoloNetv2;

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