using System;
using System.Collections.ObjectModel;
using EnvDTE;
using EnvDTE80;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.Win32;
using Microsoft.Build.Evaluation;

namespace NuGetSwapper
{
    /// <summary>
    /// Interaction logic for ToolWindow1Control.
    /// </summary>
    public partial class ToolWindow1Control : UserControl
    {
        private readonly ISwapperService _swapperService;
        public ObservableCollection<ProjectViewModel> PackagesList { get; set; }
        public ObservableCollection<TreeViewItem> SwappedPackagesList { get; set; }
        private CancellationTokenSource _loadPackagesCts;

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolWindow1Control"/> class.
        /// </summary>
        public ToolWindow1Control()
        {
            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            _swapperService = new SwapperService(dte);
            this.InitializeComponent();

            PackagesList = new ObservableCollection<ProjectViewModel>();
            SwappedPackagesList = new ObservableCollection<TreeViewItem>();

            this.DataContext = this;

            // Subscribe to the SolutionEvents
            ThreadHelper.ThrowIfNotOnUIThread();
            dte.Events.SolutionEvents.Opened += SolutionEvents_Opened;
            dte.Events.SolutionEvents.AfterClosing += SolutionEvents_AfterClosing;

            // Call LoadPackages() initially
            LoadPackages();

            // Add this line to set up the context menu
            SetupContextMenu();
        }

        private void SolutionEvents_Opened()
        {
            // Solution is opened, call LoadPackages()
            LoadPackages();
        }

        private void SolutionEvents_AfterClosing()
        {
            // Solution is closed, clear the PackagesList and SwappedPackagesList
            PackagesList.Clear();
            SwappedPackagesList.Clear();
        }

        /// <summary>
        /// Handles click on the button by displaying a message box.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
        private void ButtonSwapToProject_Click(object sender, RoutedEventArgs e)
        {
            //MessageBox.Show(string.Format(System.Globalization.CultureInfo.CurrentUICulture, "Invoked '{0}'", this.ToString()), "ToolWindow1");
            if (PackagesListTreeView.SelectedItem != null)
            {
                var selectedPackage = (PackageViewModel)PackagesListTreeView.SelectedItem;
                //var packageReferencesByProject = _swapperService.GetPackageReferencesByProject().Result;
                //var selectedPackageName = selectedPackage.Package.Name;
                //var packagesByProjects = packageReferencesByProject.FirstOrDefault(p => p.Value.Any(pr => $"{pr.Name} - {pr.Version}" == selectedPackageName));
                //var project = packagesByProjects.Key;
                //var package = packagesByProjects.Value.First(pr => $"{pr.Name} - {pr.Version}" == selectedPackageName);
                //ThreadHelper.JoinableTaskFactory.Run(() => _swapperService.SwapPackage(project.Name, package.Name, package.Version));

                //_ = _swapperService.SwapPackage(project.Name, package.Name, package.Version).Result;
                _ = _swapperService.SwapPackage(selectedPackage.ProjectName, selectedPackage.Package.Name, selectedPackage.Package.Version).Result;
                LoadPackages();
            }
        }

        private void ButtonSwapToPackage_OnClick(object sender, RoutedEventArgs e)
        {
            if (SwappedPackagesListTreeView.SelectedItem != null && !((TreeViewItem)SwappedPackagesListTreeView.SelectedItem).HasItems)
            {
                var selectedProject = SwappedPackagesListTreeView.SelectedItem;
                var projectReferencesByProject = _swapperService.GetProjectReferencesByProject().Result;
                var selectedProjectName = ((TreeViewItem)selectedProject).Header.ToString();
                var projectsByProjects = projectReferencesByProject.FirstOrDefault(p => p.Value.Any(pr => $"{pr.PackageName} - {pr.Version}" == selectedProjectName));
                var project = projectsByProjects.Key;
                var package = projectsByProjects.Value.First(pr => $"{pr.PackageName} - {pr.Version}" == selectedProjectName);
                //ThreadHelper.JoinableTaskFactory.RunAsync(() => _swapperService.SwapPackage(project.Name, package.Name, package.Version));

                _ = _swapperService.SwapProject(project.Name, package.PackageName).Result;

                LoadPackages();
            }
        }

