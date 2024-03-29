﻿using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.WindowsAPICodePack.Dialogs;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using System.Text.RegularExpressions;
using Emgu.CV;

namespace ImageToPDFWpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Task ConvertTask;

        public MainWindow()
        {
            InitializeComponent();
            WidthSettingComboBox.SelectedIndex = Properties.Settings.Default.WidthSetting;
            SetWidthTextBox.Text = Properties.Settings.Default.SetWidthSetting;
            OpenAfterCheckBox.IsChecked = Properties.Settings.Default.OpenAfterSetting;
        }

        //Begin conversion process
        private void ConvertImagesButton_Click(object sender, RoutedEventArgs e)
        {
            //check that the output file name is a valid pdf file name
            string filename = Regex.Match(OutputFileNameTextBox.Text, @"[^\\]+\.pdf", RegexOptions.IgnoreCase).Value;
            string filepath = Regex.Replace(OutputFileNameTextBox.Text, @"[^\\]+\.pdf", string.Empty, RegexOptions.IgnoreCase);
            if(string.IsNullOrWhiteSpace(filename) == true)
            {
                MessageBox.Show("Input a valid file name");
                return;
            }

            //check that output folder exists
            if(!Directory.Exists(filepath))
            {
                MessageBox.Show("Input a valid output folder");
                return;
            }

            if (ConvertTask != null && ConvertTask.IsCompleted == false && ConvertTask.IsCompleted == false)
            {
                MessageBox.Show("Conversion is currently in progress.");
                return;
            }

            if(ImageFilesListBox.Items.Count == 0)
            {
                MessageBox.Show("No images selected to convert.");
                return;
            }

            string[] images = new string[ImageFilesListBox.Items.Count];
            for(int i = 0;i < ImageFilesListBox.Items.Count;i++)
            {
                images[i] = ImageFilesListBox.Items[i].ToString();
            }
            string filepath_name = OutputFileNameTextBox.Text;
            string title = OutputTitleTextBox.Text;
            bool openafter = (bool)OpenAfterCheckBox.IsChecked;                
            WidthSetting widthoption = (WidthSetting)WidthSettingComboBox.SelectedIndex;
            double setwidth = 0;
            if (widthoption == WidthSetting.SetWidth)
            {
                try
                {
                    setwidth = Convert.ToDouble(SetWidthTextBox.Text);
                }
                catch(Exception Ex)
                {
                    MessageBox.Show("Failed to parse number of pixels: " + Ex.Message);
                    return;
                }
            }
            ConvertTask = new Task(() => { ConvertImagesToPDF(title, filepath_name, images, openafter, setwidth, widthoption); });
            ConvertTask.Start();
        }

        //Convert the images to a single PDF
        private void ConvertImagesToPDF(string Title,string OutputFileName,string[] ImagesFiles,bool OpenAfter,double SetWidth,WidthSetting WidthOption)
        {
            //Reset progress bar
            this.Dispatcher.Invoke(() => { ConversionProgressBar.Value = 0; });

            // Create a new PDF document
            PdfDocument document = new PdfDocument();
            document.Info.Title = Title;

            //find target width setting if requested
            double TargetWidth = -1;
            if (WidthOption == WidthSetting.ExpandToWidest || WidthOption == WidthSetting.ShrinkToNarrowest)
            {
                foreach (string ImageFile in ImagesFiles)
                {
                    using (XImage image = XImage.FromFile(ImageFile))
                    {
                        if (TargetWidth <= 0)
                        {
                            TargetWidth = image.PixelWidth;
                        }
                        if (image.PixelWidth < TargetWidth && WidthOption == WidthSetting.ShrinkToNarrowest)
                        {
                            TargetWidth = image.PixelWidth;
                        }
                        if (image.PixelWidth > TargetWidth && WidthOption == WidthSetting.ExpandToWidest)
                        {
                            TargetWidth = image.PixelWidth;
                        }
                        
                    }

                    //update progress bar
                    this.Dispatcher.Invoke(() => { ConversionProgressBar.Value = ConversionProgressBar.Value + 0.27 * ConversionProgressBar.Maximum / ImagesFiles.Count(); });
                }
            }
            if (WidthOption == WidthSetting.SetWidth)
            {
                TargetWidth = SetWidth;
            }

            // Add each image to its own page in the PDF 
            int i = 0;
            foreach (string ImageFile in ImagesFiles)
            {
                // Create an empty page
                PdfPage page = document.AddPage();
                string FinalImageFile = ImageFile;

                //change image file size if requested 
                if (WidthOption != WidthSetting.PreserveWidth)
                {
                    Mat OriginImage = CvInvoke.Imread(ImageFile);
                    Mat FinalImage = new Mat();
                    double ScaleFactor = TargetWidth / OriginImage.Width;
                    if (ScaleFactor <= 1.0)
                    {
                        CvInvoke.Resize(OriginImage, FinalImage, new System.Drawing.Size(), ScaleFactor, ScaleFactor, Emgu.CV.CvEnum.Inter.Area);
                    }
                    else
                    {
                        CvInvoke.Resize(OriginImage, FinalImage, new System.Drawing.Size(), ScaleFactor, ScaleFactor, Emgu.CV.CvEnum.Inter.Cubic);
                    }

                    CvInvoke.Imwrite("ScratchFile" + i.ToString() +".jpg", FinalImage);
                    FinalImageFile = "ScratchFile" + i.ToString() + ".jpg";
                    i++;
                } 

                //Open image
                using (XImage image = XImage.FromFile(FinalImageFile))
                {
                    //Set page as same size as image
                    page.Width = image.PointWidth;
                    page.Height = image.PointHeight;
                    //put image on page
                    XGraphics gfx = XGraphics.FromPdfPage(page);
                    gfx.DrawImage(image, 0, 0);
                }

                //update progress bar
                this.Dispatcher.Invoke(() => {
                    if (WidthOption == WidthSetting.ExpandToWidest || WidthOption == WidthSetting.ShrinkToNarrowest)
                    {
                        ConversionProgressBar.Value = ConversionProgressBar.Value + 0.73 * ConversionProgressBar.Maximum / ImagesFiles.Count();
                    }
                    else
                    {
                        ConversionProgressBar.Value = ConversionProgressBar.Value + ConversionProgressBar.Maximum / ImagesFiles.Count();
                    }
                });
            }

            // Save the document...
            document.Save(OutputFileName);

            // Start a viewer if requested
            if (OpenAfter)
            {                
                Process.Start(OutputFileName);
            }

            //delete the scratch files
            if(WidthOption != WidthSetting.PreserveWidth)
            {
                string[] Scratches = Directory.GetFiles(Directory.GetCurrentDirectory(),"ScratchFile*");
                foreach(string scratch in Scratches)
                {
                    File.Delete(scratch);
                }
            }

            //Clear the UI
            this.Dispatcher.Invoke(() => { ImageFilesListBox.Items.Clear(); });
        }

        //Open windows explorer UI to select image files
        private void SelectFilesToConvertButton_Click(object sender, RoutedEventArgs e)
        {
            //Open file explorer for selecting files
            using (CommonOpenFileDialog FolderDialog = new CommonOpenFileDialog())
            {
                FolderDialog.Multiselect = true;
                FolderDialog.ShowHiddenItems = true;
                FolderDialog.Title = "Select Images to Add";
                FolderDialog.Filters.Add(new CommonFileDialogFilter("Images", ".bmp;.png;.gif;.jpg;.tif;.pdf"));
                if (FolderDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    foreach (string filename in FolderDialog.FileNames)
                    {
                        ImageFilesListBox.Items.Add(filename);
                    }
                }
            }
        }

        //Open windows explorer UI to select output location/filename
        private void SelectOutputButton_Click(object sender, RoutedEventArgs e)
        {
            //Open file explorer for selecting the folder
            using (CommonOpenFileDialog FolderDialog = new CommonOpenFileDialog())
            {
                FolderDialog.ShowHiddenItems = true;
                FolderDialog.Title = "Select output location and file name";
                FolderDialog.EnsureValidNames = true;
                FolderDialog.DefaultFileName = "Output.pdf";
                FolderDialog.Filters.Add(new CommonFileDialogFilter("", ".pdf"));
                if (FolderDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    OutputFileNameTextBox.Text = FolderDialog.FileName;
                }
            }
        }

        //Remove selected files
        private void RemoveFileButton_Click(object sender, RoutedEventArgs e)
        {
            string[] FilesToRemove = new string[ImageFilesListBox.SelectedItems.Count];
            ImageFilesListBox.SelectedItems.CopyTo(FilesToRemove,0);
            for (int i = 0;i < FilesToRemove.Count();i++)
            {
                ImageFilesListBox.Items.Remove(FilesToRemove[i]);
            }
        }

        //Save settings
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Properties.Settings.Default.WidthSetting = WidthSettingComboBox.SelectedIndex;
            Properties.Settings.Default.SetWidthSetting = SetWidthTextBox.Text;
            Properties.Settings.Default.OpenAfterSetting = (bool)OpenAfterCheckBox.IsChecked;
            Properties.Settings.Default.Save();
        }
     
        //Move selected item up the list
        private void MoveUpArrowButton_Click(object sender, RoutedEventArgs e)
        {
            int SelectedIndex = ImageFilesListBox.SelectedIndex;
            //check if the item is already at the top
            if (SelectedIndex > 0)
            {
                string FileToInsert = ImageFilesListBox.Items[SelectedIndex].ToString();
                ImageFilesListBox.Items.RemoveAt(SelectedIndex);
                ImageFilesListBox.Items.Insert(SelectedIndex - 1, FileToInsert);
                //put selection back
                ImageFilesListBox.SelectedIndex = SelectedIndex - 1;
            }
        }
        
        //Move selected item down the list
        private void MoveDownArrowButton_Click(object sender, RoutedEventArgs e)
        {
            int SelectedIndex = ImageFilesListBox.SelectedIndex;
            //check if the item is already at the bottom
            if (SelectedIndex != ImageFilesListBox.Items.Count - 1)
            {
                string FileToInsert = ImageFilesListBox.Items[SelectedIndex].ToString();
                ImageFilesListBox.Items.Insert(SelectedIndex + 2, FileToInsert);
                ImageFilesListBox.Items.RemoveAt(SelectedIndex);
                //put selection back
                ImageFilesListBox.SelectedIndex = SelectedIndex + 1;
            }
        }

        //detect if delete key was pressed in selection window
        private void ImageFilesListBox_KeyDown(object sender, KeyEventArgs e)
        {
            //if delete key pressed treat the same as the remove button
            if (e.Key == Key.Delete)
            {
                RemoveFileButton_Click(sender, e);
            }
        }

        //Handle files being dropped into listbox
        private void ImageFilesListBox_Drop(object sender, DragEventArgs e)
        {
            //check if files are being dropped
            if(e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] FileNames = (string[])e.Data.GetData(DataFormats.FileDrop);
                //Add each file to the list
                foreach(string file in FileNames)
                {
                    //check that the file is a supported image type
                    if(Regex.IsMatch(file, @"(\.bmp)|(\.png)|(\.gif)|(\.jpg)|(\.tif)|(\.pdf)", RegexOptions.IgnoreCase) == true)
                    {
                        //add if allowed
                        ImageFilesListBox.Items.Add(file);
                    }
                }
            }
        }

        //hide the set width textbox if set width isn't selected
        private void WidthSettingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(WidthSettingComboBox.SelectedIndex != 3)
            {
                SetWidthTextBox.Visibility = Visibility.Hidden;
            }
            else
            {
                SetWidthTextBox.Visibility = Visibility.Visible;
            }
        }

        enum WidthSetting
        {
            PreserveWidth = 0,
            ShrinkToNarrowest,
            ExpandToWidest,
            SetWidth
        }
    }
}
