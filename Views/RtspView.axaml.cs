using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using yoloNet.ViewModels;

namespace yoloNet;

public partial class RtspView : UserControl
{
    private readonly RtspViewModel? _vm;
    public RtspView()
    {
        InitializeComponent();

        //窗体拖动
        //root.PointerPressed += (sender, e) =>
        //{
        //    if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        //    {
        //        // 找到这个 UserControl 所在的 Window
        //        var win = this.FindAncestorOfType<Window>();
        //        win?.BeginMoveDrag(e);
        //    }
        //};

        _vm = App.ServiceProvider?.GetRequiredService<RtspViewModel>();
        this.DataContext = _vm;
    }
    protected override async void OnInitialized()
    {
        base.OnInitialized();
        if (_vm != null)
            await _vm.OnInit(canvas);
    }
}