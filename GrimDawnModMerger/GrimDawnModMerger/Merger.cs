using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Threading;

namespace GrimDawnModMerger
{
    class Merger
    {
        public string GAME_DIR { get; set; }

        private CommandLine cmdLine;
        //private Thread cmdThread; // TODO why is the merger handling the threading...
        private EventHandler<UILogMessageEventArgs> UILogMessageCallback;

        private string currentMod; // !!DEBUG!!

        public Merger(EventHandler<UILogMessageEventArgs> UILogMessageCallback)
        {
            this.UILogMessageCallback = UILogMessageCallback;
        }

        public void Closing()
        {
            cmdLine.Kill(gracefully: false);
        }

        public void Merge(string mergeName, List<string> modsToMerge)
        {
            cmdLine = new CommandLine(GAME_DIR, UILogMessageCallback);
            cmdLine.Start();

            // for testing, wipe old combined dir
            string combinedDir = GAME_DIR + @"\mods\" + mergeName;

            // kill archivetool if it is still running
            foreach (Process proc in Process.GetProcessesByName("ArchiveTool"))
            {
                Trace.WriteLine("Killing archivetool...");
                proc.Kill();
            }

            SetupModDirectory(combinedDir);

            // merge mods
            foreach (string mod in modsToMerge)
            {
                currentMod = mod;
                string modDir = GAME_DIR + @"\mods\" + @mod;
                AddMod(modDir, combinedDir);
            }

            // build .arz
            cmdLine.messageQueue.Add(new Message { Command = "BuildDatabase", Args = new string[] { combinedDir, GAME_DIR } });
            //cmdLine.messageQueue.Add(new Message { Command = "Close", Args = null });
            cmdLine.Kill();
        }

        private void AddMod(string modDir, string combinedDir)
        {
            // raw copy any resources
            DirectoryCopy(modDir, combinedDir, true);
            DirectoryInfo dir = new DirectoryInfo(modDir + @"\resources");

            // decompile any .arc or .arz into their respective folders
            foreach (FileInfo file in dir.GetFiles())
            {
                if (file.Extension.Equals(".arc"))
                {
                    ExtractArc(file, combinedDir + @"\source", cmdLine);

                    cmdLine.messageQueue.Add(new Message { Command = "Pack", Args = new string[] { combinedDir + @"\source\" + @file.Name.Substring(0, file.Name.Length - 4), @file.Name.Substring(0, file.Name.Length - 4), combinedDir + @"\resources\" + file.Name } });
                }
            }

            dir = new DirectoryInfo(modDir + @"\database");

            foreach (FileInfo file in dir.GetFiles())
            {
                if (file.Extension.Equals(".arz"))
                    ExtractArz(file, combinedDir + @"\database", cmdLine);

                else if (file.Extension.Equals(".arc"))
                    ExtractArc(file, combinedDir + @"\database", cmdLine);
            }
        }

        private void ExtractArc(FileInfo file, string destination, CommandLine cmdLine) // TODO move this out of merger
        {
            Directory.CreateDirectory(destination);// + @file.Name.Substring(0, file.Name.Length - 4));
            cmdLine.messageQueue.Add(new Message { Command = "Extract", Args = new string[] { file.FullName, destination } });
        }

        private void ExtractArz (FileInfo file, string destination, CommandLine cmdLine) // TODO move this out of merger
        {
            Directory.CreateDirectory(destination);// + @file.Name.Substring(0, file.Name.Length - 4));
            cmdLine.messageQueue.Add(new Message { Command = "ExtractDatabase", Args = new string[] { file.FullName, destination } });
        }

        private void SetupModDirectory(string combinedDir) // TODO move this out of merger
        {
            if (Directory.Exists(combinedDir))
                Directory.Delete(combinedDir, true);

            Directory.CreateDirectory(combinedDir);
            Directory.CreateDirectory(combinedDir + @"\source");
            Directory.CreateDirectory(combinedDir + @"\database\templates");
        }

        private void DirectoryCopy (string sourceDirName, string destDirName, bool copySubDirs)  // TODO move this out of merger
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();

            // If the destination directory doesn't exist, create it.       
            Directory.CreateDirectory(destDirName);

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                if (file.Extension.Equals(".arz") || file.Extension.Equals(".arc"))
                    continue;

                string tempPath = Path.Combine(destDirName, file.Name);
                try
                {
                    file.CopyTo(tempPath, false);
                } catch (Exception e)
                {
                    Trace.WriteLine(currentMod + " is overwriting " + file.Name);
                    file.CopyTo(tempPath, true);
                }
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string tempPath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
                }
            }
        }
    }
}
