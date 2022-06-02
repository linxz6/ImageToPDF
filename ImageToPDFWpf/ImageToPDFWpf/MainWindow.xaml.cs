using System;
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
        }

        //Begin conversion process
        private void ConvertImagesButton_Click(object sender, RoutedEventArgs e)
        {
            if(string.IsNullOrWhiteSpace(OutputFileNameTextBox.Text) == true)
            {
                MessageBox.Show("Input a valid output location/filename.");
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
            string filename = OutputFileNameTextBox.Text;
            string title = OutputTitleTextBox.Text;
            bool openafter = (bool)OpenAfterCheckBox.IsChecked;

            ConvertTask = new Task(() => { ConvertImagesToPDF(title, filename, images, openafter); });
            ConvertTask.Start();
        }

        //Convert the images to a single PDF
        private void ConvertImagesToPDF(string Title,string FileName,string[] ImagesFiles,bool OpenAfter)
        {
            //Reset progress bar
            this.Dispatcher.Invoke(() => { ConversionProgressBar.Value = 0; });

            // Create a new PDF document
            PdfDocument document = new PdfDocument();
            document.Info.Title = Title;

            // Add each image to its own page in the PDF 
            foreach (string ImageFile in ImagesFiles)
            {
                // Create an empty page
                PdfPage page = document.AddPage();

                //Open image
                XImage image = XImage.FromFile(ImageFile);

                //Set page as same size as image
                page.Width = image.PointWidth;
                page.Height = image.PointHeight;

                //put image on page
                XGraphics gfx = XGraphics.FromPdfPage(page);
                gfx.DrawImage(image, 0, 0);

                //update progress bar
                this.Dispatcher.Invoke(() => { ConversionProgressBar.Value = ConversionProgressBar.Value + ConversionProgressBar.Maximum/ImagesFiles.Count(); });
            }

            // Save the document...
            document.Save(FileName);

            // Start a viewer if requested
            if (OpenAfter)
            {                
                Process.Start(FileName);
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
    }
}
