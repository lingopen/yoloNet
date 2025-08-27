using Avalonia;

namespace yoloNet.Models
{
    public class Annotation
    {
        public int ClassId { get; set; }
        public Rect BoundingBox { get; set; } // Avalonia Rect (x,y,w,h)
    }
}
