using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iPhotoAlbumDataParser
{
    public class MasterImage
    {
        public string Caption { get; set; }
        public string Comment { get; set; }
        public string GUID { get; set; }
        public int? Roll{ get; set; }
        public int? Rating { get; set; }
        public string MediaType { get; set; }
        public double? ModDateAsTimerInterval { get; set; }
        public double? DateAsTimerInterval { get; set; }
        public double? DateAsTimerIntervalGMT { get; set; }
        public double? MetaModDateAsTimerInterval { get; set; }
        public bool Flagged { get; set; }

        public string ImagePath { get; set; }
        public string ThumbPath { get; set; }
        public string OriginalPath { get; set; }

        private string MakePathRelative(string path, string rootPath)
        {
            if (path == null)
            {
                return path;
            }
            else if (!path.StartsWith(rootPath))
            {
                throw new Exception("Path is not under rootPath.");
            }
            else
            {
                string result = path.Substring(rootPath.Length);
                while (result.StartsWith("\\") || result.StartsWith("/"))
                {
                    result = result.Substring(1);
                }

                return result;
            }
        }

        public void MakePathsRelative(string rootPath)
        {
            ImagePath = MakePathRelative(ImagePath, rootPath);
            ThumbPath = MakePathRelative(ThumbPath, rootPath);
            OriginalPath = MakePathRelative(OriginalPath, rootPath);
        }

        public override string ToString() { return Caption; }
    }
}
