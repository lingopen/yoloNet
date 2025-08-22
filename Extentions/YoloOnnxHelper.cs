using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;

namespace yoloNet.Extentions
{
    public class DetectedBox
    {
        public float X1, Y1, X2, Y2; // xyxy
        public float Score;
        public int ClassId;
        public string Label = string.Empty;
    }

    public static class YoloOnnxHelper
    {
        // NMS 参数
        private const float ScoreThreshold = 0.3f;
        private const float NmsThreshold = 0.45f;

        public static List<DetectedBox> ParseOnnxOutput(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results)
        {
            var boxes = new List<DetectedBox>();

            // 获取原始输出
            var output = results.First(r => r.Name == "output0").AsTensor<float>();
            int numPreds = output.Dimensions[2]; // 13125
            int numAttrs = output.Dimensions[1]; // 7

            for (int i = 0; i < numPreds; i++)
            {
                float x = output[0, 0, i];
                float y = output[0, 1, i];
                float w = output[0, 2, i];
                float h = output[0, 3, i];
                float conf = output[0, 4, i];
                float classProb = output[0, 5, i];
                int classId = (int)classProb;

                float score = conf; // 或 conf * classProb

                if (score < ScoreThreshold)
                    continue;

                // YOLO 输出 x,y,w,h 是相对 0~1 的坐标吗？如果是就乘 800
                float x1 = x - w / 2;
                float y1 = y - h / 2;
                float x2 = x + w / 2;
                float y2 = y + h / 2;

                // 如果你的模型输出已经是 800x800 像素，就不用乘；否则乘 InputSize
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

            // NMS
            return NonMaxSuppression(boxes, NmsThreshold);
        }

        private static List<DetectedBox> NonMaxSuppression(List<DetectedBox> boxes, float iouThreshold)
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

        private static float IoU(DetectedBox a, DetectedBox b)
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
    }
}