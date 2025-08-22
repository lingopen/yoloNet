
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using yoloNet.Extentions;
using Point = System.Drawing.Point;

namespace yoloNet.ViewModels;

public partial class PreViewModel : ObservableObject
{


    [ObservableProperty] private double _fps;
    [ObservableProperty] private string? _onnxPath;
    [ObservableProperty] private int _frameCounter = 0;
    [ObservableProperty] private bool _isRunning = false;
    [ObservableProperty] private int _width = 0;
    [ObservableProperty] private int _height = 0;

    private InferenceSession? _session;
    private VideoCapture? _capture;
    private Canvas? _canvas;
    private WriteableBitmap? _bitmap;
    private Image<Bgr, byte>? _frame;
    private Image<Bgr, byte>? _displayFrame;

    private List<Extentions.DetectedBox> _lastBoxes = new List<Extentions.DetectedBox>();
    private int InputSize = 800;
    private const int InferenceInterval = 5;
    private bool _isInferencing = false;
    private DateTime _lastFpsTime = DateTime.Now;

    [RelayCommand]
    async Task SelectOnnxFile()
    {
        if (App.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow == null)
        {
            await MessageBox.Show("无法获取主窗口", "错误");
            return;
        }

        var file_options = new FilePickerOpenOptions
        {
            Title = "选择 ONNX 文件",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("ONNX 文件") { Patterns = new[] { "*.onnx" } } }
        };

