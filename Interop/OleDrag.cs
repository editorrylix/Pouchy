using System.Runtime.InteropServices;
using System.Windows;
using ComIDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace Pouchy.Interop
{
    /// <summary>
    /// Runs a drag with OLE directly, for data objects WPF can't pass through untouched
    /// (the shell's own file data object, which carries the drag image).
    /// </summary>
    internal static class OleDrag
    {
        [ComImport]
        [Guid("00000121-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDropSource
        {
            [PreserveSig] int QueryContinueDrag([MarshalAs(UnmanagedType.Bool)] bool fEscapePressed, int grfKeyState);
            [PreserveSig] int GiveFeedback(int dwEffect);
        }

        private const int S_OK = 0;
        private const int DRAGDROP_S_DROP = 0x00040100;
        private const int DRAGDROP_S_CANCEL = 0x00040101;
        private const int DRAGDROP_S_USEDEFAULTCURSORS = 0x00040102;
        private const int MK_LBUTTON = 0x0001;
        private const int MK_RBUTTON = 0x0002;

        [DllImport("ole32.dll")]
        private static extern int DoDragDrop(ComIDataObject pDataObj, IDropSource pDropSource, int dwOKEffects, out int pdwEffect);

        private sealed class DropSource : IDropSource
        {
            public int QueryContinueDrag(bool fEscapePressed, int grfKeyState)
            {
                if (fEscapePressed) return DRAGDROP_S_CANCEL;
                // Drop when the left button is released (a right-click during the drag cancels it).
                if ((grfKeyState & MK_RBUTTON) != 0) return DRAGDROP_S_CANCEL;
                return (grfKeyState & MK_LBUTTON) == 0 ? DRAGDROP_S_DROP : S_OK;
            }

            public int GiveFeedback(int dwEffect) => DRAGDROP_S_USEDEFAULTCURSORS;
        }

        /// <summary>Blocks until the drop or cancel, like WPF's DragDrop.DoDragDrop. Call on the UI thread.</summary>
        public static DragDropEffects Run(ComIDataObject data, DragDropEffects allowed)
        {
            int hr = DoDragDrop(data, new DropSource(), (int)allowed, out int effect);
            return hr == DRAGDROP_S_DROP ? (DragDropEffects)effect : DragDropEffects.None;
        }
    }
}
