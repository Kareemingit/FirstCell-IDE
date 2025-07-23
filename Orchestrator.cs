using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.IO;

namespace FirstCell
{
    public class Orchestrator
    {
        private readonly TabControl _tabControl;
        private readonly SyntaxHighlighter _highlighter;
        private readonly AutoCompleter _autoCompleter;
        private LiveReloadServer? _liveServer;
        public List<Project> Projects { get; private set; } = new();
        public Project? CurrentProject => Projects.LastOrDefault();
        public bool isAutoSaveEnable;
        private FileWatcher _fileWatcher;
        public Orchestrator(TabControl tabControl)
        {
            _tabControl = tabControl;
            _highlighter = new SyntaxHighlighter();
            _autoCompleter = new AutoCompleter();
            isAutoSaveEnable = false;
        }

        public async Task LoadProjectAsync(string path)
        {
            var newProject = new Project();
            await newProject.LoadAsync(path);
            Projects.Add(newProject);

            foreach (var file in newProject.Files)
                await OpenFileAsync(file, newProject);
        }

        public async Task StartLiveServerAsync(string projectPath, string fileName)
        {
            _liveServer?.Stop();
            _liveServer = new LiveReloadServer(projectPath);
            await _liveServer.StartAsync();
            string fileContent = System.IO.File.ReadAllText(projectPath + "\\" + fileName);
            string newCode = LiveReloadInjector.Inject(fileContent);
            System.IO.File.WriteAllText(projectPath + "\\" + fileName, newCode);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "http://localhost:8080/" + fileName,
                UseShellExecute = true
            });
            _fileWatcher = new FileWatcher(projectPath, _liveServer);
        }

        public async Task OpenFileAsync(File file, Project project)
        {
            var textBox = new RichTextBox
            {
                Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#333")),
                Foreground = Brushes.White,
                Margin = new Thickness(2),
                BorderThickness = new Thickness(2),
                AcceptsReturn = true,
                AcceptsTab = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Tag = file,
                FontSize = 16
            };
            textBox.Document.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            textBox.Document.LineHeight = 14;
            textBox.PreviewTextInput += (s, e) => _autoCompleter.HandleInput(textBox, e.Text);
            textBox.TextChanged += _highlighter.Editor_TextChanged;
            textBox.TextChanged += AutoSave;
            var tab = new TabItem
            {
                Header = $"[{project.Name}] {file.Name}",
                Content = textBox,
                Tag = file
            };

            file.CodeBlock = tab;
            _tabControl.Items.Add(tab);
            _tabControl.SelectedItem = tab;

            if (System.IO.File.Exists(file.Path))
            {
                string content = await System.IO.File.ReadAllTextAsync(file.Path);
                textBox.Document.Blocks.Clear();
                textBox.Document.Blocks.Add(new Paragraph(new Run(content)));
            }

        }
        private async void AutoSave(object sender, TextChangedEventArgs e)
        {
            if (isAutoSaveEnable && sender is RichTextBox textBox)
            {
                if (textBox.Tag is File file)
                    await file.SaveAsync();
            }
        }
        public async Task ShutdownAsync()
        {
            if (CurrentProject != null)
            {
                foreach (var file in CurrentProject.Files.Where(f => f.Extension == ".html"))
                    LiveReloadInjector.RemoveFromFile(file.Path);
                await CurrentProject.SaveAsync();
            }
            _liveServer?.Stop();

        }
    }
}
