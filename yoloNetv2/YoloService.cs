using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace yoloNetv2
{
    public  class YoloService : IDisposable
    {
        private const float ScoreThreshold = 0.3f;
        private const float NmsThreshold = 0.45f;
        private bool _disposed = false;

        public DenseTensor<float> CreateTensor(int width, int height)
        {
            // 🔹 每帧创建 Tensor 避免多线程冲突
            return new DenseTensor<float>(new[] { 1, 3, height, width });
        }

        public void FillTensor(SKBitmap bitmap, DenseTensor<float> tensor)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;

            using var pixmap = bitmap.PeekPixels();
            IntPtr ptr = pixmap.GetPixels();
            byte[] pixelData = new byte[width * height * 4];
            Marshal.Copy(ptr, pixelData, 0, pixelData.Length); // 🔹 安全复制像素

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * 4;
                    byte b = pixelData[index + 0];
                    byte g = pixelData[index + 1];
                    byte r = pixelData[index + 2];
                    tensor[0, 0, y, x] = r / 255f;
                    tensor[0, 1, y, x] = g / 255f;
                    tensor[0, 2, y, x] = b / 255f;
                }
            }
        }
        public List<DetectedBox> ParseOnnxOutput(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results)
        {
            var boxes = new List<DetectedBox>();
            var output = results.First(r => r.Name == "output0").AsTensor<float>();

            int numPreds = output.Dimensions[2];
            int numAttrs = output.Dimensions[1];

            for (int i = 0; i < numPreds; i++)
            {
                float x = output[0, 0, i];
                float y = output[0, 1, i];
                float w = output[0, 2, i];
                float h = output[0, 3, i];
                float conf = output[0, 4, i];
                float classProb = output[0, 5, i];
                int classId = (int)classProb;

                float score = conf;
                if (score < ScoreThreshold) continue;

                float x1 = x - w / 2;
                float y1 = y - h / 2;
                float x2 = x + w / 2;
                float y2 = y + h / 2;

                boxes.Add(new DetectedBox
                {
                    X1 = x1,
                    Y1 = y1,
                    X2 = x2,
                    Y2 = y2,
                    Score = score,
                    ClassId = classId,
                    Label = $"cls:{classId} score:{score:F2}"
                });
            }

            return NonMaxSuppression(boxes, NmsThreshold);
        }

        private List<DetectedBox> NonMaxSuppression(List<DetectedBox> boxes, float iouThreshold)
        {
            var result = new List<DetectedBox>();
            var sortedBoxes = boxes.OrderByDescending(b => b.Score).ToList();

            while (sortedBoxes.Count > 0)
            {
                var best = sortedBoxes[0];
                result.Add(best);
                sortedBoxes.RemoveAt(0);

                for (int i = sortedBoxes.Count - 1; i >= 0; i--)
                {
                    if (IoU(best, sortedBoxes[i]) > iouThreshold)
                        sortedBoxes.RemoveAt(i);
                }
            }

            return result;
        }

        private float IoU(DetectedBox a, DetectedBox b)
        {
            float x1 = Math.Max(a.X1, b.X1);
            float y1 = Math.Max(a.Y1, b.Y1);
            float x2 = Math.Min(a.X2, b.X2);
            float y2 = Math.Min(a.Y2, b.Y2);

            float w = Math.Max(0, x2 - x1);
            float h = Math.Max(0, y2 - y1);

            float inter = w * h;
            float union = (a.X2 - a.X1) * (a.Y2 - a.Y1) + (b.X2 - b.X1) * (b.Y2 - b.Y1) - inter;

            return inter / union;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing) { }
            _disposed = true;
        }
    }

    public class DetectedBox
    {
        public float X1, Y1, X2, Y2;
        public float Score;
        public int ClassId;
        public string Label = string.Empty;
    }
}
