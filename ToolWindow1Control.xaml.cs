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

            LoadPackages();
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
        private async void ButtonSwapToProject_Click(object sender, RoutedEventArgs e)
        {
            if (PackagesListTreeView.SelectedItem is PackageViewModel selectedPackage)
            {
                await _swapperService.SwapPackage(selectedPackage.SolutionProjectName, selectedPackage.Package.Name, selectedPackage.Package.Version);
                
                LoadPackages();
            }
        }

        private async void ButtonSwapToPackage_OnClick(object sender, RoutedEventArgs e)
        {
            if (SwappedProjectsListTreeView.SelectedItem is SwappedProjectViewModel selectedProject)
            {
                await _swapperService.SwapProject(selectedProject.SolutionProjectName, selectedProject.SwappedProject.PackageName);

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
            if (PackagesListTreeView.SelectedItem is PackageViewModel selectedPackage)
            {
                var packageName = selectedPackage.Package.Name;
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "C# Project Files (*.csproj)|*.csproj",
                    Title = $"Select Project File for {packageName}"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var projectFilePath = openFileDialog.FileName;
                    selectedPackage.Icon = Microsoft.VisualStudio.Imaging.KnownMonikers.DocumentSource;
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

        private void PackagesListTreeView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (PackagesListTreeView.SelectedItem is PackageViewModel selectedPackage)
            {
                ButtonSwapToProject_Click(sender, new RoutedEventArgs());
            }
        }

        private void SwappedProjectsListTreeView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (SwappedProjectsListTreeView.SelectedItem is SwappedProjectViewModel selectedProject)
            {
                ButtonSwapToPackage_OnClick(sender, new RoutedEventArgs());
            }
        }
    }
}
