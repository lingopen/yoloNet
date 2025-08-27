using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using yoloNet.Controls;
using yoloNet.ViewModels;

namespace yoloNet;

public partial class TrainView : UserControl
{
    private readonly TrainViewModel? _vm;
    private Point? _startPoint;

    public TrainView()
    {
        InitializeComponent();
        _vm = App.ServiceProvider?.GetRequiredService<TrainViewModel>();
        this.DataContext = _vm;

        // ���� DataContext �� Log ���Ա仯
        if (_vm != null)
            _vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(_vm.Log))
                    {
                        // �������ײ�
                        Dispatcher.UIThread.Post(() =>
                        {
                            LogScrollViewer.ScrollToEnd();
                        });
                    }
                };
    }
}
