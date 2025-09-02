using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading;
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
using SkiaSharp;
using yoloNet.core;
using yoloNet.Extentions;
using static System.Net.Mime.MediaTypeNames;

namespace yoloNet.ViewModels;

public partial class PreViewModel : ObservableObject
{


    [ObservableProperty] private string? _fps;
    [ObservableProperty] private string? _onnxFps;
    [ObservableProperty] private string? _onnxPath;
    [ObservableProperty] private bool _isRunning = false;
    /// <summary>
    /// 显示
    /// </summary>
    Canvas? _canvas;
    /// <summary>
    /// 相机对象
    /// </summary>
    VideoCapture? _capture;
    /// <summary>
    /// 写入流对象
    /// </summary>
    WriteableBitmap? _bitmap;
    /// <summary>
    /// 取消任务
    /// </summary>
    CancellationTokenSource? _cts;
    /// <summary>
    /// yolo帮助类
    /// </summary>
    YoloService? _yolo;


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
            _cts?.Cancel();
            System.Threading.Thread.Sleep(200);//休眠200ms

            _bitmap?.Dispose();
            _capture?.Dispose();
            _capture = null;
            IsRunning = false;
            return;
        }
        if (rtspUrl.StartsWith("rtsp"))
            _capture = new VideoCapture(rtspUrl, VideoCapture.API.Ffmpeg);
        else if (!string.IsNullOrEmpty(rtspUrl))
        {
            try
            {
                var index = int.Parse(rtspUrl);
                _capture = new VideoCapture(index, VideoCapture.API.DShow);
                _capture.Set(CapProp.FourCC, VideoWriter.Fourcc('M', 'J', 'P', 'G'));
                _capture.Set(CapProp.FrameWidth, 1280);
                _capture.Set(CapProp.FrameHeight, 720);
            }
            catch (Exception)
            {

                return;
            }

        }
        else return;
        if (!_capture.IsOpened)
        {
            Debug.WriteLine("无法打开 RTSP 流");
            return;
        }

        int width = (int)_capture.Get(CapProp.FrameWidth);
        int height = (int)_capture.Get(CapProp.FrameHeight);

        Debug.WriteLine($"Video: {width}x{height}");

        if (_canvas == null)
        {
            Debug.WriteLine("Canvas对象为空");
            return;
        }
        _bitmap = new WriteableBitmap(new PixelSize(width, width), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        _canvas.Width = width;
        _canvas.Height = width;
        _canvas.Background = new ImageBrush { Source = _bitmap };


        _cts = new CancellationTokenSource();
        _yolo = new YoloService();
        Task.Run(() => CaptureLoop(_cts.Token));
        IsRunning = true;
    }


    void CaptureLoop(CancellationToken token)
    {
        DateTime lastFpsTime = DateTime.Now;
        var now = DateTime.Now;
        int realFrameCounter = 0;
        int onnxFrameCounter = 0;
        object frameLock = new object();
        Mat latestFrame = new Mat();
        Mat _frame = new Mat();
        Image<Bgr, byte>? displayFrame = null;
        bool isInferencing = false;
        List<DetectedBox> lastBoxes = new List<DetectedBox>();

        InferenceSession? _session = null;
        if (!string.IsNullOrEmpty(OnnxPath)) //加载模型
        {

            var options = new SessionOptions();
            try
            {
                options.AppendExecutionProvider_CUDA(0);
                //options.LogSeverityLevel =  OrtLoggingLevel.ORT_LOGGING_LEVEL_INFO; // 打印详细信息
                Console.WriteLine("CUDA Execution Provider added successfully.");
            }
            catch (OnnxRuntimeException ex)
            {
                Console.WriteLine("CUDA Execution Provider failed: " + ex.Message);
                Console.WriteLine("Fallback to CPU.");
            }
            // 创建 InferenceSession
            _session = new InferenceSession(OnnxPath, options);
        }
        // 后台线程抓帧
        _ = Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                if (_capture == null) continue;
                if (_capture.Grab())
                {
                    _capture.Retrieve(_frame);
                    lock (frameLock)
                    {
                        latestFrame?.Dispose();
                        latestFrame = _frame.Clone();
                        realFrameCounter++;
                    }
                }
            }
        }, token);


        while (!token.IsCancellationRequested)
        {
            if (_yolo == null || _canvas == null) continue;
            onnxFrameCounter++;
            lock (frameLock)
            {
                if (latestFrame.IsEmpty) continue;

                displayFrame?.Dispose();
                displayFrame = _yolo.Letterbox(latestFrame.ToImage<Bgr, byte>());
            }

            // 异步推理
            if (_session != null && displayFrame != null && !isInferencing)
            {
                isInferencing = true;
                var tensor = _yolo.MatToTensor(displayFrame);
                _ = Task.Run(() =>
                 {
                     try
                     {
                         var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", tensor) };
                         using var results = _session.Run(inputs);
                         var boxes = _yolo.ParseOnnxOutput(results);
                         lastBoxes = boxes;
                     }
                     catch (System.ExecutionEngineException ex)
                     {
                         Console.WriteLine($"推理异常: {ex.Message}");
                     }
                     catch (Exception ex)
                     {
                         Console.WriteLine($"推理异常: {ex.Message}");
                     }
                     finally
                     {
                         isInferencing = false;
                     }
                 });
            }

            // 绘制结果
            foreach (var box in lastBoxes)
            {
                CvInvoke.Rectangle(displayFrame,
                    new Rectangle((int)box.X1, (int)box.Y1, (int)(box.X2 - box.X1), (int)(box.Y2 - box.Y1)),
                    new MCvScalar(0, 0, 255), 2);
                CvInvoke.PutText(displayFrame, box.Label, new System.Drawing.Point((int)box.X1, (int)box.Y1 - 5),
                    FontFace.HersheySimplex, 0.5, new MCvScalar(255, 255, 255));
            }
            
            // FPS 计算 
            now = DateTime.Now;
            var span = now - lastFpsTime;
            if (span.TotalMilliseconds >= 1000)
            {
                Fps = $"{realFrameCounter / span.TotalSeconds:F2}";
                OnnxFps = $"{onnxFrameCounter / span.TotalSeconds:F2}";
                realFrameCounter = 0;
                onnxFrameCounter = 0;
                lastFpsTime = now;
            }
            //✅ 刷新WriteableBitmap
            displayFrame?.ConvertToWriteableBitmap(_bitmap);

            Dispatcher.UIThread.Post(() =>
             {
                 // 显示  
                 _canvas?.InvalidateVisual();
             });
        }
    }
}

