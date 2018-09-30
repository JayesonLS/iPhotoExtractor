using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using iPhotoAlbumDataParser;
using XmpCore;
using XmpCore.Options;
using System.Text.RegularExpressions;

namespace iPhotoExtractor
{
    class Program
    {
        int numFilesCopied = 0;
        int numMetadataFilesCreated = 0;
        Dictionary<string, HashSet<string>> uniqueFilenameTracking = new Dictionary<string, HashSet<string>>();
        AlbumData albumData;

        void PrintUsage()
        {
            Console.WriteLine("Copies images out iPhoto library");
            Console.WriteLine("Usage: iPhotoExtractor preview|copy [options] <iPhoto library path> <dest folder path>");
            Console.WriteLine("Options: --unflaggedToExtrasFolders --copyOriginals --writeMetadataFiles --alwaysWriteMetadata --prependDateToEventNames");
        }

        bool GetOptions(ref string[] args, out bool unflaggedToExtras, out bool copyOriginals, out bool writeMetadataFiles, out bool alwaysWriteMetadata, out bool prependDateToEventNames)
        {
            unflaggedToExtras = false;
            copyOriginals = false;
            writeMetadataFiles = false;
            alwaysWriteMetadata = false;
            prependDateToEventNames = true;

            List<string> retainedArgs = new List<string>();

            foreach (string arg in args)
            {
                if (arg.StartsWith("--"))
                {
                    if (arg == "--unflaggedToExtrasFolders")
                    {
                        unflaggedToExtras = true;
                    }
                    else if (arg == "--copyOriginals")
                    {
                        copyOriginals = true;
                    }
                    else if (arg == "--writeMetadataFiles")
                    {
                        writeMetadataFiles = true;
                    }
                    else if (arg == "--alwaysWriteMetadata")
                    {
                        alwaysWriteMetadata = true;
                    }
                    else if (arg == "--prependDateToEventNames")
                    {
                        prependDateToEventNames = true;
                    }
                    else
                    {
                        Console.Error.WriteLine("Invalid option '" + arg + "'.");
                        return false;
                    }
                }
                else
                {
                    retainedArgs.Add(arg);
                }
            }

            args = retainedArgs.ToArray();

            return true;
        }

        string CleanFileOrDirectoryName(string name)
        {
            name = name.Replace('/', '-');

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return name;
        }

        void RemoveUnreferencedRolls()
        {
            HashSet<int> usedRollKeys = new HashSet<int>();

            foreach (MasterImage masterImage in albumData.MasterImages.Values)
            {
                usedRollKeys.Add((int)masterImage.Roll);
            }

            foreach (int rollKey in albumData.Rolls.Keys.ToArray())
            {
                if (!usedRollKeys.Contains(rollKey))
                {
                    Roll roll = albumData.Rolls[rollKey];

                    Console.WriteLine("Removing unreferenced event '" + roll.RollName + "'.");

                    albumData.Rolls.Remove(rollKey);
                }
            }
        }

        void PrependDateToEventNames()
        {
            foreach (Roll roll in albumData.Rolls.Values)
            {
                double earliestDate = double.MaxValue; 

                foreach (int imageKey in roll.KeyList)
                {
                    MasterImage masterImage;
                    if (albumData.MasterImages.TryGetValue(imageKey, out masterImage))
                    {
                        if (masterImage.DateAsTimerInterval != null)
                        {
                            double imageDate = (double)masterImage.DateAsTimerInterval;

                            if (imageDate != 0 && imageDate < earliestDate)
                            {
                                earliestDate = imageDate;
                            }
                        }
                    }
                }

                if (earliestDate != double.MaxValue)
                {
                    long ticks = (long)(earliestDate * (double)TimeSpan.TicksPerSecond);
                    TimeSpan timeSpan = new TimeSpan(ticks);
                    DateTime date = new DateTime(2001, 1, 1);
                    date = date.Add(timeSpan);

                    string prefix = String.Format("{0:0000}-{1:00}-{2:00} ", date.Year, date.Month, date.Day);

                    roll.RollName = prefix + roll.RollName;
                }
            }
        }

        void MakeRollNamesUnique()
        {
            HashSet<string> usedRollNames = new HashSet<string>();

            foreach (Roll roll in albumData.Rolls.Values)
            {
                string baseName = roll.RollName;
                if (String.IsNullOrEmpty(baseName))
                {
                    baseName = "unnamed event";
                }

                baseName = baseName.Replace('\\', '_');
                baseName = CleanFileOrDirectoryName(baseName);

                string name = baseName;

                for (int i = 1; usedRollNames.Contains(name.ToLower()); i++)
                {
                    name = baseName + " (" + i.ToString() + ")";
                }
                usedRollNames.Add(name.ToLower());

                if (name != roll.RollName)
                {
                    Console.WriteLine("Modifed event name from '" + roll.RollName + "' to '" + name + "'.");
                }

                roll.RollName = name;
            }
        }

        bool IsEquivalent(string title, string fileNameOrPathWithExtension)
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileNameOrPathWithExtension);
            string fileNameExtension = Path.GetExtension(fileNameOrPathWithExtension);

