using System.Drawing;
using System.Text.RegularExpressions;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Microsoft.ML.OnnxRuntime;
namespace yoloNet.core;
class Program
{
    static void Main(string[] args)
    {
        var rtspUrl = "1";// "rtsp://admin:@192.168.31.143:554/live/0/SUB";
        var onnxPath = Path.Combine(AppContext.BaseDirectory, "best.onnx");
        //var imagePath = Path.Combine(AppContext.BaseDirectory, "test.jpg");
        //Mat img = CvInvoke.Imread(imagePath);

        //if (!img.IsEmpty)
        //{
        //    Console.WriteLine("图片加载成功: " + img.Size);
        //}
        //else
        //{
        //    Console.WriteLine("图片加载失败");
        //}

        YoloService yolo = new YoloService(rtspUrl, onnxPath);


        //var rect = yolo.Detect(img.ToImage<Bgr, byte>(), onnxPath);


        //Console.ReadLine();
        yolo.Start();

        yolo.Stop();
    }

}
