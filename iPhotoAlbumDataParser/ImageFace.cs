using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iPhotoAlbumDataParser
{
    public class Rectangle
    {
        public double RectangleX { get; set; }
        public double RectangleY { get; set; }
        public double RectangleW { get; set; }
        public double RectangleH { get; set; }
    }

    public class ImageFace
    {
        public int FaceKey { get; set; }
        public Rectangle Rectangle { get; set; }
        public int? FaceIndex { get; set; }
    }
}
