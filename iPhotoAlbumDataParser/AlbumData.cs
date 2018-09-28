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
                Key = XElementParser.ParseIntValue(xmlElement, "key"),
                Name = XElementParser.ParseStringValue(xmlElement, "name"),
                KeyImage = XElementParser.ParseStringValue(xmlElement, "key image"),
                KeyImageFaceIndex = XElementParser.ParseNullableIntValue(xmlElement, "key image face index"),
                PhotoCount = XElementParser.ParseNullableIntValue(xmlElement, "PhotoCount"),
                Order = XElementParser.ParseNullableIntValue(xmlElement, "Order"),
            };
        }

        private static Rectangle ParseRectangle(XElement xmlElement)
        {
            if (xmlElement == null)
            {
                return null;
            }
            else
            {
                string stringified = xmlElement.Value;
                string simplified = stringified.Replace('{', ' ').Replace('}', ' ');
                string[] split = simplified.Split(',');

                return new Rectangle
                {
                    RectangleX = double.Parse(split[0]),
                    RectangleY = double.Parse(split[1]),
                    RectangleW = double.Parse(split[2]),
                    RectangleH = double.Parse(split[3]),
                };
            }
        }

        private static ImageFace CreateImageFace(XElement xmlElement)
        {
            return new ImageFace
            {
                FaceKey = XElementParser.ParseIntValue(xmlElement, "face key"),
                FaceIndex = XElementParser.ParseNullableIntValue(xmlElement, "face index"),
                Rectangle = ParseRectangle(XElementParser.GetElementForKey(xmlElement, "rectangle")),
            };
        }

        private static List<ImageFace> ParseImageFaces(XElement xmlElement)
        {
            if (xmlElement == null)
            {
                return null;
            }
            else
            {
                List<ImageFace> result = xmlElement.Elements("dict").Where(d => d.Elements("key").Count() > 1).Select(d => CreateImageFace(d)).ToList();
                return result.Count > 0 ? result : null;
            }
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
                Latitude = XElementParser.ParseNullableDoubleValue(xmlElement, "latitude"),
                Longitude = XElementParser.ParseNullableDoubleValue(xmlElement, "longitude"),
                Faces = ParseImageFaces(XElementParser.GetElementForKey(xmlElement, "Faces")),

                ImagePath = XElementParser.ParseStringValue(xmlElement, "ImagePath"),
                ThumbPath = XElementParser.ParseStringValue(xmlElement, "ThumbPath"),
                OriginalPath = XElementParser.ParseStringValue(xmlElement, "OriginalPath"),
            };
        }

        // Debug code to gather all of the different master image keys.
        private void DumpMasterImageKeyNames(XElement rootDict)
        {
            HashSet<string> keys = new HashSet<string>();

            foreach (XElement xmlElement in XElementParser.GetElementForKey(rootDict, "Master Image List").Elements("dict").Where(d => d.Elements("key").Count() > 1))
            {
                foreach (XElement keyElement in xmlElement.Elements("key"))
                {
                    keys.Add(keyElement.Value);
                }
            }

            foreach (string key in keys)
            {
                Console.WriteLine("Master image key: " + key);
            }
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

            // DumpMasterImageKeyNames(rootDict);
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
