using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using ComIDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace Pouchy.Interop
{
    /// <summary>One entry of a FileGroupDescriptorW: a file (or folder) that has no path on disk yet.</summary>
    public sealed record VirtualFileInfo(string Name, bool IsFolder, long? Size, DateTime? Modified);

    /// <summary>
    /// Reads "virtual files": what Outlook attachments, files inside a zip folder, phones and some
    /// browsers offer instead of real paths. The data object describes them in FileGroupDescriptorW and
    /// hands over each one's bytes on request (FileContents, by index), as a stream, a memory block or,
    /// for Outlook messages, a compound storage.
    /// </summary>
    public static class VirtualFiles
    {
        public const string DescriptorFormat = "FileGroupDescriptorW";
        public const string ContentsFormat = "FileContents";

        private const int DescriptorSize = 592;   // FILEDESCRIPTORW
        private const int FdAttributes = 0x4, FdWriteTime = 0x20, FdFileSize = 0x40;
        private const int FileAttributeDirectory = 0x10;

        private static short FormatId(string name) => (short)System.Windows.DataFormats.GetDataFormat(name).Id;

        public static bool IsPresent(ComIDataObject data)
        {
            var format = NewFormat(DescriptorFormat, -1, TYMED.TYMED_HGLOBAL);
            try
            {
                return data.QueryGetData(ref format) == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The files described by the data object, in order (empty if there are none).</summary>
        public static IReadOnlyList<VirtualFileInfo> Describe(ComIDataObject data)
        {
            var format = NewFormat(DescriptorFormat, -1, TYMED.TYMED_HGLOBAL);
            STGMEDIUM medium;
            try
            {
                data.GetData(ref format, out medium);
            }
            catch (Exception)
            {
                return Array.Empty<VirtualFileInfo>();
            }
            try
            {
                return medium.tymed == TYMED.TYMED_HGLOBAL ? Parse(ReadHGlobal(medium.unionmember, null)) : Array.Empty<VirtualFileInfo>();
            }
            finally
            {
                ReleaseStgMedium(ref medium);
            }
        }

        /// <summary>Parses a FileGroupDescriptorW block (a count, then one FILEDESCRIPTORW per file).</summary>
        public static IReadOnlyList<VirtualFileInfo> Parse(byte[] block)
        {
            if (block.Length < 4) return Array.Empty<VirtualFileInfo>();
            int count = BitConverter.ToInt32(block, 0);
            var files = new List<VirtualFileInfo>();
            for (int i = 0; i < count && 4 + (i + 1) * DescriptorSize <= block.Length; i++)
            {
                int at = 4 + i * DescriptorSize;
                int flags = BitConverter.ToInt32(block, at);
                int attributes = BitConverter.ToInt32(block, at + 36);
                long modifiedTicks = BitConverter.ToInt64(block, at + 56);
                long size = ((long)(uint)BitConverter.ToInt32(block, at + 64) << 32) | (uint)BitConverter.ToInt32(block, at + 68);
                string name = Encoding.Unicode.GetString(block, at + 72, 520);
                int end = name.IndexOf('\0');
                if (end >= 0) name = name[..end];
                if (name.Length == 0) continue;

                DateTime? modified = null;
                if ((flags & FdWriteTime) != 0 && modifiedTicks > 0)
                {
                    try
                    {
                        modified = DateTime.FromFileTime(modifiedTicks);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                    }
                }
                files.Add(new VirtualFileInfo(
                    name,
                    (flags & FdAttributes) != 0 && (attributes & FileAttributeDirectory) != 0,
                    (flags & FdFileSize) != 0 ? size : null,
                    modified));
            }
            return files;
        }

        /// <summary>
        /// Writes every described file into <paramref name="folder"/> (keeping sub-folders, as a zip
        /// folder describes them) and returns the top-level files and folders that were created.
        /// Must run on the thread that received the data object.
        /// </summary>
        public static IReadOnlyList<string> SaveAll(ComIDataObject data, string folder)
        {
            var files = Describe(data);
            var topLevel = new List<string>();
            Directory.CreateDirectory(folder);
            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                string? path = SafePath(folder, file.Name);
                if (path == null)
                {
                    Services.Logger.Log($"Skipped a virtual file with an unsafe name: {file.Name}");
                    continue;
                }

                try
                {
                    if (file.IsFolder)
                    {
                        Directory.CreateDirectory(path);
                    }
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        path = Unique(path);
                        if (!SaveContents(data, i, path, file.Size)) continue;
                        if (file.Modified is DateTime modified) File.SetLastWriteTime(path, modified);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException)
                {
                    Services.Logger.Log($"Could not save {file.Name}: {ex.Message}");
                    continue;
                }

                // "Photos\Beach.jpg" is inside "Photos": only the top-level entry goes in the pouch.
                string first = Path.GetRelativePath(folder, path).Split(Path.DirectorySeparatorChar)[0];
                string top = Path.Combine(folder, first);
                if (!topLevel.Contains(top, StringComparer.OrdinalIgnoreCase)) topLevel.Add(top);
            }
            return topLevel;
        }

        private static bool SaveContents(ComIDataObject data, int index, string path, long? size)
        {
            var format = NewFormat(ContentsFormat, index, TYMED.TYMED_ISTREAM | TYMED.TYMED_HGLOBAL | TYMED.TYMED_ISTORAGE);
            data.GetData(ref format, out var medium);
            try
            {
                switch (medium.tymed)
                {
                    case TYMED.TYMED_ISTREAM:
                        var stream = (IStream)Marshal.GetObjectForIUnknown(medium.unionmember);
                        try
                        {
                            using var output = File.Create(path);
                            CopyStream(stream, output);
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(stream);
                        }
                        return true;

                    case TYMED.TYMED_HGLOBAL:
                        File.WriteAllBytes(path, ReadHGlobal(medium.unionmember, size));
                        return true;

                    case TYMED.TYMED_ISTORAGE:
                        var storage = (IStorage)Marshal.GetObjectForIUnknown(medium.unionmember);
                        try
                        {
                            SaveStorage(storage, path);
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(storage);
                        }
                        return true;

                    default:
                        Services.Logger.Log($"Virtual file {index} came as {medium.tymed}, which isn't supported.");
                        return false;
                }
            }
            finally
            {
                ReleaseStgMedium(ref medium);
            }
        }

        private static void CopyStream(IStream source, Stream target)
        {
            var buffer = new byte[81920];
            IntPtr readPtr = Marshal.AllocCoTaskMem(sizeof(int));
            try
            {
                while (true)
                {
                    source.Read(buffer, buffer.Length, readPtr);
                    int read = Marshal.ReadInt32(readPtr);
                    if (read <= 0) break;
                    target.Write(buffer, 0, read);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(readPtr);
            }
        }

        private static byte[] ReadHGlobal(IntPtr handle, long? size)
        {
            IntPtr pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero) return Array.Empty<byte>();
            try
            {
                long length = (long)GlobalSize(handle);
                if (size is long known && known >= 0 && known < length) length = known;
                var bytes = new byte[length];
                Marshal.Copy(pointer, bytes, 0, (int)length);
                return bytes;
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }

        /// <summary>An Outlook message arrives as a compound storage: copy it into a .msg file.</summary>
        private static void SaveStorage(IStorage source, string path)
        {
            const int StgmCreate = 0x1000, StgmReadWrite = 0x2, StgmShareExclusive = 0x10;
            int hr = StgCreateDocfile(path, StgmCreate | StgmReadWrite | StgmShareExclusive, 0, out var target);
            if (hr != 0) Marshal.ThrowExceptionForHR(hr);
            try
            {
                source.CopyTo(0, null, IntPtr.Zero, target);
                target.Commit(0);
            }
            finally
            {
                Marshal.ReleaseComObject(target);
            }
        }

        /// <summary>The path for a described name inside the folder, or null if it would escape it.</summary>
        internal static string? SafePath(string folder, string name)
        {
            var parts = name.Replace('/', '\\').Split('\\', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => string.Concat(p.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim().TrimEnd('.'))
                .ToList();
            if (parts.Count == 0 || parts.Any(p => p.Length == 0 || p == "..")) return null;
            string full = Path.GetFullPath(Path.Combine(new[] { folder }.Concat(parts).ToArray()));
            string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full : null;
        }

        private static string Unique(string path)
        {
            if (!File.Exists(path)) return path;
            string dir = Path.GetDirectoryName(path)!, name = Path.GetFileNameWithoutExtension(path), ext = Path.GetExtension(path);
            for (int n = 2; ; n++)
            {
                string candidate = Path.Combine(dir, $"{name} ({n}){ext}");
                if (!File.Exists(candidate)) return candidate;
            }
        }

        private static FORMATETC NewFormat(string name, int index, TYMED tymed) => new()
        {
            cfFormat = FormatId(name),
            dwAspect = DVASPECT.DVASPECT_CONTENT,
            lindex = index,
            ptd = IntPtr.Zero,
            tymed = tymed,
        };

        [DllImport("ole32.dll")]
        private static extern void ReleaseStgMedium(ref STGMEDIUM medium);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalLock(IntPtr handle);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr handle);

        [DllImport("kernel32.dll")]
        private static extern UIntPtr GlobalSize(IntPtr handle);

        [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
        private static extern int StgCreateDocfile(string name, int mode, int reserved, out IStorage storage);

        /// <summary>The parts of IStorage used here; the others keep their slots in the vtable.</summary>
        [ComImport, Guid("0000000B-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IStorage
        {
            void CreateStream(); void OpenStream(); void CreateStorage(); void OpenStorage();
            void CopyTo(int ciidExclude, Guid[]? rgiidExclude, IntPtr snbExclude, IStorage pstgDest);
            void MoveElementTo();
            void Commit(int grfCommitFlags);
            void Revert(); void EnumElements(); void DestroyElement(); void RenameElement();
            void SetElementTimes(); void SetClass(); void SetStateBits(); void Stat();
        }
    }
}
