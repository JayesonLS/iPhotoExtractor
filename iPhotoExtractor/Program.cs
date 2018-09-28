using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using iPhotoAlbumDataParser;

namespace iPhotoExtractor
{
    class Program
    {
        static void PrintUsage()
        {
            Console.WriteLine("Copys images out iPhoto library");
            Console.WriteLine("Usage: iPhotoExtractor preview|copy [options] <iPhoto library path> <dest folder path>");
            Console.WriteLine("Options: --unflaggedToExtrasFolders");
        }

        static bool GetOptions(ref string[] args, out bool unflaggedToExtras)
        {
            unflaggedToExtras = false;

            List<string> retainedArgs = new List<string>();

            foreach (string arg in args)
            {
                if (arg.StartsWith("--"))
                {
                    if (arg == "--unflaggedToExtrasFolders")
                    {
                        unflaggedToExtras = true;
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

        static void CopyFile(string sourceRootPath, string sourceFilePath, string destRootPath, string destFolderPath, bool preview)
        {
            string sourcePath = Path.Combine(sourceRootPath, sourceFilePath);

            string fileName = Path.GetFileName(sourceFilePath);
            fileName = SanitizePathString(fileName, false);
            destFolderPath = SanitizePathString(destFolderPath, true);
            string destPath = Path.Combine(destRootPath, destFolderPath, fileName);

            Console.WriteLine("Copying to '" + Path.Combine(destFolderPath, fileName) + "'.");

            if (!preview)
            {
                File.Copy(sourcePath, destPath);
            }
        }


        static void Main(string[] args)
        {
            try
            {
                bool unflaggedToExtras;

                if (!GetOptions(ref args, out unflaggedToExtras))
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

                foreach (MasterImage masterImage in albumData.MasterImages.Values)
                {
                    Roll roll = albumData.Rolls[(int)masterImage.Roll];
                    string destPath = roll.RollName;

                    if (unflaggedToExtras && !masterImage.Flagged)
                    {
                        destPath = Path.Combine(destPath, "Extras");
                    }

                    CopyFile(iPhotoLibraryPath, masterImage.ImagePath, outputFolderPath, destPath, preview);

                    if (masterImage.OriginalPath != null && masterImage.OriginalPath != masterImage.ImagePath)
                    {
                        CopyFile(iPhotoLibraryPath, masterImage.OriginalPath, outputFolderPath, Path.Combine(destPath, "Originals"), preview);
                    }
                }

                Console.WriteLine("Done");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Caught exception: " + e.ToString());
            }
        }
    }
}
