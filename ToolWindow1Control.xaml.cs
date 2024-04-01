using EnvDTE;
using EnvDTE80;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;
using System.Reflection.Metadata;
using NuGetSwapper;

namespace NuGetSwapper
{
    /// <summary>
    /// Interaction logic for ToolWindow1Control.
    /// </summary>
    public partial class ToolWindow1Control : UserControl
    {
        private readonly ISwapperService _swapperService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolWindow1Control"/> class.
        /// </summary>
        public ToolWindow1Control()
        {
            var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            _swapperService = new SwapperService(dte);
            this.InitializeComponent();

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
            PackagesList.Items.Clear();
            SwappedPackagesList.Items.Clear();
        }

        /// <summary>
        /// Handles click on the button by displaying a message box.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
        private void ButtonSwapToProject_Click(object sender, RoutedEventArgs e)
        {
            //MessageBox.Show(string.Format(System.Globalization.CultureInfo.CurrentUICulture, "Invoked '{0}'", this.ToString()), "ToolWindow1");
            if (PackagesList.SelectedItems.Count >= 0)
            {
                var selectedPackage = PackagesList.SelectedItems[0];
                var packageReferencesByProject = _swapperService.GetPackageReferencesByProject().Result;
                var packagesByProjects = packageReferencesByProject.FirstOrDefault(p => p.Value.Any(pr => $"{pr.Name} - {pr.Version}" == selectedPackage.ToString()));
                var project = packagesByProjects.Key;
                var package = packagesByProjects.Value.First(pr => $"{pr.Name} - {pr.Version}" == selectedPackage.ToString());
                //ThreadHelper.JoinableTaskFactory.Run(() => _swapperService.SwapPackage(project.Name, package.Name, package.Version));

                _ = _swapperService.SwapPackage(project.Name, package.Name, package.Version).Result;
                LoadPackages();
            }
        }

        private void ButtonSwapToPackage_OnClick(object sender, RoutedEventArgs e)
        {
            if (SwappedPackagesList.SelectedItems.Count >= 0)
            {
                var selectedProject = SwappedPackagesList.SelectedItems[0];
                var projectReferencesByProject = _swapperService.GetProjectReferencesByProject().Result;
                var projectsByProjects = projectReferencesByProject.FirstOrDefault(p => p.Value.Any(pr => $"{pr.PackageName} - {pr.Version}" == selectedProject.ToString()));
                var project = projectsByProjects.Key;
                var package = projectsByProjects.Value.First(pr => $"{pr.PackageName} - {pr.Version}" == selectedProject.ToString());
                //ThreadHelper.JoinableTaskFactory.RunAsync(() => _swapperService.SwapPackage(project.Name, package.Name, package.Version));

                _ = _swapperService.SwapProject(project.Name, package.PackageName).Result;

                LoadPackages();
            }
        }

        private void PackagesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }


        private async void LoadPackages()
        {
            PackagesList.Items.Clear();
            SwappedPackagesList.Items.Clear();

            var packageReferencesByProject = await _swapperService.GetPackageReferencesByProject();
            foreach (var project in packageReferencesByProject)
            {
                foreach (var package in project.Value)
                {
                    PackagesList.Items.Add($"{package.Name} - {package.Version}");
                }
            }

            var projectReferencesByProject = await _swapperService.GetProjectReferencesByProject();
            foreach (var project in projectReferencesByProject)
            {
                foreach (var projectReference in project.Value)
                {
                    SwappedPackagesList.Items.Add($"{projectReference.PackageName} - {projectReference.Version}");
                }
            }

        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadPackages();
        }
    }
}