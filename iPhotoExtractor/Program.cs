using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using iPhotoAlbumDataParser;
using XmpCore;
using XmpCore.Options;

namespace iPhotoExtractor
{
    class Program
    {
        static int numFilesCopied = 0;
        static int numMetadataFilesCreated = 0;

        static void PrintUsage()
        {
            Console.WriteLine("Copys images out iPhoto library");
            Console.WriteLine("Usage: iPhotoExtractor preview|copy [options] <iPhoto library path> <dest folder path>");
            Console.WriteLine("Options: --unflaggedToExtrasFolders --copyOriginals --alwaysWriteMetadata");
        }

        static bool GetOptions(ref string[] args, out bool unflaggedToExtras, out bool copyOriginals, out bool alwaysWriteMetadata)
        {
            unflaggedToExtras = false;
            copyOriginals = false;
            alwaysWriteMetadata = false;

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
                    else if (arg == "--alwaysWriteMetadata")
                    {
                        alwaysWriteMetadata = true;
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

        static string CleanEventName(string name)
        {
            name = name.Replace('/', '-');

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return name;
        }

        static void MakeRollNamesUnique(AlbumData albumData)
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
                baseName = CleanEventName(baseName);

                string name = baseName;

                for (int i = 1; usedRollNames.Contains(name); i++)
                {
                    name = baseName + " (" + i.ToString() + ")";
                }

                if (name != roll.RollName)
                {
                    Console.WriteLine("Modifed event name from '" + roll.RollName + "' to '" + name + "'.");
                }

                roll.RollName = name;
            }
        }

        static string SanitizePathString(string pathString, bool allowSeparator)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                if (allowSeparator && (c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar))
                {
                    continue;
                }

                string replacement = String.Format("%0:X2", (int)c);
                pathString = pathString.Replace(c.ToString(), replacement);
            }

            return pathString;
        }

        static string CopyFile(string sourceRootPath, string sourceFilePath, string destRootPath, string destFolderPath, bool preview)
        {
            string sourcePath = Path.Combine(sourceRootPath, sourceFilePath);

            string fileName = Path.GetFileName(sourceFilePath);
            fileName = SanitizePathString(fileName, false);
            destFolderPath = SanitizePathString(destFolderPath, true);
            string destPath = Path.Combine(destRootPath, destFolderPath, fileName);

            // Console.WriteLine("Copying to '" + Path.Combine(destFolderPath, fileName) + "'.");

            if (!preview)
            {
                if (!File.Exists(sourcePath))
                {
                    Console.Error.WriteLine("Can't find source file '" + sourcePath + "', skipping.");
                    return null;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                if (File.Exists(destPath))
                {
                    Console.WriteLine("WARNING: Destination file already exists, skipping '" + Path.Combine(destFolderPath, fileName) + "'.");
                    return null;
                }
                else
                {
                    File.Copy(sourcePath, destPath);
                    numFilesCopied++;
                }
            }
            else
            {
                numFilesCopied++;
            }

            return destPath;
        }

        private static void WriteXmpMetaData(string metaFilePath, AlbumData albumData, MasterImage masterImage, bool alwaysWriteMetadata, bool preview)
        {
            bool writeMetadata = false;

            IXmpMeta xmp = XmpMetaFactory.Create();

            string fileName = Path.GetFileNameWithoutExtension(metaFilePath);
            if ((masterImage.Caption != fileName && !String.IsNullOrEmpty(masterImage.Caption)) || alwaysWriteMetadata)
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
                if (true)//!preview)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(metaFilePath));
                    if (File.Exists(metaFilePath))
                    {
                        Console.WriteLine("WARNING: XMP meta file already exists, skipping '" + metaFilePath + "'.");
                    }
                    else
                    {
                        using (var stream = File.OpenWrite(metaFilePath))
                        {
                            XmpMetaFactory.Serialize(xmp, stream, new SerializeOptions { OmitPacketWrapper = true });
                        }
                        numMetadataFilesCreated++;
                    }
                }
                else
                {
                    numMetadataFilesCreated++;
                }
            }
        }

        static void Main(string[] args)
        {
            try
            {
                bool unflaggedToExtras;
                bool copyOriginals;
                bool alwaysWriteMetadata;

                if (!GetOptions(ref args, out unflaggedToExtras, out copyOriginals, out alwaysWriteMetadata))
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
                AlbumData albumData = AlbumData.Load(iPhotoXmlPath);

                MakeRollNamesUnique(albumData);

                List<MasterImage> masterImages = albumData.MasterImages.Values.ToList();

                for (int imageIndex = 0; imageIndex < masterImages.Count; imageIndex++)
                {
                    if (imageIndex % 500 == 0)
                    {
                        Console.WriteLine(String.Format("Copying {0:0.0}% complete ({1:n0} of {2:n0})", imageIndex / (float)masterImages.Count * 100, imageIndex, masterImages.Count));
                    }

                    MasterImage masterImage = masterImages[imageIndex];

                    Roll roll = albumData.Rolls[(int)masterImage.Roll];
                    string destPath = roll.RollName;

                    if (unflaggedToExtras && !masterImage.Flagged)
                    {
                        destPath = Path.Combine(destPath, "Extras");
                    }

                    string destFilePath = CopyFile(iPhotoLibraryPath, masterImage.ImagePath, outputFolderPath, destPath, preview);
                    if (destFilePath != null)
                    {
                        // Copy was successful.

                        string metaFilePath = Path.ChangeExtension(destFilePath, ".xmp");
                        WriteXmpMetaData(metaFilePath, albumData, masterImage, alwaysWriteMetadata, preview);

                        if (copyOriginals &&  masterImage.OriginalPath != null && masterImage.OriginalPath != masterImage.ImagePath)
                        {
                            CopyFile(iPhotoLibraryPath, masterImage.OriginalPath, outputFolderPath, Path.Combine(destPath, "Originals"), preview);
                        }
                    }
                }

                Console.WriteLine(String.Format("Done. {0} files copied, {1} metadata files written.", numFilesCopied, numMetadataFilesCreated));
            }

            catch (Exception e)
            {
                Console.Error.WriteLine("Caught exception: " + e.ToString());
            }
        }
    }
}
