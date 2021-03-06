﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace GrimDawnModMerger
{
    public class CommandLine // TODO make singleton
    {
        public BlockingCollection<Message> messageQueue;
        public event EventHandler<UILogMessageEventArgs> UILogMessageEvent;

        private Process cmd;
        private Thread cmdThread;
        private string gameDir;
        private bool threadRunning = false; // is this still needed?

        private HashSet<string> files;
        private int overwrittenFiles; // sort of placeholder

        public CommandLine(string gameDir, EventHandler<UILogMessageEventArgs> eventHandler)
        {
            this.gameDir = gameDir;
            messageQueue = new BlockingCollection<Message>(new ConcurrentQueue<Message>());
            UILogMessageEvent += eventHandler;
            files = new HashSet<string>();
            overwrittenFiles = 0;
        }

        public void Start()
        {
            overwrittenFiles = 0;
            cmdThread = new Thread(ThreadLoop);
            cmdThread.IsBackground = true;
            cmdThread.Start();
        }

        public void Kill(bool gracefully = true) // somewhat tested. None-graceful seems to work...
        {
            if (gracefully)
                messageQueue.Add(new Message { Command = "Close", Args = null });
            else
            {
                cmd.Kill();
                Trace.WriteLine("cmd is kill");
            }
            
            // kill archivetool if it is still running
            foreach (Process proc in Process.GetProcessesByName("ArchiveTool"))
            {
                Trace.WriteLine("Killing archivetool...");
                proc.Kill();
            }
        }

        public void ThreadLoop()
        {
            threadRunning = true;
            cmd = new Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.UseShellExecute = false;
            cmd.Start();

            cmd.BeginOutputReadLine();
            //int skippedCount = 0;
            cmd.OutputDataReceived += (sender, args) =>
            {
                if (args.Data == null)
                    return;
                // message culling test, looks a bit confusing tbh
                /*
               if (args.Data.Length > 2 && (args.Data[0] == '(' || (args.Data[0] == 'a' && args.Data[1] == 'd')))
               {
                   skippedCount++;
                   if (skippedCount > 10)
                   {
                       SendToUI(args.Data);
                       skippedCount = 0;
                   }
               }
               else
               {
                   SendToUI(args.Data);
               }
                */

                TrackFile(args.Data);

                SendToUI(args.Data);
                //Trace.WriteLine(args.Data);
            };
            cmd.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                    SendToUI(args.Data);
                //Trace.WriteLine(args.Data);
            };

            cmd.StandardInput.WriteLine("cd \"" + gameDir + "\"");
            cmd.StandardInput.FlushAsync();
            cmd.StandardInput.WriteLine("echo Test Test Test");
            cmd.StandardInput.FlushAsync();

            Trace.WriteLine("Thread " + Thread.CurrentThread.ManagedThreadId + " running...");
            while (threadRunning)
            {
                Message message = messageQueue.Take();
                switch (message.Command)
                {
                    case "Close":
                        Trace.WriteLine("Closing..");
                        threadRunning = false;
                        Close();
                        return;

                    case "Extract":
                        Extract(message.Args[0], message.Args[1]);
                        break;

                    case "ExtractDatabase":
                        ExtractDatabase(message.Args[0], message.Args[1]);
                        break;

                    case "Pack":
                        Pack(message.Args[0], message.Args[1], message.Args[2]);
                        break;

                    case "BuildDatabase":
                        BuildDatabase(message.Args[0], message.Args[1]);
                        break;
                }
            }

            Close();
        }

        public void Extract(string from, string to)
        {
            cmd.StandardInput.WriteLine(".\\archivetool \"" + from + "\" -extract \"" + to + "\"");
            cmd.StandardInput.Flush();
        }

        public void ExtractDatabase (string from, string to)
        {
            cmd.StandardInput.WriteLine(".\\archivetool \"" + from + "\" -database \"" + to + "\"");
            cmd.StandardInput.Flush();
        }

        public void Pack(string parentDir, string dir, string to)
        {
            cmd.StandardInput.WriteLine(".\\archivetool \"" + @to + "\" -update " + "." + " \"" + @parentDir + "\" 6");
            cmd.StandardInput.Flush();
        }

        public void BuildDatabase(string modDir, string gameDir)
        {
            cmd.StandardInput.WriteLine(Directory.GetCurrentDirectory() + "\\arzedit build \"" + modDir + "\" \"" + modDir + "\" -g \"" + gameDir + "\" -t \"" + modDir + "\\database\\templates\" -A -R");

            cmd.StandardInput.Flush();
        }

        public void Close()
        {
            cmd.StandardInput.Close();
            cmd.WaitForExit();
            if (overwrittenFiles > 0)
                SendToUI("\n\nWARNING: Overwrote " + overwrittenFiles + " files");
            SendToUI("\n<---- Merge Complete ---->");
            Trace.WriteLine("cmd closed");
        }

        private void SendToUI(string message)
        {
            UILogMessageEvent.Invoke(this, new UILogMessageEventArgs {Message = message});
        }

        private void TrackFile(string data)
        {
            if (data.Length < 5)
                return;

            // detect extraction
            if (data[0] == '(')
            {
                int s = 0;
                for (int i = 0; i < data.Length; i++)
                {
                    if (data[i] == ')')
                    {
                        s = i + 2;
                        if (data.Contains("Extracting "))
                            s += 11;
                        break;
                    }
                }
                string fileName = data.Substring(s);
                if (files.Contains(fileName))
                {
                    overwrittenFiles++;
                    Trace.WriteLine("overwriting " + fileName);
                }
                else
                {
                    files.Add(fileName);
                }
                //Trace.WriteLine(filename);
            }

            // detect addition
            if (data.Contains("added: "))
            {
                int s = 7;
                string fileName = data.Substring(s);
                if (files.Contains(fileName))
                {
                    overwrittenFiles++;
                    Trace.WriteLine("overwriting " + fileName);
                }
                else
                {
                    files.Add(fileName);
                }
                //Trace.WriteLine(filename);
            }
        }
    }
}
