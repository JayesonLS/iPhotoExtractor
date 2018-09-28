using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iPhotoAlbumDataParser
{
    public class Roll
    {
        public int RollID { get; set; }
        public string ProjectUuid { get; set; }
        public string RollName { get; set; }
        public double? RollDateAsTimerInterval { get; set; }
        public int? KeyPhotoKey { get; set; }
        public int? PhotoCount { get; set; }
        public List<int> KeyList { get; set; }

        public override string ToString() { return RollName; }
    }
}
