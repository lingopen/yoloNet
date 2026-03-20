using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
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
using Microsoft.ML.OnnxRuntime;
using yoloNetv2.Controls;

namespace yoloNetv2.ViewModels;

public partial class PreViewModel : ViewModelBase
{ 
    [ObservableProperty] private string? _onnxPath;
    [ObservableProperty] private bool _isRunning = false;
    [ObservableProperty]
    ObservableCollection<string>? _devices = new ObservableCollection<string>();
    /// <summary>
    /// 取消任务
    /// </summary>
    CancellationTokenSource? _cts;
    /// <summary>
    /// yolo帮助类
    /// </summary>
    YoloService? _yolo;

    public void OnInit()
    {
        var list = VideoHelper.GetDevices();
        Devices?.Clear();
        foreach (var item in list)
        {
            Devices!.Add($"{item.Description}");
        }
    }



    [RelayCommand]
    async Task SelectOnnxFile()
    {
        if (App.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow == null)
        {
            await ShowTip((false, "无法获取主窗口"));
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




    [RelayCommand]
    public async Task Start(int index)
    {
        if (IsRunning)
        {
            _cts?.Cancel();
            IsRunning = false;

            if (VideoHelper.IsInit)
            {
                await VideoHelper.UnInit();
            }
            await Task.Delay(200);//休眠200ms 
            return;
        }
        try
        {
            VideoHelper.Init(index);
        }
        catch (Exception)
        {

            return;
        }

        if (!VideoHelper.IsInit)
        {
            IsRunning = false;
            return;
        }
        else
        {
            await VideoHelper.Start(0);
            IsRunning = VideoHelper.IsRunning; 


            //_cts = new CancellationTokenSource();
            //_yolo = new YoloService();
            //_ = Task.Run(() => CaptureLoop(_cts.Token));
        } 
    }


//    void CaptureLoop(CancellationToken token)
//    {
//        DateTime lastFpsTime = DateTime.Now;
//        var now = DateTime.Now;
//        int realFrameCounter = 0;
//        int onnxFrameCounter = 0;
//        object frameLock = new object();
//        Mat latestFrame = new Mat();
//        Mat _frame = new Mat();
//        Image<Bgr, byte>? displayFrame = null;
//        Image<Bgr, byte>? copyFrame = null;
//        bool isInferencing = false;
//        List<DetectedBox> lastBoxes = new List<DetectedBox>();
//        double scale = 1;
//        int offset_x = 0, offset_y = 0;
//        InferenceSession? _session = null;
//        if (!string.IsNullOrEmpty(OnnxPath)) //加载模型
//        {

//            var options = new SessionOptions();
//            try
//            {
//                options.AppendExecutionProvider_CUDA(0);
//                //options.LogSeverityLevel =  OrtLoggingLevel.ORT_LOGGING_LEVEL_INFO; // 打印详细信息
//                Console.WriteLine("CUDA Execution Provider added successfully.");
//            }
//            catch (OnnxRuntimeException ex)
//            {
//                Console.WriteLine("CUDA Execution Provider failed: " + ex.Message);
//                Console.WriteLine("Fallback to CPU.");
//            }
//            // 创建 InferenceSession
//            _session = new InferenceSession(OnnxPath, options);
//        }
//        // 后台线程抓帧
//        _ = Task.Run(() =>
//        {
//            while (!token.IsCancellationRequested)
//            {
//                if (_capture == null) continue;
//                if (_capture.Grab())
//                {
//                    _capture.Retrieve(_frame);
//                    lock (frameLock)
//                    {
//                        latestFrame?.Dispose();
//                        latestFrame = _frame.Clone();
//                        realFrameCounter++;
//                    }
//                }
//            }
//        }, token);


//        while (!token.IsCancellationRequested)
//        {
//            if (_yolo == null || _canvas == null) continue;

//            lock (frameLock)
//            {
//                if (latestFrame.IsEmpty) continue;

//                displayFrame?.Dispose();
//                displayFrame = latestFrame.ToImage<Bgr, byte>();
//                copyFrame = _yolo.Letterbox(latestFrame.ToImage<Bgr, byte>(), out scale, out offset_x, out offset_y);
//            }

//            // 异步推理
//            if (_session != null && copyFrame != null && !isInferencing)
//            {
//                isInferencing = true;
//                onnxFrameCounter++;
//                var tensor = _yolo.MatToTensor(copyFrame);
//                _ = Task.Run(() =>
//                 {
//#pragma warning disable CS0618 // 类型或成员已过时
//                     try
//                     {
//                         var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", tensor) };
//                         using var results = _session.Run(inputs);
//                         var boxes = _yolo.ParseOnnxOutput(results);
//                         lastBoxes = boxes;
//                     }
//                     catch (System.ExecutionEngineException ex)
//                     {
//                         Console.WriteLine($"推理异常: {ex.Message}");
//                     }
//                     catch (Exception ex)
//                     {
//                         Console.WriteLine($"推理异常: {ex.Message}");
//                     }
//                     finally
//                     {
//                         isInferencing = false;
//                         copyFrame?.Dispose();
//                     }
//#pragma warning restore CS0618 // 类型或成员已过时
//                 });
//            }

//            // 绘制结果
//            foreach (var box in lastBoxes)
//            {
//                CvInvoke.Rectangle(displayFrame,
//                    new Rectangle((int)box.X1 - offset_x, (int)box.Y1 - offset_y, (int)(box.X2 - box.X1), (int)(box.Y2 - box.Y1)),
//                    new MCvScalar(0, 0, 255), 2);
//                CvInvoke.PutText(displayFrame, box.Label, new System.Drawing.Point((int)box.X1 - offset_x, (int)box.Y1 - offset_y - 5),
//                    FontFace.HersheySimplex, 0.5, new MCvScalar(255, 255, 255));
//            }

//            // FPS 计算 
//            now = DateTime.Now;
//            var span = now - lastFpsTime;
//            if (span.TotalMilliseconds >= 1000)
//            {
//                Fps = $"{realFrameCounter / span.TotalSeconds:F2}";
//                OnnxFps = $"{onnxFrameCounter / span.TotalSeconds:F2}";
//                realFrameCounter = 0;
//                onnxFrameCounter = 0;
//                lastFpsTime = now;
//            }
//            //✅ 刷新WriteableBitmap
//            displayFrame?.ConvertToWriteableBitmap(_bitmap);

//            Dispatcher.UIThread.Post(() =>
//             {
//                 // 显示  
//                 _canvas?.InvalidateVisual();
//             });
//        }
//    }
}

