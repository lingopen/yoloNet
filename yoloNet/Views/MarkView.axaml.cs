using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using yoloNet.Controls;
using yoloNet.ViewModels;

namespace yoloNet;

public partial class MarkView : UserControl
{
    private readonly MarkViewModel? _vm;
    private Point? _startPoint;

    public MarkView()
    {
        InitializeComponent();
        _vm = App.ServiceProvider?.GetRequiredService<MarkViewModel>();
        this.DataContext = _vm;


        if (ImageCanvas is AnnotatedCanvas canvas)
        {
            canvas.ViewModel = _vm; 
        }
        // ?? ��ˢ���¼�
        _vm.RequestInvalidate = () => ImageCanvas.InvalidateVisual();
        // ע������¼�
        ImageCanvas.PointerPressed += OnCanvasPressed;
        ImageCanvas.PointerReleased += OnCanvasReleased;
        ImageCanvas.PointerMoved += OnCanvasMoved;


        this.KeyDown += OnKeyDown; // ע������¼�
    }
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // ���ͷ
        if (e.Key == Key.Left)
        {
            _vm?.PrevImageCommand.Execute("1");//  // ��һ��
            e.Handled = true;
        }
        // �Ҽ�ͷ
        else if (e.Key == Key.Right)
        {
            _vm?.NextImageCommand.Execute("1");//��һ��
            e.Handled = true;
        }
        // Ctrl + S ����
        else if (e.Key == Key.S && (e.KeyModifiers & KeyModifiers.Control) == KeyModifiers.Control)
        {
            _vm?.SaveAnnotationCommand.Execute(null);
            e.Handled = true;
        }
    }
    private void OnCanvasPressed(object sender, PointerPressedEventArgs e)
    {
        _startPoint = e.GetPosition(ImageCanvas);
    }

    private void OnCanvasReleased(object sender, PointerReleasedEventArgs e)
    {
        if (_vm != null && _startPoint.HasValue)
        {
            var end = e.GetPosition(ImageCanvas);
            var rect = new Rect(_startPoint.Value, end);
            _vm.AddAnnotation(rect);
            _startPoint = null;
            _vm.TempRect = null;
            ImageCanvas.InvalidateVisual();
        }
    }

    private void OnCanvasMoved(object sender, PointerEventArgs e)
    {
        if (_vm != null && _startPoint.HasValue)
        {
            var pos = e.GetPosition(ImageCanvas);
            _vm.TempRect = new Rect(_startPoint.Value, pos);
            ImageCanvas.InvalidateVisual();
        }
    }
}
