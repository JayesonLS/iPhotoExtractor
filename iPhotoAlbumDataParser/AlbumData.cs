using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.IO;

namespace iPhotoAlbumDataParser
{
    public class AlbumData
    {
        public Dictionary<int, Album> Albums { get; private set; }
        public Dictionary<int, Roll> Rolls { get; private set; }
        public Dictionary<int, Face> Faces { get; private set; }
        public Dictionary<int, MasterImage> MasterImages { get; private set; }

        private AlbumData()
        {
        }

        private static Album CreateAlbum(XElement xmlElement)
        {
            return new Album
            {
                AlbumId = XElementParser.ParseIntValue(xmlElement, "AlbumId"),
                AlbumName = XElementParser.ParseStringValue(xmlElement, "AlbumName"),
                AlbumType = XElementParser.ParseStringValue(xmlElement, "Album Type"),
                GUID = XElementParser.ParseStringValue(xmlElement, "GUID"),
                SortOrder = XElementParser.ParseNullableIntValue(xmlElement, "Sort Order"),
                KeyList = XElementParser.ParseIntArray(xmlElement, "KeyList"),
            };
        }

        private static Roll CreateRoll(XElement xmlElement)
        {
            return new Roll
            {
                RollID = XElementParser.ParseIntValue(xmlElement, "RollID"),
                ProjectUuid = XElementParser.ParseStringValue(xmlElement, "ProjectUuid"),
                RollName = XElementParser.ParseStringValue(xmlElement, "RollName"),
                RollDateAsTimerInterval = XElementParser.ParseNullableDoubleValue(xmlElement, "RollDateAsTimerInterval"),
                KeyPhotoKey = XElementParser.ParseNullableIntValue(xmlElement, "KeyPhotoKey"),
                PhotoCount = XElementParser.ParseNullableIntValue(xmlElement, "PhotoCount"),
                KeyList = XElementParser.ParseIntArray(xmlElement, "KeyList"),
            };
        }

        private static Face CreateFace(XElement xmlElement)
        {
            return new Face
            {
                Name = XElementParser.ParseStringValue(xmlElement, "name"),
            };
        }

        private static MasterImage CreateMasterImage(XElement xmlElement)
        {
            return new MasterImage
            {
                Caption = XElementParser.ParseStringValue(xmlElement, "Caption"),
                Comment = XElementParser.ParseStringValue(xmlElement, "Comment"),
                GUID = XElementParser.ParseStringValue(xmlElement, "GUID"),
                Roll = XElementParser.ParseNullableIntValue(xmlElement, "Roll"),
                Rating = XElementParser.ParseNullableIntValue(xmlElement, "Rating"),
                MediaType = XElementParser.ParseStringValue(xmlElement, "MediaType"),
                ModDateAsTimerInterval = XElementParser.ParseNullableDoubleValue(xmlElement, "ModDateAsTimerInterval"),
                DateAsTimerInterval = XElementParser.ParseNullableDoubleValue(xmlElement, "DateAsTimerInterval"),
                DateAsTimerIntervalGMT = XElementParser.ParseNullableDoubleValue(xmlElement, "DateAsTimerIntervalGMT"),
                MetaModDateAsTimerInterval = XElementParser.ParseNullableDoubleValue(xmlElement, "MetaModDateAsTimerInterval"),
                Flagged = XElementParser.ParseBoolean(xmlElement, "Flagged"),
                ImagePath = XElementParser.ParseStringValue(xmlElement, "ImagePath"),
                ThumbPath = XElementParser.ParseStringValue(xmlElement, "ThumbPath"),
                OriginalPath = XElementParser.ParseStringValue(xmlElement, "OriginalPath"),
            };
        }

        private void ParseXml(XDocument xmlDoc)
        {
            var rootDict = xmlDoc.Descendants("dict").FirstOrDefault();
            string rootPath = XElementParser.ParseStringValue(rootDict, "Archive Path");

            Albums = XElementParser.GetElementForKey(rootDict, "List of Albums").Elements("dict").Where(d => d.Elements("key").Count() > 1).Select(d => CreateAlbum(d)).ToDictionary(a => a.AlbumId);
            Rolls = XElementParser.GetElementForKey(rootDict, "List of Rolls").Elements("dict").Where(d => d.Elements("key").Count() > 1).Select(d => CreateRoll(d)).ToDictionary(a => a.RollID);
            Faces = XElementParser.ParseKeyDictPairs<Face>(XElementParser.GetElementForKey(rootDict, "List of Faces"), CreateFace);
            MasterImages = XElementParser.ParseKeyDictPairs<MasterImage>(XElementParser.GetElementForKey(rootDict, "Master Image List"), CreateMasterImage);

            foreach (MasterImage masterImage in MasterImages.Values)
            {
                masterImage.MakePathsRelative(rootPath);
            }
        }

        // Pass in path to iPhoto Library.photolibrary/AlbumData.xml
        public static AlbumData Load(string albumDataXmlPath)
        {
            string xmlText = File.ReadAllText(albumDataXmlPath);
            XDocument xmlDoc = XDocument.Parse(xmlText);

            AlbumData result = new AlbumData();
            result.ParseXml(xmlDoc);

            return result;
        }
    }
}
