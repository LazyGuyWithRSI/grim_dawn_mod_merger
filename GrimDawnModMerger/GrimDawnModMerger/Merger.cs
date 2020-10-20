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
        private EventHandler<UILogMessageEventArgs> UILogMessageCallback;

        private string currentMod; // !!DEBUG!!

        public Merger(EventHandler<UILogMessageEventArgs> UILogMessageCallback)
        {
            this.UILogMessageCallback = UILogMessageCallback;
        }

        public void Closing()
        {
            cmdLine.Kill();
        }

        public void Merge(string mergeName, List<string> modsToMerge)
        {
            //if (cmd != null)
                //cmd.Close();
            cmdLine = new CommandLine(GAME_DIR, UILogMessageCallback);
            Thread cmdThread = new Thread(cmdLine.ThreadLoop);
            cmdThread.IsBackground = true;
            cmdThread.Start();
            // for testing, wipe old combined dir
            string combinedDir = GAME_DIR + @"\mods\" + mergeName;

            // kill archivetool if it is still running
            foreach (Process proc in Process.GetProcessesByName("ArchiveTool"))
            {
                Trace.WriteLine("Killing archivetool...");
                proc.Kill();
            }

            if (Directory.Exists(combinedDir))
                Directory.Delete(combinedDir, true);

            Directory.CreateDirectory(combinedDir);
            Directory.CreateDirectory(combinedDir + @"\source");
            Directory.CreateDirectory(combinedDir + @"\database\templates");

            bool templatesPresent = false;

            foreach (string mod in modsToMerge)
            {
                currentMod = mod;
                string modDir = GAME_DIR + @"\mods\" + @mod;
                // raw copy any resources
                DirectoryCopy(modDir, combinedDir, true);
                DirectoryInfo dir = new DirectoryInfo(modDir + @"\resources");

                // decompile any .arc or .arz into their respective folders
                foreach (FileInfo file in dir.GetFiles())
                {
                    if (file.Extension.Equals(".arc"))
                    {
                        //cmd = new CommandLine(GAME_DIR);
                        Directory.CreateDirectory(combinedDir + @"\source");// + @file.Name.Substring(0, file.Name.Length - 4));
                        cmdLine.messageQueue.Add(new Message { Command = "Extract", Args = new string[] { file.FullName, combinedDir + @"\source" } });
                        //cmd.Extract(file.FullName, combinedDir + @"\source");// + @file.Name.Substring(0, file.Name.Length - 4));

                        cmdLine.messageQueue.Add(new Message { Command = "Pack", Args = new string[] { combinedDir + @"\source\" + @file.Name.Substring(0, file.Name.Length - 4), @file.Name.Substring(0, file.Name.Length - 4), combinedDir + @"\resources\" + file.Name } });
                        //cmd.Pack(combinedDir + @"\source\" + @file.Name.Substring(0, file.Name.Length - 4), @file.Name.Substring(0, file.Name.Length - 4), combinedDir + @"\resources\" + file.Name);
                        //cmd.Close();
                    }
                }
                dir = new DirectoryInfo(modDir + @"\database");

                foreach (FileInfo file in dir.GetFiles())
                {
                    if (file.Extension.Equals(".arz"))
                    {
                        //cmd = new CommandLine(GAME_DIR);
                        Directory.CreateDirectory(combinedDir + @"\database");// + @file.Name.Substring(0, file.Name.Length - 4));
                        cmdLine.messageQueue.Add(new Message { Command = "ExtractDatabase", Args = new string[] { file.FullName, combinedDir + @"\database" } });

                        //cmd.ExtractDatabase(file.FullName, combinedDir + @"\database");// + @file.Name.Substring(0, file.Name.Length - 4));
                        //cmd.Close();
                    }
                    else if (file.Extension.Equals(".arc"))
                    {

                        Directory.CreateDirectory(combinedDir + @"\database");// + @file.Name.Substring(0, file.Name.Length - 4));
                        cmdLine.messageQueue.Add(new Message { Command = "Extract", Args = new string[] { file.FullName, combinedDir + @"\database" } });
                        //cmd.Extract(file.FullName, combinedDir + @"\database");// + @file.Name.Substring(0, file.Name.Length - 4));
                        templatesPresent = true;
                    }
                }
            }
            cmdLine.messageQueue.Add(new Message { Command = "BuildDatabase", Args = new string[] { combinedDir, GAME_DIR, templatesPresent ? "1" : "0" } });
            //cmd.BuildDatabase(combinedDir, GAME_DIR, templatesPresent);
            cmdLine.messageQueue.Add(new Message { Command = "Close", Args = null });
            //cmd.Close();
        }


        private void DirectoryCopy (string sourceDirName, string destDirName, bool copySubDirs) // TODO maybe own class?
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
