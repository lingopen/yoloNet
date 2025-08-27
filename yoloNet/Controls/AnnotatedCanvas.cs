using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using yoloNet.ViewModels;

namespace yoloNet.Controls
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
                    0 => new SolidColorBrush(Colors.Red),
                    1 => new SolidColorBrush(Colors.Green),
                    2 => new SolidColorBrush(Colors.Blue),
                    _ => new SolidColorBrush(Colors.Yellow)
                };
                var pen = new Pen(brush, 2);
                context.DrawRectangle(null, pen, ann.BoundingBox);
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
