using System;
using System.IO;
using SkiaSharp;

namespace yoloNetv2.Extentions
{
    public static class MatExtensions
    {
        public static void SaveFrame(this SKBitmap image, int index)
        {
            try
            {
                string folder = Path.Combine("dataset", "images", "train");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string filename = Path.Combine(folder, $"{index}_({DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}).jpg");

                // ✅ 编码（线程安全前提：image 是独立副本）
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);

                // ✅ 写文件
                using var stream = File.OpenWrite(filename);
                data.SaveTo(stream);

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
        public static (bool ,string) ClearFrame()
        {
            try
            {
                string folder = Path.Combine("dataset", "images", "train");
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true); // true 表示递归删除子目录和文件 
                    Console.WriteLine($"已删除文件夹: {folder}");
                    return (true, $"已删除文件夹: {folder}");
                }
                else
                {
                    Console.WriteLine($"文件夹不存在: {folder}");
                    return (false, $"文件夹不存在: {folder}");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"删除失败: {ex.Message}");
                return (false, $"删除失败: {ex.Message}");  
            }
        }
    }
}
