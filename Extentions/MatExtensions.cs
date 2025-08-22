using System;
using System.ComponentModel.Design;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Reg;
using Emgu.CV.Structure;
using static System.Net.Mime.MediaTypeNames;

namespace yoloNet.Extentions
{
    public static class MatExtensions
    {
        // 将Emgu.CV的Mat转换为Avalonia的Bitmap
        public static void ConvertToWriteableBitmap(this Image<Bgr, byte> image, WriteableBitmap? writeableBitmap, Canvas canvas)
        {
            if (image == null || image.Data == null || image.Bytes == null) return;

            int width = image.Width;
            int height = image.Height;
            byte[] data = image.Bytes;

            if (writeableBitmap == null || writeableBitmap.PixelSize.Width != width || writeableBitmap.PixelSize.Height != height)
            {
                writeableBitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
                canvas.Background = new ImageBrush { Source = writeableBitmap };
            }

            using (var buffer = writeableBitmap.Lock())
            {
                int rowBytes = buffer.RowBytes;
                IntPtr bufferPtr = buffer.Address;

                unsafe
                {
                    for (int y = 0; y < height; y++)
                    {
                        byte* row = (byte*)IntPtr.Add(bufferPtr, y * rowBytes);
                        for (int x = 0; x < width; x++)
                        {
                            int pixelIndex = (y * width + x) * 3; // 正确索引
                            byte b = data[pixelIndex];
                            byte g = data[pixelIndex + 1];
                            byte r = data[pixelIndex + 2];
                            row[x * 4] = b;
                            row[x * 4 + 1] = g;
                            row[x * 4 + 2] = r;
                            row[x * 4 + 3] = 255;
                        }
                    }
                }
            }

            canvas.InvalidateVisual();
        }

        public static void SaveFrame(this Image<Bgr, byte> image, int index)
        {
            try
            {
                string folder = Path.Combine("dataset", "images", "train");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string filename = Path.Combine(folder, $"{index}_({DateTime.Now:yyyyMMdd_HHmmss_fff}).jpg");
                image.Save(filename);
                Console.WriteLine($"保存帧: {filename}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存帧失败: {ex.Message}");
            }
        }
        /// <summary>
        /// 删除抽帧文件
        /// </summary>
        public static void ClearFrame()
        {
            try
            {
                string folder = Path.Combine("dataset", "images", "train");
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true); // true 表示递归删除子目录和文件 
                    Console.WriteLine($"已删除文件夹: {folder}");
                }
                else Console.WriteLine($"文件夹不存在: {folder}");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"删除失败: {ex.Message}");
            }
        }
    }
}
