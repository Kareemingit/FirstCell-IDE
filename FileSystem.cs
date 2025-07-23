using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;

namespace FirstCell
{
    public interface IFileManager
    {
        Task LoadAsync(string path);
        Task SaveAsync();
        Task CreateAsync();
        void Add(File file);
    }
    public static class FileAccessLock
    {
        private static readonly Dictionary<string, SemaphoreSlim> _locks = new();

        public static SemaphoreSlim GetLock(string path)
        {
            lock (_locks)
            {
                if (!_locks.ContainsKey(path))
                    _locks[path] = new SemaphoreSlim(1, 1);
                return _locks[path];
            }
        }
    }


    public abstract class File
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Extension { get; set; }
        public TabItem? CodeBlock { get; set; } // Connected to UI

        protected File(string path)
        {
            Path = path;
            Name = System.IO.Path.GetFileName(path);
            Extension = System.IO.Path.GetExtension(path);
        }

        public virtual async Task LoadAsync()
        {
            if (!System.IO.File.Exists(Path)) return;
            string content = await System.IO.File.ReadAllTextAsync(Path);
            // Set RichTextBox content later via orchestrator/UI
        }

        public virtual async Task SaveAsync()
        {
            //var textBox = CodeBlock?.Content as RichTextBox;
            //if (textBox != null)
            //{
            //    var range = new TextRange(textBox.Document.ContentStart, textBox.Document.ContentEnd);
            //    await System.IO.File.WriteAllTextAsync(Path, range.Text);
            //}
            var fileLock = FileAccessLock.GetLock(Path);
            await fileLock.WaitAsync();
            try
            {
                var textBox = CodeBlock?.Content as RichTextBox;
                if (textBox != null)
                {
                    var range = new TextRange(textBox.Document.ContentStart, textBox.Document.ContentEnd);
                    await System.IO.File.WriteAllTextAsync(Path, range.Text);
                }
            }
            finally
            {
                fileLock.Release();
            }
        }

        public virtual Task CreateAsync()
        {
            System.IO.File.WriteAllText(Path, "");
            return Task.CompletedTask;
        }

        public virtual void Add() { } //no need on base
    }


    public class Project : IFileManager
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public bool IsLoaded { get; private set; }
        public List<File> Files { get; private set; } = new();

        public async Task LoadAsync(string path)
        {
            this.Path = path;
            this.Name = System.IO.Path.GetFileName(path);
            IsLoaded = true;

            // Optional: Load existing files from folder
            foreach (var file in Directory.GetFiles(path))
            {
                string ext = System.IO.Path.GetExtension(file);
                File? loaded = ext switch
                {
                    ".html" => new HTMLFile(file),
                    ".css" => new CSSFile(file),
                    ".js" => new JSFile(file),
                    _ => null
                };
                if (loaded != null)
                {
                    await loaded.LoadAsync();
                    Files.Add(loaded);
                }
            }
        }

        public Task SaveAsync() => Task.WhenAll(Files.Select(f => f.SaveAsync()));

        public Task CreateAsync() => Task.CompletedTask;

        public void Add(File file) => Files.Add(file);
    }

    public class HTMLFile : File
    {
        public HTMLFile(string path) : base(path) { }
    }

    public class CSSFile : File
    {
        public CSSFile(string path) : base(path) { }
    }

    public class JSFile : File
    {
        public JSFile(string path) : base(path) { }
    }

}