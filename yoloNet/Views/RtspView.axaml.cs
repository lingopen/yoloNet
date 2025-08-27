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

        //�����϶�
        //root.PointerPressed += (sender, e) =>
        //{
        //    if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        //    {
        //        // �ҵ���� UserControl ���ڵ� Window
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