        var result = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(file_options);
        if (result != null && result.Count > 0)
        {
            OnnxPath = result[0].Path.LocalPath;
            try
            {
                var options = new SessionOptions();
                options.AppendExecutionProvider_CPU(); // 明确使用 CPU
                _session?.Dispose();
                _session = new InferenceSession(OnnxPath, options);

                Debug.WriteLine("模型加载成功！");
                Debug.WriteLine("模型输入信息：");
                foreach (var input in _session.InputMetadata)
                {
                    Debug.WriteLine($"  Name: {input.Key}, Type: {input.Value.ElementType}, Shape: [{string.Join(",", input.Value.Dimensions)}]");
                }

                Debug.WriteLine("模型输出信息：");
                foreach (var output in _session.OutputMetadata)
                {
                    Debug.WriteLine($"  Name: {output.Key}, Type: {output.Value.ElementType}, Shape: [{string.Join(",", output.Value.Dimensions)}]");
                }
            }
            catch (Exception ex)
            {
                OnnxPath = $"加载模型失败: {ex.Message}";
                _session = null;
            }
        }
    }


    public async Task OnInit(Canvas canvas)
    {
        _canvas = canvas;
        await Task.CompletedTask;
    }


    [RelayCommand]
    public void Start(string rtspUrl)
    {
        if (IsRunning)
        {
            StopCapture();
            return;
        }

        if (_canvas == null) return;

        FrameCounter = 0;
        _capture = new VideoCapture(rtspUrl, VideoCapture.API.Ffmpeg);
        if (!_capture.IsOpened)
        {
            IsRunning = false;
            return;
        }

        Width = (int)_capture.Get(CapProp.FrameWidth);
        Height = (int)_capture.Get(CapProp.FrameHeight);
        Fps = _capture.Get(CapProp.Fps);

        _canvas.Width = Width;
        _canvas.Height = Width; // 保持正方形显示

        _bitmap?.Dispose();
        _bitmap = new WriteableBitmap(new PixelSize(Width, Width), new Vector(96, 96),
                                      PixelFormat.Bgra8888, AlphaFormat.Premul);
        _canvas.Background = new ImageBrush { Source = _bitmap };

        _capture.ImageGrabbed += _capture_ImageGrabbed;
        _capture.Start();
        IsRunning = true;
    }

    private void StopCapture()
    {
        IsRunning = false;
        if (_capture != null)
        {
            _capture.Stop();
            _capture.ImageGrabbed -= _capture_ImageGrabbed;
            _capture.Dispose();
            _capture = null;
        }

        _frame?.Dispose();
        _displayFrame?.Dispose();
        _frame = null;
        _displayFrame = null;
    }



    private void _capture_ImageGrabbed(object? sender, EventArgs e)
    {
        if (_capture == null || _bitmap == null || _canvas == null) return;

        _frame = _capture.QueryFrame()?.ToImage<Bgr, byte>();
        if (_frame == null) return;

        _displayFrame = Letterbox(_frame.Clone(), InputSize, InputSize);

        FrameCounter++;

        // 每隔 InferenceInterval 帧进行一次异步推理
        if (!_isInferencing && _session != null && FrameCounter % InferenceInterval == 0)
        {
            _isInferencing = true;
            var tensorFrame = _displayFrame.Clone();
            Task.Run(() =>
            {
                try
                {
                    var inputTensor = MatToTensor(tensorFrame);
                    var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", inputTensor) };
                    List<Extentions.DetectedBox> boxes;
                    using (var results = _session.Run(inputs))
                    {
                        boxes = YoloOnnxHelper.ParseOnnxOutput(results);// xyxy → X,Y,Width,Height
                    }

                    // 更新全局框列表
                    _lastBoxes = boxes;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ONNX 推理异常: {ex.Message}");
                }
                finally
                {
                    _isInferencing = false;
                    tensorFrame.Dispose();
                }
            });
        }

        // UI 刷新
        Dispatcher.UIThread.Post(() =>
        {
            if (_displayFrame != null)
            {
                // 绘制上次框
                foreach (var box in _lastBoxes)
                {
                    _displayFrame.Draw(new Rectangle((int)box.X1, (int)box.Y1, (int)(box.X2 - box.X1), (int)(box.Y2 - box.Y1)),
                       new Bgr(0, 0, 255), 2);
                    _displayFrame.Draw(box.Label, new System.Drawing.Point((int)box.X1, (int)box.Y1 - 5),
                                       Emgu.CV.CvEnum.FontFace.HersheySimplex, 0.5, new Bgr(255, 255, 255));
                }

                _displayFrame.ConvertToWriteableBitmap(_bitmap, _canvas);

                // FPS 计算
                var now = DateTime.Now;
                var span = now - _lastFpsTime;
                if (span.TotalMilliseconds >= 500)
                {
                    Fps = FrameCounter / span.TotalSeconds;
                    FrameCounter = 0;
                    _lastFpsTime = now;
                }
            }
        });
    }

    #region ONNX Helper

    private Image<Bgr, byte> Letterbox(Image<Bgr, byte> src, int targetW = 320, int targetH = 320)
    {
        int w = src.Width;
        int h = src.Height;
        double scale = Math.Min((double)targetW / w, (double)targetH / h);
        int newW = (int)(w * scale);
        int newH = (int)(h * scale);

        var resized = src.Resize(newW, newH, Inter.Linear);
        var output = new Image<Bgr, byte>(targetW, targetH, new Bgr(0, 0, 0));

        int x = (targetW - newW) / 2;
        int y = (targetH - newH) / 2;
        resized.CopyTo(output.GetSubRect(new Rectangle(x, y, newW, newH)));
        return output;
    }

    private static Tensor<float> MatToTensor(Image<Bgr, byte> mat)
    {
        int h = mat.Height;
        int w = mat.Width;
        var tensor = new DenseTensor<float>(new[] { 1, 3, h, w });
        var data = mat.Data;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                tensor[0, 0, y, x] = data[y, x, 2] / 255f; // R
                tensor[0, 1, y, x] = data[y, x, 1] / 255f; // G
                tensor[0, 2, y, x] = data[y, x, 0] / 255f; // B
            }
        }
        return tensor;
    }

    private List<DetectedBox> ParseOnnxOutput(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results)
    {
        //var boxes = new List<DetectedBox>();
        //var output = results.First().AsTensor<float>();
        //int numBoxes = output.Dimensions[1];

        //for (int i = 0; i < numBoxes; i++)
        //{
        //    float score = output[0, i, 4];
        //    if (score < 50f) continue;
        //    boxes.Add(new DetectedBox
        //    {
        //        X = output[0, i, 0],
        //        Y = output[0, i, 1],
        //        Width = output[0, i, 2],
        //        Height = output[0, i, 3],
        //        Label = $"cls:{(int)output[0, i, 5]} score:{score:F2}"
        //    });
        //}

        //return boxes;



        var boxes = new List<DetectedBox>();
        var output = results.First().AsTensor<float>();
        int numBoxes = output.Dimensions[1];

        for (int i = 0; i < numBoxes; i++)
        {
            float score = output[0, i, 4];
            if (score < 50.0f) continue; // 根据你的阈值

            float x1 = output[0, i, 0];
            float y1 = output[0, i, 1];
            float x2 = output[0, i, 2];
            float y2 = output[0, i, 3];

            boxes.Add(new DetectedBox
            {
                X = x1,
                Y = y1,
                Width = x2 - x1,
                Height = y2 - y1,
                Label = $"cls:{(int)output[0, i, 5]} score:{score:F2}"
            });
        }

        return boxes;

    }

    private class DetectedBox
    {
        public float X, Y, Width, Height;
        public string Label = string.Empty;
    }

    #endregion
}

