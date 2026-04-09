using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.ML.OnnxRuntime;
using SkiaSharp;

namespace yoloNetv2.Controls
{
    /// <summary>
    /// 图片帮助类
    /// </summary>
    public static class ImageHelper
    {
        // 🔹 UI显示控件
        public static ImageDisplayControl? UICanvas { get; set; }

        // 🔹 显示缓存
        private static WriteableBitmap? _bitmap;
        private static byte[]? _pixelBuffer;



        // 🔹 YOLO 推理
        private static InferenceSession? _session = null;
        private static int isInferencingFlag = 0;

        // 🔹 上一次检测结果
        private static List<Rect> _lastDetectedFaces = new List<Rect>();
        private static string _lastDetectedScore = "";

        // 🔹 推理计时器
        private static Stopwatch _inferenceStopwatch = new();

        // 🔹 初始化算法
        public static bool Init(string? onnxPath = null)
        {
            try
            {
                // 🔹 加载 ONNX 模型
                if (!string.IsNullOrEmpty(onnxPath))
                {
                    var options = new SessionOptions();
                    try
                    {
                        options.AppendExecutionProvider_CPU();
                        //options.AppendExecutionProvider_CUDA(1);
                        Console.WriteLine("CUDA Execution Provider added successfully.");
                    }
                    catch (OnnxRuntimeException ex)
                    {
                        Console.WriteLine("CUDA Execution Provider failed: " + ex.Message);
                        Console.WriteLine("Fallback to CPU.");
                    }
                    _session = new InferenceSession(onnxPath, options);
                }
                if (UICanvas == null) return false;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }
        static bool _rknnInitialized = false;


        // 🔹 
        public static void UnInit()
        {
            _inferenceStopwatch.Stop();

            if (_rknnInitialized)
            {
                RKNNHelper.Release();
                _rknnInitialized = false;
            }
        }
        public static void OnDraw(SKBitmap sourceBitmap)
        {
            try
            {
                int width = sourceBitmap.Width;
                int height = sourceBitmap.Height;
                // 🔹 初始化显示缓存（WriteableBitmap + pixelBuffer）
                if (_bitmap == null || _bitmap.PixelSize.Width != width || _bitmap.PixelSize.Height != height)
                {

                    _bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888);
                    _pixelBuffer = new byte[width * height * 4];
                    if (UICanvas != null)
                    {
                        UICanvas.Width = width;
                        UICanvas.Height = height;
                    }
                }

                using (var pix = sourceBitmap.PeekPixels())
                {
                    Marshal.Copy(pix.GetPixels(), _pixelBuffer!, 0, _pixelBuffer!.Length);

                    if (_session != null)
                    {
                        _inferenceStopwatch.Restart();
                        int srcW, srcH;
                        // 假设 bufferScope.Buffer 获取到 RGB byte[]，长度 = width * height * 3
                        byte[] rgbPixels = OnnxHelper.DecodeJpegToRGB(sourceBitmap, out srcW, out srcH);



                        try
                        {
                            // 🔹 模型训练输入尺寸
                            int modelInputSize = width; // 或 640，跟你的 yolo11n.pt 训练尺寸保持一致
                            var tensor = OnnxHelper.FillTensorWithLetterbox(rgbPixels, srcW, srcH, modelInputSize);

                            var onnxInput = NamedOnnxValue.CreateFromTensor("images", tensor);
                            using var results = _session.Run(new[] { onnxInput });
                            var boxes = OnnxHelper.ParseYoloOutputForNoClass(results, srcW, srcH, modelInputSize);

                            if (boxes.Any())
                            {
                                _lastDetectedFaces.Clear();
                                foreach (var box in boxes)
                                {
                                    _lastDetectedFaces.Add(new Rect(box.X1, box.Y1, box.X2 - box.X1, box.Y2 - box.Y1));
                                }
                                _lastDetectedScore = $"最高 {boxes.Max(p => p.Score).ToString("N2")} 最低 {boxes.Min(p => p.Score).ToString("N2")} 检测到 {boxes.Count()}个 耗时 {_inferenceStopwatch.ElapsedMilliseconds}ms";
                            }
                            else
                            {
                                _lastDetectedFaces.Clear();
                                _lastDetectedScore = $"未检测到目标 | {_inferenceStopwatch.ElapsedMilliseconds}ms";
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"推理异常: {ex.Message}");
                        }


                    }
                }

            }
            finally { }

            // 🔹 更新 UI
            if (UICanvas != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_pixelBuffer == null || _pixelBuffer.Length <= 0) return;
                    using var fb = _bitmap!.Lock();
                    Marshal.Copy(_pixelBuffer, 0, fb.Address, _pixelBuffer.Length);
                    UICanvas.UpdateFrame(_bitmap!, _lastDetectedFaces, _lastDetectedScore);
                });
            }
            // 🔹 主线程释放
            sourceBitmap.Dispose();

        }


        // 初始化 NPU 
        public static bool Init_RKNN(string modelPath)
        {
            if (string.IsNullOrEmpty(modelPath)) return false;

            try
            {
                RKNNHelper.Init(modelPath);
                _rknnInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("RKNN Init failed: " + ex.Message);
                return false;
            }
        }



        public static void OnDraw_RKNN(SKBitmap sourceBitmap, int modelInputSize = 320, float confThreshold = 0.3f, float iouThreshold = 0.5f)
        {
            try
            {
                int width = sourceBitmap.Width;
                int height = sourceBitmap.Height;

                // 🔹 初始化显示缓存（WriteableBitmap + pixelBuffer）
                if (_bitmap == null || _bitmap.PixelSize.Width != width || _bitmap.PixelSize.Height != height)
                {
                    _bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888);
                    _pixelBuffer = new byte[width * height * 4];

                    if (UICanvas != null)
                    {
                        UICanvas.Width = width;
                        UICanvas.Height = height;
                    }
                }

                // 🔹 拷贝源图到缓存
                using (var pix = sourceBitmap.PeekPixels())
                {
                    Marshal.Copy(pix.GetPixels(), _pixelBuffer!, 0, _pixelBuffer!.Length);

                    if (_rknnInitialized)
                    {
                        _inferenceStopwatch.Restart();
                        try
                        {
                            // 🔹 推理
                            var boxes = RKNNHelper.Run(sourceBitmap, modelInputSize, confThreshold, iouThreshold);
                            // 🔹 打印 boxes 日志
                            //if (boxes != null && boxes.Length > 0)
                            //{
                            //    Console.WriteLine($"检测到 {boxes.Length} 个目标：");
                            //    for (int i = 0; i < boxes.Length; i++)
                            //    {
                            //        var b = boxes[i];
                            //        Console.WriteLine(
                            //            $"Box[{i}] - X1:{b.X1:F2}, Y1:{b.Y1:F2}, X2:{b.X2:F2}, Y2:{b.Y2:F2}, Score:{b.Score:F3}, ClassId:{b.ClassId}");
                            //    }
                            //}
                            //else
                            //{
                            //    Console.WriteLine("未检测到目标");
                            //}

                            if (boxes != null && boxes.Length > 0)
                            {

                                _lastDetectedFaces.Clear();
                                foreach (var box in boxes)
                                {
                                    _lastDetectedFaces.Add(new Rect(box.X1, box.Y1, box.X2 - box.X1, box.Y2 - box.Y1));
                                }
                                _lastDetectedScore = $"最高 {boxes.Max(p => p.Score):N2} 最低 {boxes.Min(p => p.Score):N2} 检测到 {boxes.Length} 个 | {_inferenceStopwatch.ElapsedMilliseconds}ms";
                            }
                            else
                            {
                                _lastDetectedFaces.Clear();
                                _lastDetectedScore = $"未检测到目标 | {_inferenceStopwatch.ElapsedMilliseconds}ms";
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"RKNN 推理异常: {ex.Message}");
                            _lastDetectedFaces.Clear();
                            _lastDetectedScore = $"推理异常 | {_inferenceStopwatch.ElapsedMilliseconds}ms";
                        }
                    }
                }
            }
            finally { }

            // 🔹 更新 UI 显示
            if (UICanvas != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_pixelBuffer == null || _pixelBuffer.Length <= 0) return;

                    using var fb = _bitmap!.Lock();
                    Marshal.Copy(_pixelBuffer, 0, fb.Address, _pixelBuffer.Length);

                    UICanvas.UpdateFrame(_bitmap!, _lastDetectedFaces, _lastDetectedScore);
                });
            }

            sourceBitmap.Dispose();
        }
    }
}