            title = title.Trim();

            if (title == fileNameWithoutExtension)
            {
                return true;
            }

            // Sometimes the image name ends in "_n" (number) versus the title with " copy". _n is a better approach.
            while (title.EndsWith(" copy") && Regex.IsMatch(fileNameWithoutExtension, @"_\d+$"))
            {
                title = title.Substring(0, title.Length - " copy".Length);
                fileNameWithoutExtension = fileNameWithoutExtension.Substring(0, fileNameWithoutExtension.LastIndexOf('_'));

                if (title == fileNameWithoutExtension)
                {
                    return true;
                }
            }

            // Sometimes the name is the file name with the extension. 
            // We want to filter this out, don't want files named image.jpg.jpg for example.
            int titleLastPeriodIndex = title.LastIndexOf('.');
            if (titleLastPeriodIndex > 0)
            {
                string titleExtension = title.Substring(titleLastPeriodIndex);
                if (!String.IsNullOrEmpty(titleExtension) &&
                    titleExtension.ToLower() == fileNameExtension.ToLower())
                {
                    title = title.Substring(0, title.Length - titleExtension.Length);

                    if (title == fileNameWithoutExtension)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool HasMatchingExtension(string title, string fileNameOrPathWithExtension)
        {
            title = title.Trim();

            int titleLastPeriodIndex = title.LastIndexOf('.');
            if (titleLastPeriodIndex > 0)
            {
                string titleExtension = title.Substring(titleLastPeriodIndex);
                if (!String.IsNullOrEmpty(titleExtension) &&
                    titleExtension.ToLower() == Path.GetExtension(fileNameOrPathWithExtension).ToLower())
                {
                    return true;
                }
            }

            return false;
        }

        string GetUniqueFileNameForImage(string destRollPath, MasterImage masterImage)
        {
            string extension = Path.GetExtension(masterImage.ImagePath);
            string imageFileName = Path.GetFileNameWithoutExtension(masterImage.ImagePath);
            string desiredName = imageFileName;

            if (!String.IsNullOrWhiteSpace(masterImage.Caption) && 
                !IsEquivalent(masterImage.Caption, masterImage.ImagePath) &&
                !HasMatchingExtension(masterImage.Caption, masterImage.ImagePath))
            {
                desiredName = masterImage.Caption.Trim();

                int maxNameLen = Math.Max(80, imageFileName.Length);
                if (desiredName.Length > maxNameLen)
                {
                    desiredName = desiredName.Substring(0, maxNameLen);
                }
            }

            desiredName = CleanFileOrDirectoryName(desiredName);

            // Make name unique.
            HashSet<string> usedNames;
            if (!uniqueFilenameTracking.TryGetValue(destRollPath.ToLower(), out usedNames))
            {
                usedNames = new HashSet<string>();
                uniqueFilenameTracking[destRollPath.ToLower()] = usedNames;
            }

            string name = desiredName;

            for (int i = 1; usedNames.Contains((name + extension).ToLower()); i++)
            {
                name = desiredName + " (" + i.ToString() + ")";
            }
            usedNames.Add((name + extension).ToLower());

            if (name != imageFileName)
            {
                // Console.WriteLine("Modifed image name from '" + Path.Combine(destRollPath, imageFileName + extension) + "' to '" + Path.Combine(destRollPath, name + extension) + "'.");
            }

            return name + extension;
        }

        bool CopyFile(string sourceRootPath, string sourceFilePath, string destPath, bool preview)
        {
            string sourcePath = Path.Combine(sourceRootPath, sourceFilePath);

            // Console.WriteLine("Copying to '" + Path.Combine(destFolderPath, fileName) + "'.");

            if (!File.Exists(sourcePath))
            {
                // Try sanitizing the source file path. Mac allows characters that windows does not, like ":".
                sourceFilePath = sourceFilePath.Replace(":", "_");
                sourcePath = Path.Combine(sourceRootPath, sourceFilePath);

                if (!File.Exists(sourcePath))
                {
                    Console.Error.WriteLine("ERROR: Can't find source file '" + sourcePath + "', skipping.");
                    return false;
                }
            }

            if (File.Exists(destPath))
            {
                Console.Error.WriteLine("ERROR: Destination file already exists, skipping '" + destPath + "'.");
                return false;
            }

            if (!preview)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                // File.WriteAllText(destPath, "Placeholder"); // For debugging output more quickly than a full copy.
                File.Copy(sourcePath, destPath);
            }

            numFilesCopied++;

            return true;
        }
        
        private void WriteXmpMetaData(string imageFilePath, MasterImage masterImage, bool alwaysWriteMetadata, bool preview)
        {
            bool writeMetadata = false;

            IXmpMeta xmp = XmpMetaFactory.Create();

            if ((!String.IsNullOrEmpty(masterImage.Caption) && !IsEquivalent(masterImage.Caption, imageFilePath)) || alwaysWriteMetadata)
            {
                xmp.AppendArrayItem(XmpConstants.NsDC, "dc:title", new PropertyOptions { IsArrayAlternate = true }, masterImage.Caption, null);
                writeMetadata = true;
            }

            if (!String.IsNullOrEmpty(masterImage.Comment))
            {
                xmp.AppendArrayItem(XmpConstants.NsDC, "dc:description", new PropertyOptions { IsArrayAlternate = true }, masterImage.Comment, null);
                writeMetadata = true;
            }

            if (masterImage.Rating != null && (int)masterImage.Rating > 0)
            {
                xmp.SetProperty(XmpConstants.NsXmp, "xmp:Rating", ((int)masterImage.Rating).ToString());
                writeMetadata = true;
            }

            // TODO: Handle faces.

            if (writeMetadata)
            {
                string metaFilePath = Path.ChangeExtension(imageFilePath, ".xmp");

                if (File.Exists(metaFilePath))
                {
                    Console.Error.WriteLine("ERROR: XMP meta file already exists, skipping '" + metaFilePath + "'.");
                }
                else if (!preview)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(metaFilePath));

                    using (var stream = File.OpenWrite(metaFilePath))
                    {
                        XmpMetaFactory.Serialize(xmp, stream, new SerializeOptions { OmitPacketWrapper = true });
                    }

                    numMetadataFilesCreated++;
                }
            }
        }

        public void Run(string[] args)
        {
            bool unflaggedToExtras;
            bool copyOriginals;
            bool writeMetadataFiles;
            bool alwaysWriteMetadata;
            bool prependDateToEventNames;

            if (!GetOptions(ref args, out unflaggedToExtras, out copyOriginals, out writeMetadataFiles, out alwaysWriteMetadata, out prependDateToEventNames))
            {
                PrintUsage();
                return;
            }

            if (args.Length != 3)
            {
                PrintUsage();
                return;
            }

            string mode = args[0];
            string iPhotoLibraryPath = args[1];
            string outputFolderPath = args[2];

            if (mode != "preview" && mode != "copy")
            {
                Console.Error.WriteLine("Invalid mode '" + mode + "'.");
                PrintUsage();
                return;
            }

            if (!Directory.Exists(iPhotoLibraryPath))
            {
                Console.Error.WriteLine("Can't find iphoto library folder '" + iPhotoLibraryPath + "'.");
                PrintUsage();
                return;
            }

            if (!Directory.Exists(outputFolderPath))
            {
                Console.Error.WriteLine("Can't find output folder'" + outputFolderPath + "'.");
                PrintUsage();
                return;
            }

            string iPhotoXmlPath = Path.Combine(iPhotoLibraryPath, "AlbumData.xml");
            if (!File.Exists(iPhotoXmlPath))
            {
                Console.Error.WriteLine("Can't find iphoto ablum data XML '" + iPhotoXmlPath + "'.");
                PrintUsage();
                return;
            }

            bool preview = mode == "preview";
            albumData = AlbumData.Load(iPhotoXmlPath);

            RemoveUnreferencedRolls();

            if (prependDateToEventNames)
            {
                PrependDateToEventNames();
            }

            MakeRollNamesUnique();

            List<MasterImage> masterImages = albumData.MasterImages.Values.ToList();
            for (int imageIndex = 0; imageIndex < masterImages.Count; imageIndex++)
            {
                if (imageIndex % 500 == 0)
                {
                    Console.WriteLine(String.Format("Copying {0:0.0}% complete ({1:n0} of {2:n0})", imageIndex / (float)masterImages.Count * 100, imageIndex, masterImages.Count));
                }

                MasterImage masterImage = masterImages[imageIndex];

                Roll roll = albumData.Rolls[(int)masterImage.Roll];
                string destRollPath = roll.RollName;

                if (unflaggedToExtras && !masterImage.Flagged)
                {
                    destRollPath = Path.Combine(destRollPath, "Extras");
                }

                string destFileName = GetUniqueFileNameForImage(destRollPath, masterImage);
                string destFilePath = Path.Combine(outputFolderPath, destRollPath, destFileName);

                if (CopyFile(iPhotoLibraryPath, masterImage.ImagePath, destFilePath, preview))
                {
                    if (writeMetadataFiles || alwaysWriteMetadata)
                    {
                        WriteXmpMetaData(destFilePath, masterImage, alwaysWriteMetadata, preview);
                    }

                    if (copyOriginals && masterImage.OriginalPath != null && masterImage.OriginalPath != masterImage.ImagePath)
                    {
                        string originalDestFilePath = Path.Combine(Path.GetDirectoryName(destFilePath), "Originals", Path.GetFileNameWithoutExtension(destFilePath) + Path.GetExtension(masterImage.OriginalPath));
                        CopyFile(iPhotoLibraryPath, masterImage.OriginalPath, originalDestFilePath, preview);
                    }
                }
            }

            Console.WriteLine(String.Format("Done. {0} files copied, {1} metadata files written.", numFilesCopied, numMetadataFilesCreated));
        }

        static void Main(string[] args)
        {
            try
            {
                Program program = new Program();
                program.Run(args);
            }

            catch (Exception e)
            {
                Console.Error.WriteLine("Caught exception: " + e.ToString());
                Console.Error.WriteLine(e.StackTrace);
            }
        }
    }
}
