using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
namespace yoloNetv2.Controls
{
    public class VideoDisplayControl : Control
    {
        public double Fps { get; set; } = 0;
        public IBrush FpsBrush { get; set; } = Brushes.Red;
        public double FpsFontSize { get; set; } = 18;
        // 视频帧
        public WriteableBitmap? VideoFrame { get; set; }

        // 目标检测框
        public Rect? DetectRect { get; set; }

        // 对应文字标签
        public string DetectScore { get; set; } = "";

        // 可配置画笔
        public Pen DetectPen { get; set; } = new Pen(Brushes.Red, 2);

        public Typeface LabelTypeface { get; set; } = new Typeface("Arial");
        public double LabelFontSize { get; set; } = 16;
        public IBrush LabelBrush { get; set; } = Brushes.Yellow;
        /// <summary>
        /// 抽帧次数
        /// </summary>
        public int SaveCount { get; set; }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (VideoFrame != null)
            {
                // 视频帧缩放到控件大小
                Rect destRect = new Rect(0, 0, Bounds.Width, Bounds.Height);
                Rect srcRect = new Rect(0, 0, VideoFrame.PixelSize.Width, VideoFrame.PixelSize.Height);
                context.DrawImage(VideoFrame, srcRect, destRect);
            }

            // 绘制框和文字

            if (DetectRect != null && DetectRect?.X > 0 && DetectRect?.Y > 0)
            {
                // 缩放框到控件大小

                var scaledRect = ScaleRect(DetectRect.Value, VideoFrame, Bounds);

                // 绘制矩形
                context.DrawRectangle(null, DetectPen, scaledRect);

                //// 绘制文字 
                //var formattedText = new FormattedText(
                //    $"({FaceScore})",
                //    System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                //    LabelTypeface,
                //    LabelFontSize,
                //    // 字体大小
                //    LabelBrush);
                //context.DrawText(formattedText, new Point(scaledRect.X, scaledRect.Y - LabelFontSize - 2));
            }

            // 绘制 FPS
            if (Fps > 0)
            {

                var formattedText = new FormattedText(
                 $"FPS: {Fps:F1} {(SaveCount > 0 ? "保存 " + SaveCount + " 张" : "")} {(string.IsNullOrEmpty(DetectScore) ? "" : DetectScore)}",
                 System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                 LabelTypeface, LabelFontSize, FpsBrush);
                context.DrawText(formattedText, new Point(20, 20));
            }
        }

        private Rect ScaleRect(Rect rect, WriteableBitmap? frame, Rect bounds)
        {
            if (frame == null) return rect;

            double scaleX = bounds.Width / frame.PixelSize.Width;
            double scaleY = bounds.Height / frame.PixelSize.Height;

            return new Rect(
                rect.X * scaleX,
                rect.Y * scaleY,
                rect.Width * scaleX,
                rect.Height * scaleY
            );
        }

        // 更新视频帧和绘制目标检测框和得分信息
        public void UpdateFrame(WriteableBitmap bitmap, Rect? detectRect = null, string? detectScore = null)
        {
            VideoFrame = bitmap;
            DetectRect = detectRect;
            if (detectScore != null)
                DetectScore = detectScore;
            InvalidateVisual(); // 触发重绘
        }
    }
}
