using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using yoloNetv2.ViewModels;

namespace yoloNetv2.Controls
{
    public class AnnotatedCanvas : Control
    {
        public MarkViewModel? ViewModel { get; set; }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (ViewModel == null) return;

            // 绘制图片
            if (ViewModel.CurrentImage != null)
            {
                context.DrawImage(ViewModel.CurrentImage, 
                    new Rect(0, 0, ViewModel.CurrentImage.PixelSize.Width, ViewModel.CurrentImage.PixelSize.Height),
                    new Rect(0, 0, ViewModel.ImageWidth, ViewModel.ImageHeight));

               
            }

            // 绘制已标注矩形
            foreach (var ann in ViewModel.Annotations)
            {
                IBrush brush = ann.ClassId switch
                {
                    0 => Brushes.Yellow,
                    1 => Brushes.Green,
                    2 => Brushes.Blue,
                    _ => Brushes.White
                };
                var pen = new Pen(brush, 2);
                context.DrawRectangle(null, pen, ann.BoundingBox);

                // =========================
                // 🔥新增：绘制类别文字
                // =========================

                string label = ann.ClassId.ToString(); // 👉 可后续替换为类别名

                var formattedText = new FormattedText(
                    label,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Arial"),
                    14,
                    Brushes.Black
                );

                // 🔥新增：计算文字背景位置（防止超出顶部）
                double textX = ann.BoundingBox.X;
                double textY = ann.BoundingBox.Y - formattedText.Height;

                if (textY < 0)
                    textY = ann.BoundingBox.Y;

                var textRect = new Rect(
                    textX,
                    textY,
                    formattedText.Width + 4,
                    formattedText.Height
                );

                // 🔥新增：绘制背景（用类别颜色）
                context.DrawRectangle(brush, null, textRect);

                // 🔥新增：绘制文字
                context.DrawText(formattedText, new Point(textX + 2, textY));
            }

            // 绘制临时矩形
            if (ViewModel.TempRect.HasValue)
            {
                var pen = new Pen(new SolidColorBrush(Colors.Yellow), 2, dashStyle: DashStyle.Dash);
                context.DrawRectangle(null, pen, ViewModel.TempRect.Value);
            }
        }
    } 
}
