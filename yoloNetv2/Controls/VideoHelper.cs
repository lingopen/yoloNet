using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FlashCap;
using HarfBuzzSharp;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;
using yoloNetv2.Extentions;
using Vector = Avalonia.Vector;

namespace yoloNetv2.Controls
{
    /// <summary>
    /// 视频帮助类
    /// </summary>
    public static class VideoHelper
    {
        // 🔹 UI显示控件
        public static VideoDisplayControl? UICanvas { get; set; }

        // 🔹 摄像头设备及参数
        private static CaptureDeviceDescriptor? Device;
        public static VideoCharacteristics? Characteristics { get; set; }
        public static bool IsInit { get; private set; }
        public static bool IsRunning { get; private set; }

        //// 🔹 最后一帧图像流
        //public  Stream? ImageStream => _bitmap?.ToStream();

        // 🔹 帧处理计数与抽帧间隔
        private static double _interval = 0;
        private static int _frameCounter = 0;
        private static int _saveCount = 0;

        // 🔹 显示缓存
        private static WriteableBitmap? _bitmap;
        private static byte[]? _pixelBuffer;

        // 🔹 摄像头控制
        private static CaptureDevice? _captureDevice = null;

        // 🔹 FPS 统计
        private static Stopwatch _fpsStopwatch = new();
        private static long _frameCount = 0;
        private static double _currentFps = 0.0;

        // 🔹 原始像素数据
        private static ArraySegment<byte> image = new ArraySegment<byte>();

        // 🔹 YOLO 推理
        private static InferenceSession? _session = null;
        private static int isInferencingFlag = 0;

        // 🔹 上一次检测结果
        private static List<Rect> _lastDetectedFaces = new List<Rect>();
        private static string _lastDetectedScore = "";

        // 🔹 推理计时器
        private static Stopwatch _yoloStopwatch = new();

        #region ================== 扩展方法 ==================
        // 🔹 将 WriteableBitmap 转为 Stream
        public static Stream ToStream(this WriteableBitmap bitmap)
        {
            var stream = new MemoryStream();
            bitmap.Save(stream); // Avalonia 自带 Save(Stream)
            stream.Position = 0;
            return stream;
        }
        #endregion

        #region ================== 摄像头回调 ==================

        static SKBitmap? sourceBitmap = null;
        static int width = 0;
        static int height = 0;
        /// <summary>
        /// 相机回调
        /// </summary>
        /// <param name="bufferScope"></param>
        private static void OnPixelBufferArrivedAsync(PixelBufferScope bufferScope)
        {
            if (Characteristics == null) return;

            // 🔹 每秒计算一次 FPS
            _frameCount++;
            if (_fpsStopwatch.ElapsedMilliseconds >= 1000)
            {
                _currentFps = _frameCount / (_fpsStopwatch.ElapsedMilliseconds / 1000.0);
                _frameCount = 0;
                _fpsStopwatch.Restart();
            }
            try
            {
                width = Characteristics.Width;
                height = Characteristics.Height;
                image = bufferScope.Buffer.ReferImage();
                sourceBitmap = SKBitmap.Decode(image);
                // 🔹 初始化显示缓存（WriteableBitmap + pixelBuffer）
                if (_bitmap == null || _bitmap.PixelSize.Width != width || _bitmap.PixelSize.Height != height)
                {
                    //_bitmap?.Dispose(); 这里临时去掉，多次切换页面，会造成负载
                    _bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888);
                    _pixelBuffer = new byte[width * height * 4];

                }

                // 🔹 读取像素数据到 _pixelBuffer
                using (var pix = sourceBitmap.PeekPixels())
                {
                    Marshal.Copy(pix.GetPixels(), _pixelBuffer!, 0, _pixelBuffer!.Length);

                    // 🔹 抽帧保存
                    if (_interval > 0)
                    {
                        _frameCounter++;
                        if (_frameCounter % (int)_interval == 0)
                        {
                            var copy = sourceBitmap.Copy();
                            _ = Task.Run(() =>
                            {
                                _saveCount++;
                                copy?.SaveFrame(_saveCount);
                                copy?.Dispose();
                            });
                        }
                    }

                    // 🔹 异步 YOLO 推理，每帧独立 Tensor 避免多线程冲突
                    if (_session != null && Interlocked.CompareExchange(ref isInferencingFlag, 1, 0) == 0)
                    {
                        _yoloStopwatch.Restart();
                        int srcW, srcH;
                        // 假设 bufferScope.Buffer 获取到 RGB byte[]，长度 = width * height * 3
                        byte[] rgbPixels = TensorHelper.DecodeJpegToRGB(sourceBitmap, out srcW, out srcH);
                        if (rgbPixels == null || rgbPixels.Length == 0)
                        {
                            Interlocked.Exchange(ref isInferencingFlag, 0);
                            return;
                        }
                        _ = Task.Run(() =>
                        {

                            try
                            {
                                // 🔹 模型训练输入尺寸
                                int modelInputSize = width; // 或 640，跟你的 yolo11n.pt 训练尺寸保持一致
                                var tensor = TensorHelper.FillTensorWithLetterbox(rgbPixels, srcW, srcH, modelInputSize);

                                var onnxInput = NamedOnnxValue.CreateFromTensor("images", tensor);
                                using var results = _session.Run(new[] { onnxInput });
                                var boxes = TensorHelper.ParseYoloOutputForNoClass(results, srcW, srcH, modelInputSize);

                                if (boxes.Any())
                                {
                                    _lastDetectedFaces.Clear();
                                    foreach (var box in boxes)
                                    {
                                        _lastDetectedFaces.Add(new Rect(box.X1, box.Y1, box.X2 - box.X1, box.Y2 - box.Y1));
                                    }
                                    _lastDetectedScore = $"最高 {boxes.Max(p => p.Score).ToString("N2")} 最低 {boxes.Min(p => p.Score).ToString("N2")} 检测到 {boxes.Count()}个 耗时 {_yoloStopwatch.ElapsedMilliseconds}ms";
                                }
                                else
                                {
                                    _lastDetectedFaces.Clear();
                                    _lastDetectedScore = $"未检测到目标 | {_yoloStopwatch.ElapsedMilliseconds}ms";
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"推理异常: {ex.Message}");
                            }
                            finally
                            {
                                Interlocked.Exchange(ref isInferencingFlag, 0);
                            }

                        });
                    }
                }
            }
            finally
            {
                bufferScope.ReleaseNow();
            }


