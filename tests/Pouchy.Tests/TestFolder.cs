using System.IO;

namespace Pouchy.Tests
{
    /// <summary>A temporary directory deleted when disposed.</summary>
    internal sealed class TestFolder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PouchyTests", Guid.NewGuid().ToString("N"));

        public TestFolder()
        {
            Directory.CreateDirectory(Path);
        }

        public string File(string name, string content = "hello")
        {
            string path = System.IO.Path.Combine(Path, name);
            System.IO.File.WriteAllText(path, content);
            return path;
        }

        public string Folder(string name)
        {
            string path = System.IO.Path.Combine(Path, name);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
