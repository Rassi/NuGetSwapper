using System;
using System.Collections.ObjectModel;
using EnvDTE;
using EnvDTE80;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Media;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.Win32;
using NuGetSwapper.ViewModels;

namespace NuGetSwapper
{
    /// <summary>
    /// Interaction logic for ToolWindow1Control.
    /// </summary>
    public partial class ToolWindow1Control : UserControl
    {
        private readonly ISwapperService _swapperService;
        public ObservableCollection<SolutionPackagesViewModel> PackagesList { get; set; }
        public ObservableCollection<SolutionSwappedProjectViewModel> SwappedProjectsList { get; set; }
        private CancellationTokenSource _loadPackagesCts;

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolWindow1Control"/> class.
        /// </summary>
        public ToolWindow1Control()
        {
            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            _swapperService = new SwapperService(dte);
            this.InitializeComponent();

            PackagesList = new ObservableCollection<SolutionPackagesViewModel>();
            SwappedProjectsList = new ObservableCollection<SolutionSwappedProjectViewModel>();

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
            SwappedProjectsList.Clear();
        }

        /// <summary>
        /// Handles click on the button by displaying a message box.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
        private void ButtonSwapToProject_Click(object sender, RoutedEventArgs e)
        {
            if (PackagesListTreeView.SelectedItem != null) // TODO: is PackageViewModel selectedPackage
            {
                var selectedPackage = (PackageViewModel)PackagesListTreeView.SelectedItem;
                _ = _swapperService.SwapPackage(selectedPackage.SolutionProjectName, selectedPackage.Package.Name, selectedPackage.Package.Version).Result;
                LoadPackages();
            }
        }

        private void ButtonSwapToPackage_OnClick(object sender, RoutedEventArgs e)
        {
            if (SwappedProjectsListTreeView.SelectedItem is SwappedProjectViewModel selectedProject)
            {
                //var selectedProject = (SwappedProjectViewModel)SwappedProjectsListTreeView.SelectedItem;
                var projectName = selectedProject.SolutionProjectName;
                _ = _swapperService.SwapProject(projectName, selectedProject.SwappedProject.PackageName).Result;

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
            SwappedProjectsList.Clear();

            try
            {
                var packageReferencesBySolutionProject = await _swapperService.GetPackageReferencesBySolutionProject();
                ct.ThrowIfCancellationRequested();

                foreach (var projectEntry in packageReferencesBySolutionProject)
                {
                    var solutionPackagesViewModel = new SolutionPackagesViewModel(projectEntry.Key);
                    foreach (var package in projectEntry.Value)
                    {
                        var packageViewModel = new PackageViewModel(package, projectEntry.Key.Name);
                        solutionPackagesViewModel.Packages.Add(packageViewModel);
                    }

                    PackagesList.Add(solutionPackagesViewModel);
                }

                var projectReferencesBySolutionProject = await _swapperService.GetProjectReferencesBySolutionProject();
                ct.ThrowIfCancellationRequested();

                foreach (var projectEntry in projectReferencesBySolutionProject)
                {
                    var solutionSwappedProjectViewModel = new SolutionSwappedProjectViewModel(projectEntry.Key);
                    foreach (var projectReference in projectEntry.Value)
                    {
                        var swappedProjectViewModel = new SwappedProjectViewModel(projectReference, projectEntry.Key.Name);
                        solutionSwappedProjectViewModel.SwappedProjects.Add(swappedProjectViewModel);
                    }

                    SwappedProjectsList.Add(solutionSwappedProjectViewModel);
                }

                // Asynchronously update icons for all packages
                await UpdatePackageIcons(ct);
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

        private async Task UpdatePackageIcons(CancellationToken ct)
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
