using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace yoloNetv2.Controls
{
    public class ImageDisplayControl : Control
    {


        public IBrush FpsBrush { get; set; } = Brushes.Red;

        // 图片帧
        public WriteableBitmap? ImageFrame { get; set; }

        // 目标检测框
        public List<Rect>? DetectRects { get; set; }

        // 对应文字标签
        public string DetectScore { get; set; } = "";

        // 可配置画笔
        public Pen DetectPen { get; set; } = new Pen(Brushes.Yellow, 2);

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

            if (ImageFrame != null)
            {
                // 帧缩放到控件大小
                Rect destRect = new Rect(0, 0, Bounds.Width, Bounds.Height);
                context.DrawImage(ImageFrame, destRect);
            }

            // 绘制框 
            if (DetectRects != null)
                foreach (var DetectRect in DetectRects!)
                {
                    if (DetectRect.X > 0 && DetectRect.Y > 0)
                    {
                        // 缩放框到控件大小

                        var scaledRect = ScaleRect(DetectRect, ImageFrame, Bounds);

                        // 绘制矩形
                        context.DrawRectangle(null, DetectPen, scaledRect);

                    }
                }

            // 绘制 文本
            if (!string.IsNullOrEmpty(DetectScore))
            {

                var formattedText = new FormattedText(
                 $"{(string.IsNullOrEmpty(DetectScore) ? "" : DetectScore)}",
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
        public void UpdateFrame(WriteableBitmap bitmap, List<Rect> detectRects, string? detectScore = null)
        {
            ImageFrame = bitmap;
            DetectRects = detectRects;
            if (detectScore != null)
                DetectScore = detectScore;
            InvalidateVisual(); // 触发重绘
        }
    }
}