        private async void LoadPackages()
        {
            // Cancel any ongoing LoadPackages operation
            _loadPackagesCts?.Cancel();
            _loadPackagesCts = new CancellationTokenSource();
            var ct = _loadPackagesCts.Token;

            PackagesList.Clear();
            SwappedPackagesList.Clear();

            try
            {
                var packageReferencesByProject = await _swapperService.GetPackageReferencesByProject();
                ct.ThrowIfCancellationRequested();

                foreach (var projectEntry in packageReferencesByProject)
                {
                    var projectViewModel = new ProjectViewModel(projectEntry.Key);

                    foreach (var package in projectEntry.Value)
                    {
                        var packageViewModel = new PackageViewModel(package, projectEntry.Key.Name);
                        projectViewModel.Packages.Add(packageViewModel);
                    }

                    PackagesList.Add(projectViewModel);
                }

                var projectReferencesByProject = await _swapperService.GetProjectReferencesByProject();
                ct.ThrowIfCancellationRequested();

                foreach (var project in projectReferencesByProject)
                {
                    var projectNode = new TreeViewItem { Header = project.Key.Name };

                    foreach (var projectReference in project.Value)
                    {
                        var projectReferenceNode = new TreeViewItem { Header = $"{projectReference.PackageName} - {projectReference.Version}" };
                        projectNode.Items.Add(projectReferenceNode);
                    }

                    SwappedPackagesList.Add(projectNode);
                }

                // Asynchronously update icons for all packages
                await UpdatePackageIcons(packageReferencesByProject, ct);
            }
            catch (OperationCanceledException)
            {
                // Operation was cancelled, do nothing
            }
            catch (Exception ex)
            {
                // Handle any other exceptions that might occur during loading
                MessageBox.Show($"An error occurred while loading packages: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task UpdatePackageIcons(Dictionary<ProjectInfo, IEnumerable<PackageInfo>> packageReferencesByProject, CancellationToken ct)
        {
            foreach (var projectViewModel in PackagesList)
            {
                foreach (var packageViewModel in projectViewModel.Packages)
                {
                    ct.ThrowIfCancellationRequested();

                    var packageProjectFilename = await Task.Run(() => _swapperService.FindPackageProjectFilename(packageViewModel.Package.Name), ct);
                    var hasProjectFile = !string.IsNullOrEmpty(packageProjectFilename);

                    packageViewModel.Icon = hasProjectFile ? Microsoft.VisualStudio.Imaging.KnownMonikers.StatusOK : Microsoft.VisualStudio.Imaging.KnownMonikers.StatusError;
                    
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadPackages();
        }

        private void SetupContextMenu()
        {
            var contextMenu = new ContextMenu();
            var menuItem = new MenuItem { Header = "Specify Project File" };
            menuItem.Click += SpecifyProjectFile_Click;
            contextMenu.Items.Add(menuItem);

            PackagesListTreeView.MouseRightButtonDown += (sender, args) =>
            {
                var treeViewItem = VisualUpwardSearch<TreeViewItem>(args.OriginalSource as DependencyObject) as TreeViewItem;
                if (treeViewItem != null && !treeViewItem.HasItems)
                {
                    treeViewItem.ContextMenu = contextMenu;
                }
            };
        }

        private void SpecifyProjectFile_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = PackagesListTreeView.SelectedItem as TreeViewItem;
            if (selectedItem != null && !selectedItem.HasItems)
            {
                var packageName = ((StackPanel)selectedItem.Header).Children.OfType<TextBlock>().First().Text.Split('-')[0].Trim();
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "C# Project Files (*.csproj)|*.csproj",
                    Title = $"Select Project File for {packageName}"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var projectFilePath = openFileDialog.FileName;
                    // Update the package icon to indicate a manually specified project file
                    UpdatePackageIcon(selectedItem, Microsoft.VisualStudio.Imaging.KnownMonikers.DocumentSource);
                    // Store the manually specified project file path
                    _swapperService.SetManualProjectFilePath(packageName, projectFilePath);
                }
            }
        }

        private static DependencyObject VisualUpwardSearch<T>(DependencyObject source)
        {
            while (source != null && source.GetType() != typeof(T))
                source = VisualTreeHelper.GetParent(source);

            return source;
        }

        private void UpdatePackageIcon(TreeViewItem packageNode, ImageMoniker icon)
        {
            var stackPanel = (StackPanel)packageNode.Header;
            var crispImage = (Microsoft.VisualStudio.Imaging.CrispImage)stackPanel.Children[0];
            crispImage.Moniker = icon;
        }
    }
}