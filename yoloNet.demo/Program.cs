using yoloNet.demo;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

internal class Program
{
    static void Main(string[] args)
    {

        var onnxPath = Path.Combine(AppContext.BaseDirectory, "best.onnx");
        Console.WriteLine($"onnxPath: {onnxPath}");
        var yolo = new DnnService(onnxPath);



        var imagePath = Path.Combine(AppContext.BaseDirectory, "test.jpg");
        Mat img = CvInvoke.Imread(imagePath);

        if (!img.IsEmpty)
        {
            Console.WriteLine("图片加载成功: " + img.Size);
        }
        else
        {
            Console.WriteLine("图片加载失败");
        }
        var boxes = yolo.Detect(img);
        if (boxes != null)
            foreach (var box in boxes)
            {
                Console.WriteLine("label:{0},score:{1},x:{2},y:{3},w:{4},h:{5}", box.Label, box.Score, box.X1, box.Y1, box.X2, box.Y2);
            }

        boxes = yolo.DetectByOnnx(img);
        if (boxes != null)
            foreach (var box in boxes)
            {
                Console.WriteLine("label:{0},score:{1},x:{2},y:{3},w:{4},h:{5}", box.Label, box.Score, box.X1, box.Y1, box.X2, box.Y2);
            }
        Console.ReadLine();

#if DEBUG
        using var capture = new VideoCapture(1, VideoCapture.API.DShow);
        capture.Set(CapProp.FourCC, VideoWriter.Fourcc('M', 'J', 'P', 'G'));
        capture.Set(CapProp.FrameWidth, 1280);
        capture.Set(CapProp.FrameHeight, 720);

#else
        int _cameraIndex = 10;
        int _cameraDefaultIndex = 8;
        Console.WriteLine($"正在连接摄像头({_cameraIndex})");
        var capture = new VideoCapture(_cameraIndex, VideoCapture.API.V4L2); // 使用 V4L2 后端
        while (!capture.IsOpened)
        {

            if (_cameraIndex > 20)
                _cameraIndex = _cameraDefaultIndex;
            Task.Delay(1000).Wait(); ;
            Console.WriteLine($"正在连接摄像头({_cameraIndex})");
            capture = new VideoCapture(_cameraIndex, VideoCapture.API.V4L2); // 使用 V4L2 后端

            _cameraIndex++;
        }
#endif

        if (capture.IsOpened)
        {
            Console.WriteLine("Camera opened successfully.");
        }
        else
        {
            Console.WriteLine("Failed to open camera.");
            return;
        }
        capture.Set(CapProp.FourCC, VideoWriter.Fourcc('M', 'J', 'P', 'G'));
        capture.Set(CapProp.FrameWidth, 1280);
        capture.Set(CapProp.FrameHeight, 720);
        CancellationTokenSource cts = new CancellationTokenSource();
        object frameLock = new object();
        Mat frame = new Mat();

        Mat lastFrame = new Mat();
        Mat displayFrame = new Mat();
        Mat copyFrame = new Mat();
        List<DetectedBox>? lastBoxes = null;
        DateTime lastFpsTime = DateTime.Now;
        int frameCount = 0;
        int onnxframeCount = 0;
        bool isInferencing = false;
        string str_fps = "";
        Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                if (capture.Grab())
                {
                    capture.Retrieve(frame);
                    lock (frameLock)
                    {
                        lastFrame.Dispose();
                        if (frame != null && !frame.IsEmpty)
                            lastFrame = frame.Clone();
                        frameCount++;
                    }
                }
            }
        }, cts.Token);
        double scale = 1;
        int x, y;
        var onnxNow = DateTime.Now;

        var now = DateTime.Now;
        while (!cts.Token.IsCancellationRequested)
        {
            Thread.Sleep(1);

            lock (frameLock)
            {
                if (lastFrame.IsEmpty) { continue; }
                displayFrame.Dispose();
                displayFrame = lastFrame.Clone();
                copyFrame = yolo.Letterbox(lastFrame, out scale, out x, out y);
            }
            //异步推理
            Task.Run(() =>
            {
                if (!isInferencing)
                {
                    isInferencing = true;
                    onnxframeCount++;

                    lastBoxes = yolo.DetectByOnnx(copyFrame);

                    isInferencing = false;
                }
            });
            //绘制检测框
            if (lastBoxes != null)
                foreach (var box in lastBoxes)
                {
                    CvInvoke.Rectangle(displayFrame,
                        new Rectangle((int)box.X1 - x, (int)box.Y1 - y, (int)(box.X2 - box.X1), (int)(box.Y2 - box.Y1)),
                        new MCvScalar(0, 0, 255), 2);
                    CvInvoke.PutText(displayFrame, box.Label,
                        new Point((int)box.X1 - x, (int)box.Y1 - y - 5),
                        FontFace.HersheySimplex, 0.5,
                        new MCvScalar(255, 255, 255));
                }

            // FPS 显示
            now = DateTime.Now;
            var span = now - lastFpsTime;
            if (span.TotalMilliseconds >= 1000)
            {
                double fps = frameCount / span.TotalSeconds;
                double onnxfps = onnxframeCount / span.TotalSeconds;
                str_fps = $"FPS: {fps:F1},DNN_FPS: {onnxfps:F1}";
                frameCount = 0;
                onnxframeCount = 0;
                lastFpsTime = now;
            }

            CvInvoke.PutText(displayFrame, str_fps,
                   new Point(10, 30),
                   FontFace.HersheySimplex, 1.0, new MCvScalar(0, 255, 0), 2);

            CvInvoke.Imshow("YOLOv11 Detect", displayFrame);

            if (CvInvoke.WaitKey(1) == 27) // ESC
                break;
        }

        // 释放资源
        cts.Cancel();

        System.Threading.Thread.Sleep(200); // 等待线程结束   
        capture.Dispose();
        frame.Dispose();
        displayFrame.Dispose();
        lastFrame.Dispose();
        copyFrame.Dispose();

    }
}
