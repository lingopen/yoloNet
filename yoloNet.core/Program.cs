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
        var rtspUrl = "rtsp://admin:@192.168.31.143:554/live/0/SUB";
        var onnxPath = Path.Combine(AppContext.BaseDirectory, "best.onnx");
        YoloService yolo = new YoloService(rtspUrl,onnxPath);
        yolo.Start();
        yolo.Stop();
    }
    
}
