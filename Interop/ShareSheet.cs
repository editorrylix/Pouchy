using System.IO;
using Pouchy.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;

namespace Pouchy.Interop
{
    /// <summary>Opens the Windows share sheet (Nearby Share, Mail, Teams, ...) for files or text.</summary>
    internal static class ShareSheet
    {
        public static void Show(IntPtr hwnd, IReadOnlyList<string> paths, string? text, string title)
        {
            var manager = DataTransferManagerInterop.GetForWindow(hwnd);

            TypedEventHandler<DataTransferManager, DataRequestedEventArgs>? handler = null;
            handler = async (sender, args) =>
            {
                sender.DataRequested -= handler;
                var request = args.Request;
                var deferral = request.GetDeferral();
                try
                {
                    request.Data.Properties.Title = title;
                    if (paths.Count > 0)
                    {
                        var items = new List<IStorageItem>();
                        foreach (var path in paths)
                        {
                            items.Add(Directory.Exists(path)
                                ? await StorageFolder.GetFolderFromPathAsync(path)
                                : await StorageFile.GetFileFromPathAsync(path));
                        }
                        request.Data.SetStorageItems(items);
                    }
                    if (!string.IsNullOrEmpty(text))
                    {
                        request.Data.SetText(text);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log("Share failed: " + ex.Message);
                    request.FailWithDisplayText("Pouchy couldn't share these items.");
                }
                finally
                {
                    deferral.Complete();
                }
            };

            manager.DataRequested += handler;
            DataTransferManagerInterop.ShowShareUIForWindow(hwnd);
        }
    }
}
