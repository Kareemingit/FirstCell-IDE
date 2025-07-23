using FirstCell.Input_Dialog;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FirstCell
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private Orchestrator _orchestrator;

        public MainWindow()
        {
            InitializeComponent();
            _orchestrator = new Orchestrator(MainTabControl);
            this.Closing += async (s, e) => await _orchestrator.ShutdownAsync();
        }
        private bool _isAutoSaveEnabled;
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        public bool IsAutoSaveEnabled
        {
            get => _isAutoSaveEnabled;
            set
            {
                if (_isAutoSaveEnabled != value)
                {
                    _isAutoSaveEnabled = value;
                    OnPropertyChanged(nameof(IsAutoSaveEnabled));
                    // Trigger any behavior here (e.g., timer or flag)
                }
            }
        }
        private void AutoSaveToggle_Click(object sender, RoutedEventArgs e)
        {
            IsAutoSaveEnabled = !IsAutoSaveEnabled;
            if (IsAutoSaveEnabled)
                _orchestrator.isAutoSaveEnable = true;
            else
                _orchestrator.isAutoSaveEnable = false;
        }
        private async void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                await _orchestrator.LoadProjectAsync(dialog.FileName);
                LoadProjectsToTreeView();
            }
        }

        private void LoadProjectsToTreeView()
        {
            ProjectList.Items.Clear();

            foreach (var project in _orchestrator.Projects)
            {
                var projectNode = new TreeViewItem
                {
                    Header = project.Name,
                    Tag = project,
                    Foreground = Brushes.White
                };
                // Attach Context Menu
                var contextMenu = new ContextMenu();
                var createFileItem = new MenuItem { Header = "Create File" };
                createFileItem.Click += (s, e) => CreateFile_Click_ForProject(project);
                contextMenu.Items.Add(createFileItem);

                projectNode.ContextMenu = contextMenu;
                foreach (var file in project.Files)
                {
                    var fileNode = new TreeViewItem
                    {
                        Header = file.Name,
                        Tag = file,
                        Foreground = Brushes.White
                    };
                    if(file.Extension == ".html")
                    {
                        var OWLSContext = new ContextMenu();
                        var OWLSItwm = new MenuItem { Header = "Open With Live Server" };
                        OWLSItwm.Click += (s, e) => OpenWithLiveServer_Click(file.Name , project.Path);
                        OWLSContext.Items.Add(OWLSItwm);
                        fileNode.ContextMenu = OWLSContext;
                    }
                    projectNode.Items.Add(fileNode);
                }

                ProjectList.Items.Add(projectNode);
            }
        }
        private async void OpenWithLiveServer_Click(string fileName , string projectPath)
        {
            await _orchestrator.StartLiveServerAsync(projectPath, fileName);
        }
        private async void ProjectList_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (ProjectList.SelectedItem is TreeViewItem item && item.Tag is File file)
            {
                var project = _orchestrator.Projects.FirstOrDefault(p => p.Files.Contains(file));
                if (project != null)
                    await _orchestrator.OpenFileAsync(file, project);
            }
        }

        private async void SaveAll_Click(object sender, RoutedEventArgs e)
        {
            if (_orchestrator.CurrentProject != null)
                await _orchestrator.CurrentProject.SaveAsync();
        }

        private async void ProjectList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProjectList.SelectedItem is Project project)
            {
                foreach (var file in project.Files)
                {
                    await _orchestrator.OpenFileAsync(file, project);
                }
            }
        }

        private async void CreateFile_Click_ForProject(Project project)
        {
            if (_orchestrator.CurrentProject == null) return;

            var inputDialog = new InputDialog("Enter file name with extension:");
            if (inputDialog.ShowDialog() == true)
            {
                string fileName = inputDialog.Answer;
                string fullPath = System.IO.Path.Combine(_orchestrator.CurrentProject.Path, fileName);
                File newFile = System.IO.Path.GetExtension(fileName) switch
                {
                    ".html" => new HTMLFile(fullPath),
                    ".css" => new CSSFile(fullPath),
                    ".js" => new JSFile(fullPath),
                    _ => new HTMLFile(fullPath)
                };

                await newFile.CreateAsync();
                _orchestrator.CurrentProject.Add(newFile);
                await _orchestrator.OpenFileAsync(newFile, _orchestrator.CurrentProject);
                LoadProjectsToTreeView();
            }
        }

    }
}