using Emgu.CV;
using Emgu.CV.Cuda;
using Emgu.CV.CvEnum;
using Emgu.CV.Dnn;
using Emgu.CV.Structure;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Diagnostics;
using System.Drawing;

namespace yoloNet.demo
{
    /// <summary>
    /// 目标检测服务
    /// </summary>
    public class DnnService
    {

        Net? net;
        InferenceSession? session;
        public DnnService(string onnxPath)
        {
            // 0. 读取模型
            if (string.IsNullOrEmpty(onnxPath)) return;
            try
            {
                net = DnnInvoke.ReadNetFromONNX(onnxPath);

                foreach (var layerName in net.UnconnectedOutLayersNames)
                {
                    Console.WriteLine(layerName);
                } 
                var options = new SessionOptions();
                try
                {
                    options.AppendExecutionProvider_CUDA(0);
                    //options.LogSeverityLevel =  OrtLoggingLevel.ORT_LOGGING_LEVEL_INFO; // 打印详细信息
                    Console.WriteLine("CUDA Execution Provider added successfully.");
                }
                catch (OnnxRuntimeException ex)
                {
                    Console.WriteLine("CUDA Execution Provider failed: " + ex.Message);
                    Console.WriteLine("Fallback to CPU.");
                }
                // 创建 InferenceSession
                session = new InferenceSession(onnxPath, options);
            }
            catch (Exception ex)
            {
                net = null; session = null;
                Console.WriteLine($"加载模型失败: {ex.Message}");
            }

        }
        // NMS 参数
        private const float ScoreThreshold = 0.3f;
        private const float NmsThreshold = 0.45f;
        double scale = 1;
        int x = 0;
        int y = 0;