            // 🔹 更新 UI
            if (UICanvas != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_pixelBuffer == null || _pixelBuffer.Length <= 0) return;
                    using var fb = _bitmap!.Lock();
                    Marshal.Copy(_pixelBuffer, 0, fb.Address, _pixelBuffer.Length);
                    UICanvas.Fps = _currentFps;
                    UICanvas.SaveCount = _saveCount;
                    UICanvas.UpdateFrame(_bitmap!, _lastDetectedFaces, _lastDetectedScore);
                });
            }
            // 🔹 主线程释放
            sourceBitmap.Dispose();

        }
        #endregion
        #region ================== 公共方法 ==================
        // 🔹 获取可用摄像头列表
        public static List<CaptureDeviceDescriptor> GetDevices()
        {
            var devices = new CaptureDevices();
            return devices.EnumerateDescriptors().Where(d => d.Characteristics.Length >= 1).ToList();
        }
        // 🔹 初始化摄像头
        public static async Task Init(int index = 0, int characterIndex = -1)
        {
            if (IsInit) await UnInit();

            var devices = new CaptureDevices();
            var availableDevices = devices.EnumerateDescriptors().Where(d => d.Characteristics.Length >= 1).ToList();
            if (!availableDevices.Any()) return;

            if (index < 0) index = 0;
            Device = availableDevices[index];
            if (characterIndex < 0)
                Characteristics = Device?.Characteristics?.FirstOrDefault(c => c.Width == 640 && c.Height == 480 && c.PixelFormat == FlashCap.PixelFormats.JPEG);
            else
                Characteristics = Device?.Characteristics[characterIndex];
            if (Characteristics != null && UICanvas != null)
            {
                UICanvas.Width = Characteristics.Width;
                UICanvas.Height = Characteristics.Height;//直接显示正方形
            }
            IsInit = true;
        }
        // 🔹 启动摄像头 + 可选加载 ONNX 模型
        public static async Task<bool> Start(double interval = 0, string? onnxPath = null)
        {
            try
            {
                _interval = interval;
                _frameCounter = 0;
                _saveCount = 0;

                // 🔹 加载 ONNX 模型
                if (!string.IsNullOrEmpty(onnxPath))
                {
                    var options = new SessionOptions();
                    try
                    {
                        options.AppendExecutionProvider_CPU();
                        //options.AppendExecutionProvider_CUDA(0);
                        Console.WriteLine("CUDA Execution Provider added successfully.");
                    }
                    catch (OnnxRuntimeException ex)
                    {
                        Console.WriteLine("CUDA Execution Provider failed: " + ex.Message);
                        Console.WriteLine("Fallback to CPU.");
                    }
                    _session = new InferenceSession(onnxPath, options);
                }

                if (UICanvas == null || Device == null || Characteristics == null) return false;

                _fpsStopwatch.Restart();
                _captureDevice = await Device.OpenAsync(Characteristics, OnPixelBufferArrivedAsync);
                if (_captureDevice == null) return false;
                await _captureDevice.StartAsync();
                IsRunning = true;
                return true;
            }
            catch
            {
                _captureDevice?.Dispose();
                _captureDevice = null;
                IsRunning = false;
                return false;
            }
        }

        // 🔹 停止摄像头
        public static async Task Stop()
        {
            try
            {

                _fpsStopwatch.Stop();
                _yoloStopwatch.Stop();
                if (_captureDevice == null) return;
                await _captureDevice.StopAsync();
            }
            finally
            {
                _captureDevice?.Dispose();
                _captureDevice = null;
                IsRunning = false;
            }
        }

        // 🔹 卸载摄像头
        public static async Task UnInit()
        {
            _frameCounter = 0;
            _saveCount = 0;
            IsInit = false;
            await Stop();
        }
        #endregion 
    }

}
