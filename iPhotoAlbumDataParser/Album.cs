using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iPhotoAlbumDataParser
{
    public class Album
    {
        public int AlbumId { get; set; }
        public string AlbumName { get; set; }
        public string AlbumType { get; set; }
        public string GUID { get; set; }
        public int? SortOrder { get; set; }
        public List<int> KeyList { get; set; }

        public override string ToString() { return AlbumName; }
    }
}