        /// <summary>
        /// 不可靠的方案 类别会输出错误 这是 OpenCV DNN 的已知限制
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        public List<DetectedBox>? Detect(Mat src)
        {
            if (src == null || net == null) return null;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            int size = src.Width;

            if (src.Width != src.Height)
            {
                src = Letterbox(src, out scale, out x, out y);
                Console.WriteLine($"Letterbox: {sw.ElapsedMilliseconds} ms");
            }

            // 1. 预处理
            sw.Restart();
            using Mat blob = DnnInvoke.BlobFromImage(src, 1 / 255.0, new Size(size, size),
                new MCvScalar(0, 0, 0), swapRB: true, crop: false);
            Console.WriteLine($"1.预处理: {sw.ElapsedMilliseconds} ms");

            // 2. 前向推理
            sw.Restart();
            net.SetInput(blob);
            using Mat output = net.Forward();
            Console.WriteLine($"2.前向推理: {sw.ElapsedMilliseconds} ms");

            // 3. 输出维度
            sw.Restart();
            int[] dims = output.SizeOfDimension; // e.g. [1, 84, 8400]
            int numAttrs = dims[1];
            int numPreds = dims[2];

            float[] data = new float[output.Total.ToInt32()];
            output.CopyTo(data);
            Console.WriteLine($"3.输出维度: {sw.ElapsedMilliseconds} ms");

            // 5. 循环解析预测
            sw.Restart();
            var boxes = new List<DetectedBox>();
            for (int i = 0; i < numPreds; i++)
            {
                // 索引方式: [1, attr, pred] → data[attr * numPreds + i]
                float cx = data[0 * numPreds + i];
                float cy = data[1 * numPreds + i];
                float w = data[2 * numPreds + i];
                float h = data[3 * numPreds + i];

                float score = data[4 * numPreds + i];  // YOLOv11 导出的 conf 已经是 obj*classProb
                if (score < ScoreThreshold) continue;

                // 找类别
                int classId = -1;
                float maxCls = 0f;
                for (int c = 5; c < numAttrs; c++)
                {
                    float clsProb = data[c * numPreds + i]; // 注意：不要 sigmoid
                    if (clsProb > maxCls)
                    {
                        maxCls = clsProb;
                        classId = c - 5; // 这里就是类别
                    }
                }




                // xywh → xyxy
                float x1 = (cx - w / 2) * src.Width / size;
                float y1 = (cy - h / 2) * src.Height / size;
                float x2 = (cx + w / 2) * src.Width / size;
                float y2 = (cy + h / 2) * src.Height / size;

                // 裁剪
                x1 = Math.Max(0, Math.Min(x1, src.Width - 1));
                y1 = Math.Max(0, Math.Min(y1, src.Height - 1));
                x2 = Math.Max(0, Math.Min(x2, src.Width - 1));
                y2 = Math.Max(0, Math.Min(y2, src.Height - 1));

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
            Console.WriteLine($"4.循环解析预测: {sw.ElapsedMilliseconds} ms");

            // 6. NMS
            sw.Restart();
            boxes = NonMaxSuppression(boxes, NmsThreshold);
            Console.WriteLine($"5.NMS: {sw.ElapsedMilliseconds} ms");



            Console.WriteLine($"==================================》得分: {(boxes.FirstOrDefault()?.Score ?? 0):F1}");
            return boxes.Any() ? boxes : null;
        }



        private static float Sigmoid(float x) => 1f / (1f + (float)Math.Exp(-x));
        Image<Bgr, byte> Letterbox(Image<Bgr, byte> src)
        {
            int w = src.Width;
            int h = src.Height;
            int targetW = w;
            int targetH = w;//正方形
            double scale = Math.Min((double)targetW / w, (double)targetH / h);
            int newW = (int)(w * scale);
            int newH = (int)(h * scale);

            var resized = src.Resize(newW, newH, Inter.Linear);
            var output = new Image<Bgr, byte>(targetW, targetH, new Bgr(0, 0, 0));

            int x = (targetW - newW) / 2;
            int y = (targetH - newH) / 2;
            resized.CopyTo(output.GetSubRect(new Rectangle(x, y, newW, newH)));
            return output;
        }

        public Mat Letterbox(Mat src, out double scale, out int x, out int y)
        {
            int w = src.Width;
            int h = src.Height;
            int targetSize = w;
            // 缩放比例
            scale = Math.Min((double)targetSize / w, (double)targetSize / h);
            int newW = (int)(w * scale);
            int newH = (int)(h * scale);

            // resize
            Mat resized = new Mat();
            CvInvoke.Resize(src, resized, new Size(newW, newH), 0, 0, Inter.Linear);

            // 创建黑色背景
            Mat output = new Mat(new Size(targetSize, targetSize), DepthType.Cv8U, 3);
            output.SetTo(new MCvScalar(0, 0, 0));

            // 计算放置位置（居中）
            x = (targetSize - newW) / 2;
            y = (targetSize - newH) / 2;

            // ROI 区域
            var roi = new Rectangle(x, y, newW, newH);
            Mat subMat = new Mat(output, roi);

            resized.CopyTo(subMat);

            return output;
        }

        List<DetectedBox> NonMaxSuppression(List<DetectedBox> boxes, float iouThreshold)
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

        float IoU(DetectedBox a, DetectedBox b)
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

        internal List<DetectedBox>? DetectByOnnx(Mat copyFrame)
        {
            if (session == null || copyFrame == null || copyFrame.IsEmpty) return null;
            Stopwatch sw = new Stopwatch();
            sw.Start();
            var tensor = MatToTensor(copyFrame);
            Console.WriteLine($"1.填充: {sw.ElapsedMilliseconds} ms");
#pragma warning disable CS0618 // 类型或成员已过时
            try
            {
                sw.Restart();
                var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", tensor) };
                using var results = session.Run(inputs);
                Console.WriteLine($"2.推理: {sw.ElapsedMilliseconds} ms");
                sw.Restart();
                var boxes = ParseOnnxOutput(results);
                Console.WriteLine($"3.NMS: {sw.ElapsedMilliseconds} ms");

                Console.WriteLine($"==================================》得分: {(boxes.FirstOrDefault()?.Score ?? 0):F1}");
                return boxes;
            }
            catch (System.ExecutionEngineException ex)
            {
                Console.WriteLine($"推理异常: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"推理异常: {ex.Message}");
                return null;
            }
            finally
            {
                copyFrame?.Dispose();
            }
#pragma warning restore CS0618 // 类型或成员已过时
        }

        public Tensor<float> MatToTensor(Mat mat)
        {
            int h = mat.Rows;
            int w = mat.Cols;
            int c = mat.NumberOfChannels;
            if (c != 3) throw new ArgumentException("Only 3-channel BGR Mats are supported.");

            var tensor = new DenseTensor<float>(new[] { 1, 3, h, w });

            unsafe
            {
                byte* ptr = (byte*)mat.DataPointer.ToPointer();
                int step = mat.Step; // 每行字节数

                Parallel.For(0, h, y =>
                {
                    for (int x = 0; x < w; x++)
                    {
                        int offset = y * step + x * c;
                        tensor[0, 0, y, x] = ptr[offset + 2] / 255f; // R
                        tensor[0, 1, y, x] = ptr[offset + 1] / 255f; // G
                        tensor[0, 2, y, x] = ptr[offset + 0] / 255f; // B
                    }
                });
            }

            return tensor;
        }

        public List<DetectedBox> ParseOnnxOutput(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results)
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
    }
    public class DetectedBox
    {
        public float X1, Y1, X2, Y2; // xyxy
        public float Score;
        public int ClassId;
        public string Label = string.Empty;
    }
}
