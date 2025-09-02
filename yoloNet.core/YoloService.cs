using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;

namespace yoloNet.core
{
    /// <summary>
    /// yolo onnx 模型服务
    /// </summary>
    public class YoloService : IDisposable
    {
        // NMS 参数
        private const float ScoreThreshold = 0.3f;
        private const float NmsThreshold = 0.45f;
        CancellationTokenSource? cts;
        Mat frame = new Mat();
        Mat latestFrame = new Mat();
        Image<Bgr, byte>? copyFrame;
        Image<Bgr, byte>? displayFrame;
        VideoCapture? capture;
        InferenceSession? session;
        private string? rtspUrl;
        private string? onnxPath;
        public YoloService() { }
        public YoloService(string rtspUrl, string? onnxPath)
        {
            this.rtspUrl = rtspUrl;
            this.onnxPath = onnxPath;

        }

        /// <summary>
        /// 停止推理
        /// </summary>
        public void Stop()
        {
            capture?.Stop();
            cts?.Cancel();
            Thread.Sleep(200); // 等待后台线程退出
            session?.Dispose();
            frame?.Dispose();
            latestFrame?.Dispose();
            displayFrame?.Dispose();
            capture?.Dispose();
        }

        /// <summary>
        /// 采集图像并推理
        /// </summary>
        public void Start()
        {
            if (!string.IsNullOrEmpty(rtspUrl) && rtspUrl.StartsWith("rtsp"))
                capture = new VideoCapture(rtspUrl, VideoCapture.API.Ffmpeg);
            else if (!string.IsNullOrEmpty(rtspUrl))
            {
                try
                {
                    var index = int.Parse(rtspUrl);
                    capture = new VideoCapture(index, VideoCapture.API.DShow);
                    capture.Set(CapProp.FourCC, VideoWriter.Fourcc('M', 'J', 'P', 'G'));
                    capture.Set(CapProp.FrameWidth, 1280);
                    capture.Set(CapProp.FrameHeight, 720);
                }
                catch (Exception)
                {
                    return;
                }

            }
            else return;
            if (!capture.IsOpened)
            {
                Debug.WriteLine("无法打开 RTSP 流");
                return;
            }

            int width = (int)capture.Get(CapProp.FrameWidth);
            int height = (int)capture.Get(CapProp.FrameHeight);
            Debug.WriteLine($"Video: {width}x{height}");

            object frameLock = new object();

            List<DetectedBox> lastBoxes = new List<DetectedBox>();
            bool isInferencing = false;

            int realFrameCounter = 0;
            int onnxFrameCounter = 0;
            DateTime lastFpsTime = DateTime.Now;

            cts = new CancellationTokenSource();
            if (!string.IsNullOrEmpty(onnxPath)) //加载模型
            {

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

            // 后台线程抓帧
            Task.Run(() =>
            {
                while (cts != null && !cts.IsCancellationRequested)
                {
                    if (capture.Grab())
                    {
                        capture.Retrieve(frame);
                        lock (frameLock)
                        {
                            latestFrame?.Dispose();
                            latestFrame = frame.Clone();
                            realFrameCounter++;
                        }
                    }
                }
            }, cts.Token);
            double scale = 1; int x = 0; int y = 0;
            string fps = "0";
            while (cts != null && !cts.IsCancellationRequested)
            {
                lock (frameLock)
                {
                    if (latestFrame.IsEmpty) continue;

                    displayFrame?.Dispose();
                    displayFrame = latestFrame.ToImage<Bgr, byte>();
                    copyFrame = Letterbox(displayFrame, out scale, out x, out y);
                }

                // 异步推理
                if (session != null && copyFrame != null && !isInferencing)
                {
                    onnxFrameCounter++;
                    isInferencing = true;
                    var tensor = MatToTensor(copyFrame);
                    Task.Run(() =>
                    {
#pragma warning disable CS0618 // 类型或成员已过时
                        try
                        {
                            var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", tensor) };
                            using var results = session.Run(inputs);
                            var boxes = ParseOnnxOutput(results);
                            lastBoxes = boxes;
                        }
                        catch (System.ExecutionEngineException ex)
                        {
                            Console.WriteLine($"推理异常: {ex.Message}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"推理异常: {ex.Message}");
                        }
                        finally
                        {
                            isInferencing = false;
                            copyFrame?.Dispose(); copyFrame = null;
                        }
#pragma warning restore CS0618 // 类型或成员已过时
                    });
                }

                // 绘制结果
                foreach (var box in lastBoxes)
                {
                    CvInvoke.Rectangle(displayFrame,
                        new Rectangle((int)box.X1 - x, (int)box.Y1 - y, (int)(box.X2 - box.X1), (int)(box.Y2 - box.Y1)),
                        new MCvScalar(0, 0, 255), 2);
                    CvInvoke.PutText(displayFrame, box.Label, new Point((int)box.X1 - x, (int)box.Y1 - y - 5),
                        FontFace.HersheySimplex, 0.5, new MCvScalar(255, 255, 255));
                }

                // FPS 计算
                var now = DateTime.Now;
                var span = now - lastFpsTime;

                if (span.TotalMilliseconds >= 1000)
                {
                    fps = $"FPS: {realFrameCounter / span.TotalSeconds:F2},DNN: {onnxFrameCounter / span.TotalSeconds:F2}";

                    Console.Title = fps;
                    realFrameCounter = 0;
                    onnxFrameCounter = 0;
                    lastFpsTime = now;
                }
                CvInvoke.PutText(displayFrame, fps,
                   new Point(10, 30),
                   FontFace.HersheySimplex, 1.0, new MCvScalar(0, 255, 0), 2);
                // 显示

                CvInvoke.Imshow("YOLOv11 RTSP", displayFrame);
                int key = CvInvoke.WaitKey(1);
                if (key == 27) // ESC
                {
                    // 停止推理，释放资源
                    break;
                }
            }
        }

        public Image<Bgr, byte> Letterbox(Image<Bgr, byte> src)
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

        public Image<Bgr, byte> Letterbox(Image<Bgr, byte> src, out double scale, out int x, out int y)
        {
            int w = src.Width;
            int h = src.Height;
            int targetW = w;
            int targetH = w;//正方形
            scale = Math.Min((double)targetW / w, (double)targetH / h);
            int newW = (int)(w * scale);
            int newH = (int)(h * scale);

            var resized = src.Resize(newW, newH, Inter.Linear);
            var output = new Image<Bgr, byte>(targetW, targetH, new Bgr(0, 0, 0));

            x = (targetW - newW) / 2;
            y = (targetH - newH) / 2;
            resized.CopyTo(output.GetSubRect(new Rectangle(x, y, newW, newH)));
            return output;
        }

        public Tensor<float> MatToTensorGPU(Image<Bgr, byte> src)
        {
            // Letterbox Resize
            int w = src.Width;
            int h = src.Height;
            int newW = w;
            int newH = w;

            double scale = Math.Min((double)newW / w, (double)newH / h);
            int resizeW = (int)(w * scale);
            int resizeH = (int)(h * scale);

            var resized = new Image<Bgr, byte>(resizeW, resizeH);
            CvInvoke.Resize(src, resized, new Size(resizeW, resizeH), 0, 0, Inter.Linear);

            var output = new Image<Bgr, byte>(newW, newH, new Bgr(0, 0, 0));
            int x = (newW - resizeW) / 2;
            int y = (newH - resizeH) / 2;
            resized.CopyTo(output.GetSubRect(new Rectangle(x, y, resizeW, resizeH)));
            resized.Dispose();

            // 转 Tensor
            var tensor = new DenseTensor<float>(new[] { 1, 3, newH, newW });
            var data = output.Data; // [h, w, 3]

            // 并行处理每个通道
            Parallel.For(0, newH, row =>
            {
                for (int col = 0; col < newW; col++)
                {
                    tensor[0, 0, row, col] = data[row, col, 2] / 255f; // R
                    tensor[0, 1, row, col] = data[row, col, 1] / 255f; // G
                    tensor[0, 2, row, col] = data[row, col, 0] / 255f; // B
                }
            });

            output.Dispose();
            return tensor;
        }


        public Tensor<float> MatToTensor(Image<Bgr, byte> mat)
        {
            int h = mat.Height;
            int w = mat.Width;
            var tensor = new DenseTensor<float>(new[] { 1, 3, h, w });
            var data = mat.Data;

            Parallel.For(0, h, y =>
            {
                for (int x = 0; x < w; x++)
                {
                    tensor[0, 0, y, x] = data[y, x, 2] / 255f; // R
                    tensor[0, 1, y, x] = data[y, x, 1] / 255f; // G
                    tensor[0, 2, y, x] = data[y, x, 0] / 255f; // B
                }
            });

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

        public void Dispose()
        {
            Stop();
        }


        public Rectangle? Detect(Image<Bgr, byte>? src, string? onnxPath)
        {
            if (string.IsNullOrEmpty(onnxPath) || src == null) return null;
            InferenceSession? _session = null;
            if (!string.IsNullOrEmpty(onnxPath)) //加载模型
            {
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
                _session = new InferenceSession(onnxPath, options);

            }
            if (_session == null) return null;
            using var displayFrame = Letterbox(src);
            if (displayFrame == null) return null;

            var tensor = MatToTensor(displayFrame);
            List<DetectedBox>? lastBoxes = null;
#pragma warning disable CS0618 // 类型或成员已过时
            try
            {
                var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("images", tensor) };
                using var results = _session.Run(inputs);
                var boxes = ParseOnnxOutput(results);
                lastBoxes = boxes;
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
#pragma warning restore CS0618 // 类型或成员已过时
            if (lastBoxes == null || !lastBoxes.Any()) return null;
            var box = lastBoxes.OrderByDescending(b => b.Score).First();
            return new Rectangle((int)box.X1, (int)box.Y1, (int)(box.X2 - box.X1), (int)(box.Y2 - box.Y1));
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
