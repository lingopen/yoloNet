using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using yoloNet.Extentions;

namespace yoloNet.ViewModels
{
    public partial class RtspViewModel : ViewModelBase
    {
        [ObservableProperty]
        double _fps;
        /// <summary>
        /// 抽帧 每N帧保存一次
        /// </summary>
        [ObservableProperty]
        double _interval = 0;
        [ObservableProperty]
        int _frameCounter = 0;

        [ObservableProperty]
        int _width = 0;

        [ObservableProperty]
        int _Height = 0;
        [ObservableProperty]
        bool isRunning = false;
        private VideoCapture? _capture;
        /// <summary>
        /// 成像区域
        /// </summary>
        Canvas? _canvas;
        /// <summary>
        /// 图像内存对象
        /// </summary>
        private WriteableBitmap? _bitmap;
        [RelayCommand]
        public void Start(string rtspUrl)
        {
            if (IsRunning)
            {
                IsRunning = false;
                if (_capture != null)
                {
                    _capture.Stop();
                    _capture.ImageGrabbed -= _capture_ImageGrabbed;
                }
                System.Threading.Thread.Sleep(200);//休眠200ms
                _capture?.Dispose();
                _capture = null;
                _frame?.Dispose();
                _frame = null;
                return;
            }
            if (_canvas == null) return;
            FrameCounter = 0;
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
            //_capture = new VideoCapture(0);
            if (!_capture.IsOpened)
            {
                IsRunning = false;
                return;
            }

            Width = (int)_capture.Get(CapProp.FrameWidth);
            Height = (int)_capture.Get(CapProp.FrameHeight);
            Fps = _capture.Get(CapProp.Fps);
            _canvas.Width = Width; //调整画布大小
            _canvas.Height = Height;//调整画布大小 
            if (_bitmap != null)
            {
                _bitmap.Dispose();
            }
            _bitmap = new WriteableBitmap(
                new PixelSize(Width, Height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul
            );
            _canvas.Background = new ImageBrush { Source = _bitmap };

            _capture.ImageGrabbed += _capture_ImageGrabbed;
            _capture.Start();//重新采集
            IsRunning = true;
        }

        private Image<Bgr, byte> Letterbox(Image<Bgr, byte> src, int targetW = 320, int targetH = 320)
        {
            int w = src.Width;
            int h = src.Height;

            // 计算缩放比例
            double scale = Math.Min((double)targetW / w, (double)targetH / h);
            int newW = (int)(w * scale);
            int newH = (int)(h * scale);

            // 等比缩放
            var resized = src.Resize(newW, newH, Emgu.CV.CvEnum.Inter.Linear);

            // 创建黑色背景 (letterbox)
            var output = new Image<Bgr, byte>(targetW, targetH, new Bgr(0, 0, 0));

            // 居中放置
            int x = (targetW - newW) / 2;
            int y = (targetH - newH) / 2;
            resized.CopyTo(output.GetSubRect(new System.Drawing.Rectangle(x, y, newW, newH)));

            return output;
        }

        private Image<Bgr, byte>? _frame;

        private void _capture_ImageGrabbed(object? sender, EventArgs e)
        {
            if (_capture == null) return;
            _frame = _capture?.QueryFrame()?.ToImage<Bgr, byte>();
            if (_frame != null && _bitmap != null && _canvas != null)
            {
                //✅ 刷新WriteableBitmap
                _frame.ConvertToWriteableBitmap(_bitmap);
                // ✅ UI 显示，尽量轻量
                Dispatcher.UIThread.Post(() =>
                {
                    _canvas?.InvalidateVisual();//刷新UI
                });
                // ✅ 抽帧保存放到后台线程
                if (Interval > 0)
                {
                    FrameCounter++;
                    if (FrameCounter % Interval == 0)
                    {
                        var clone = _frame.Clone(); // 避免对象被释放
                        Task.Run(() =>
                        {
                            //原始尺寸缩放
                            var letterboxed = Letterbox(clone, Width, Width);
                            letterboxed.SaveFrame(FrameCounter);
                            clone.Dispose();
                            letterboxed.Dispose();
                        });
                    }
                }
            }
        }

        [RelayCommand]
        void Clear()
        {

            MatExtensions.ClearFrame();
        }
        /// <summary>
        /// 窗体初始化
        /// </summary>
        /// <returns></returns>
        public async Task OnInit(Canvas canvas)
        {
            _canvas = canvas;
            await Task.CompletedTask;
        }
    }
}
