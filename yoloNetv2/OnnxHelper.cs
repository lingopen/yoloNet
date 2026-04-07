using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace yoloNetv2
{ 
    public static class OnnxHelper
    {
        public static byte[] DecodeJpegToRGB(SKBitmap bitmap, out int width, out int height)
        {
            width = bitmap.Width;
            height = bitmap.Height;
            byte[] rgb = new byte[width * height * 3];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var c = bitmap.GetPixel(x, y);
                    int idx = (y * width + x) * 3;
                    rgb[idx + 0] = c.Red;
                    rgb[idx + 1] = c.Green;
                    rgb[idx + 2] = c.Blue;
                }
            }

            return rgb;
        }
        // 🔹 RGB byte[] 直接填充 Letterbox Tensor
        public static DenseTensor<float> FillTensorWithLetterbox(byte[] rgb, int srcW, int srcH, int dstSize)
        {
            var tensor = new DenseTensor<float>(new[] { 1, 3, dstSize, dstSize });

            float scale = Math.Min((float)dstSize / srcW, (float)dstSize / srcH);
            int newW = (int)(srcW * scale);
            int newH = (int)(srcH * scale);
            int offsetX = (dstSize - newW) / 2;
            int offsetY = (dstSize - newH) / 2;

            for (int y = 0; y < newH; y++)
            {
                for (int x = 0; x < newW; x++)
                {
                    int srcX = (int)(x / scale);
                    int srcY = (int)(y / scale);
                    int srcIndex = (srcY * srcW + srcX) * 3;
                    int dstIndexX = x + offsetX;
                    int dstIndexY = y + offsetY;

                    tensor[0, 0, dstIndexY, dstIndexX] = rgb[srcIndex + 0] / 255f; // R
                    tensor[0, 1, dstIndexY, dstIndexX] = rgb[srcIndex + 1] / 255f; // G
                    tensor[0, 2, dstIndexY, dstIndexX] = rgb[srcIndex + 2] / 255f; // B
                }
            }

            return tensor;
        }


        

        // 🔹 简单解析 YOLO 输出
      
        public static Box[] ParseYoloOutputForNoClass(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results, int srcW, int srcH, int dstSize, float confThreshold = 0.3f, float iouThreshold = 0.5f)
        {
            var output = results.First().AsTensor<float>(); // shape: [1,5,N]
            int N = output.Dimensions[2]; // 框数量
            var list = new List<Box>();

            // Letterbox 缩放参数
            float scale = Math.Min((float)dstSize / srcW, (float)dstSize / srcH);
            float offsetX = (dstSize - srcW * scale) / 2;
            float offsetY = (dstSize - srcH * scale) / 2;

            for (int i = 0; i < N; i++)
            {
                float conf = output[0, 4, i];
                if (conf < confThreshold) continue;

                // YOLO 输出是 [cx, cy, w, h, conf]，需要转成 x1,y1,x2,y2
                float cx = output[0, 0, i];
                float cy = output[0, 1, i];
                float w = output[0, 2, i];
                float h = output[0, 3, i];

                float x1 = cx - w / 2;
                float y1 = cy - h / 2;
                float x2 = cx + w / 2;
                float y2 = cy + h / 2;

                // 逆 Letterbox 映射回原图
                x1 = (x1 - offsetX) / scale;
                y1 = (y1 - offsetY) / scale;
                x2 = (x2 - offsetX) / scale;
                y2 = (y2 - offsetY) / scale;

                list.Add(new Box
                {
                    X1 = x1,
                    Y1 = y1,
                    X2 = x2,
                    Y2 = y2,
                    Score = conf,
                    ClassId = 0 // 暂时没有类别信息
                });
            }
            // ==========================
            // NMS 去重
            // ==========================
            var nmsList = new List<Box>();
            // 按置信度从高到低排序
            var sorted = list.OrderByDescending(b => b.Score).ToList();

            while (sorted.Count > 0)
            {
                var best = sorted[0];
                nmsList.Add(best);
                sorted.RemoveAt(0);

                sorted = sorted
                    .Where(b => IoU(best, b) < iouThreshold)
                    .ToList();
            }

            return nmsList.ToArray(); 
        }
        // IoU 计算函数
        private static float IoU(Box a, Box b)
        {
            float x1 = Math.Max(a.X1, b.X1);
            float y1 = Math.Max(a.Y1, b.Y1);
            float x2 = Math.Min(a.X2, b.X2);
            float y2 = Math.Min(a.Y2, b.Y2);

            float interArea = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
            float unionArea = (a.X2 - a.X1) * (a.Y2 - a.Y1) + (b.X2 - b.X1) * (b.Y2 - b.Y1) - interArea;

            return interArea / unionArea;
        }


    }

    public struct Box
    {
        public float X1, Y1, X2, Y2, Score;
        public int ClassId;
    }
}
