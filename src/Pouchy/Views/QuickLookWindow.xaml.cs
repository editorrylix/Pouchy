using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Pouchy.Models;
using Pouchy.Services;

namespace Pouchy.Views
{
    public partial class QuickLookWindow : Wpf.Ui.Controls.FluentWindow
    {
        private const int MaxPreviewLines = 50;
        private const int MaxListEntries = 50;
        private const int MaxImageDecodeWidth = 1920;

        public QuickLookWindow(PouchItem item)
        {
            InitializeComponent();
            Title = item.DisplayName;
            LoadPreview(item);
        }

        private void LoadPreview(PouchItem item)
        {
            try
            {
                switch (item.Kind)
                {
                    case PouchItemKind.Stack when item.StackFiles != null:
                        ShowList(item.StackFiles.Select(f => Path.GetFileName(f)));
                        return;

                    case PouchItemKind.Folder when item.FilePath != null && Directory.Exists(item.FilePath):
                        ShowList(Directory.EnumerateFileSystemEntries(item.FilePath).Select(f => Path.GetFileName(f)));
                        return;

                    case PouchItemKind.Text when item.TextContent != null:
                        ShowText(item.TextContent);
                        return;

                    case PouchItemKind.Image when item.ImageContent != null:
                        ShowImage(item.ImageContent);
                        return;

                    case PouchItemKind.File when item.FilePath != null && File.Exists(item.FilePath):
                        if (PreviewSupport.IsImage(item.FilePath))
                        {
                            ShowImage(LoadBitmap(item.FilePath));
                            return;
                        }
                        if (PreviewSupport.IsText(item.FilePath))
                        {
                            ShowText(string.Join(Environment.NewLine, File.ReadLines(item.FilePath).Take(MaxPreviewLines)));
                            return;
                        }
                        if (PreviewSupport.IsVideo(item.FilePath))
                        {
                            PreviewVideo.Source = new Uri(item.FilePath);
                            PreviewVideo.Visibility = Visibility.Visible;
                            return;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Quick Look failed for {item.DisplayName}: {ex.Message}");
            }

            NoPreviewText.Visibility = Visibility.Visible;
        }

        private void ShowList(IEnumerable<string> entries)
        {
            PreviewList.ItemsSource = entries.Take(MaxListEntries).ToList();
            PreviewList.Visibility = Visibility.Visible;
        }

        private void ShowText(string text)
        {
            PreviewText.Text = text;
            PreviewTextScroll.Visibility = Visibility.Visible;
        }

        private void ShowImage(BitmapSource image)
        {
            PreviewImage.Source = image;
            PreviewImage.Visibility = Visibility.Visible;
        }

        private static BitmapImage LoadBitmap(string path)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = MaxImageDecodeWidth;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space || e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            PreviewVideo.Source = null;
        }
    }
}
