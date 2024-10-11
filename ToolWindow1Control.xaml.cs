using System.Collections.ObjectModel;
using EnvDTE;
using EnvDTE80;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace NuGetSwapper
{
    /// <summary>
    /// Interaction logic for ToolWindow1Control.
    /// </summary>
    public partial class ToolWindow1Control : UserControl
    {
        private readonly ISwapperService _swapperService;
        public ObservableCollection<TreeViewItem> PackagesList { get; set; }
        public ObservableCollection<TreeViewItem> SwappedPackagesList { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolWindow1Control"/> class.
        /// </summary>
        public ToolWindow1Control()
        {
            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            _swapperService = new SwapperService(dte);
            this.InitializeComponent();

            PackagesList = new ObservableCollection<TreeViewItem>();
            SwappedPackagesList = new ObservableCollection<TreeViewItem>();

            // Subscribe to the SolutionEvents
            ThreadHelper.ThrowIfNotOnUIThread();
            dte.Events.SolutionEvents.Opened += SolutionEvents_Opened;
            dte.Events.SolutionEvents.AfterClosing += SolutionEvents_AfterClosing;

            // Call LoadPackages() initially
            LoadPackages();
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
            if (PackagesListTreeView.SelectedItem != null && !((TreeViewItem) PackagesListTreeView.SelectedItem).HasItems)
            {
                var selectedPackage = PackagesListTreeView.SelectedItem;
                var packageReferencesByProject = _swapperService.GetPackageReferencesByProject().Result;
                var selectedPackageName = ((TreeViewItem) selectedPackage).Header.ToString();
                var packagesByProjects = packageReferencesByProject.FirstOrDefault(p => p.Value.Any(pr => $"{pr.Name} - {pr.Version}" == selectedPackageName));
                var project = packagesByProjects.Key;
                var package = packagesByProjects.Value.First(pr => $"{pr.Name} - {pr.Version}" == selectedPackageName);
                //ThreadHelper.JoinableTaskFactory.Run(() => _swapperService.SwapPackage(project.Name, package.Name, package.Version));

                _ = _swapperService.SwapPackage(project.Name, package.Name, package.Version).Result;
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
            PackagesList.Clear();
            SwappedPackagesList.Clear();

            var packageReferencesByProject = await _swapperService.GetPackageReferencesByProject();
            foreach (var project in packageReferencesByProject)
            {
                var projectNode = new TreeViewItem { Header = project.Key.Name };

                foreach (var package in project.Value)
                {
                    var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    
                    // Add a placeholder icon
                    var placeholderIcon = new Microsoft.VisualStudio.Imaging.CrispImage
                    {
                        Moniker = Microsoft.VisualStudio.Imaging.KnownMonikers.Loading,
                        Width = 16,
                        Height = 16,
                        Margin = new Thickness(5, 0, 0, 0)
                    };
                    stackPanel.Children.Add(placeholderIcon);
                    stackPanel.Children.Add(new TextBlock { Text = $"{package.Name} - {package.Version}" });

                    var packageNode = new TreeViewItem { Header = stackPanel };
                    projectNode.Items.Add(packageNode);
                }

                PackagesList.Add(projectNode);
            }

            var projectReferencesByProject = await _swapperService.GetProjectReferencesByProject();
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
            await UpdatePackageIcons(packageReferencesByProject);
        }

        private async Task UpdatePackageIcons(Dictionary<ProjectInfo, IEnumerable<PackageInfo>> packageReferencesByProject)
        {
            foreach (var project in packageReferencesByProject)
            {
                foreach (var package in project.Value)
                {
                    var packageProjectFilename = await Task.Run(() => _swapperService.FindPackageProjectFilename(package.Name));
                    var hasProjectFile = !string.IsNullOrEmpty(packageProjectFilename);

                    var icon = hasProjectFile ? Microsoft.VisualStudio.Imaging.KnownMonikers.StatusOK : Microsoft.VisualStudio.Imaging.KnownMonikers.StatusError;

                    // Find the corresponding TreeViewItem and update its icon
                    var projectNode = PackagesList.FirstOrDefault(p => p.Header.ToString() == project.Key.Name);
                    if (projectNode != null)
                    {
                        var packageNode = projectNode.Items.Cast<TreeViewItem>().FirstOrDefault(i => ((StackPanel)i.Header).Children.OfType<TextBlock>().First().Text == $"{package.Name} - {package.Version}");
                        if (packageNode != null)
                        {
                            var stackPanel = (StackPanel)packageNode.Header;
                            var crispImage = (Microsoft.VisualStudio.Imaging.CrispImage)stackPanel.Children[0];
                            crispImage.Moniker = icon;
                        }
                    }
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadPackages();
        }
    }
}