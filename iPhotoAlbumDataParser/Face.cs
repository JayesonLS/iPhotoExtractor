using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iPhotoAlbumDataParser
{
    public class Face
    {
        public int Key { get; set; }
        public string Name { get; set; }
        public string KeyImage { get; set; }
        public int? KeyImageFaceIndex { get; set; }
        public int? PhotoCount { get; set; }
        public int? Order { get; set; }

        public override string ToString() { return Name; }
    }
}
