using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace GrimDawnModMerger
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<ModListItem> modList { get; set; }
        Merger merger;

        public MainWindow ()
        {
            InitializeComponent();

            modList = new ObservableCollection<ModListItem>();
            this.DataContext = this;
            merger = new Merger(UILogMessageHandler);

            string savedGameDir = @Properties.Settings.Default.gameDir;
            if (savedGameDir.Length > 0)
                txtBoxGamePath.Text = savedGameDir;
        }

        private void btnUp_Click (object sender, RoutedEventArgs e)
        {
            MoveModInList(-1);
        }

        private void btnDown_Click (object sender, RoutedEventArgs e)
        {
            MoveModInList(1);
        }

        private void btnSelectPath_Click (object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            //dialog.InitialDirectory = "C:\\Users";
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                txtBoxGamePath.Text = dialog.FileName;
            }
        }

        private void btnMerge_Click (object sender, RoutedEventArgs e)
        {
            if (txtBoxMergeModName.Text.Length == 0)
                return;

            // get all active mods
            Trace.WriteLine("Btn merge clicked");
            List<string> activeMods = new List<string>();
            foreach (ModListItem item in modList)
            {
                if (item.IsSelected)
                {
                    activeMods.Add(item.Name);
                    Trace.WriteLine(item.Name + ", is checked: " + item.IsSelected);
                }
            }
            btnMerge.Content = "MERGE IN\nPROGRESS";
            btnMerge.IsEnabled = false;
            activeMods.Reverse();
            merger.Merge(txtBoxMergeModName.Text, activeMods);
        }

        private void txtBoxGamePath_TextChanged (object sender, TextChangedEventArgs e)
        {
            merger.GAME_DIR = @txtBoxGamePath.Text;

            // scan new directory for mods
            // TODO in future, save last configuration (isselected and position), as well as what mods were last merged
            DirectoryInfo dirInfo = new DirectoryInfo(merger.GAME_DIR + @"\mods");
            modList.Clear();
            foreach(DirectoryInfo d in dirInfo.GetDirectories())
            {
                if (d.Name.Equals("gdx1") || d.Name.Equals("gdx2"))
                    continue;
                modList.Add(new ModListItem(false, d.Name));
            }
            Properties.Settings.Default.gameDir = @txtBoxGamePath.Text;
            Properties.Settings.Default.Save();
        }

        private void btnTest_Click (object sender, RoutedEventArgs e)
        {
        }

        public void UILogMessageHandler(object o, UILogMessageEventArgs e)
        {
            try // exception thrown when exiting, it is a problem with the cmd line thread exiting. TODO look into this
            {
                Dispatcher.Invoke(() =>
                {
                    if (e.Message.Contains("Parsing")) // just for fun, and a bit of clarity for the user
                    e.Message += "\nPacking Database...";

                    if (e.Message.Contains("Merge Complete"))
                    {
                        btnMerge.Content = "Merge";
                        btnMerge.IsEnabled = true;
                    }
                    if (txtBoxOutput.LineCount > 300)
                    {
                        var lines = (from item in txtBoxOutput.Text.Split('\n') select item.Trim());
                        lines = lines.Skip(200);
                        txtBoxOutput.Text = string.Join(Environment.NewLine, lines.ToArray());
                    }

                    txtBoxOutput.AppendText("\n" + e.Message);
                    txtBoxOutput.ScrollToEnd();
                //txtBoxOutput.Text += "\n" + e.Message;
            });
            } catch (Exception ex) { Trace.WriteLine(ex); }
        }

        private void Window_Closing (object sender, System.ComponentModel.CancelEventArgs e)
        {
            merger.Closing();
        }

        private void MoveModInList (int delta)
        {
            ModListItem item;
            int index;

            if (listBoxMods.SelectedItems.Count != 1)
                return;

            item = (ModListItem)listBoxMods.SelectedItems[0];
            index = modList.IndexOf(item);
            if (index > 0)
            {
                modList.Move(index, index + delta);
            }
        }

    }

    public class ModListItem
    {
        public string Name { get; set; }
        public bool IsSelected { get; set; }

        public ModListItem (bool isSelected, string name)
        {
            Name = name;
            IsSelected = isSelected;
        }
    }
}
