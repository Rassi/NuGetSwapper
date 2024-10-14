using System.Collections.ObjectModel;
using NuGetSwapper.Models;

namespace NuGetSwapper.ViewModels
{
    public class SolutionPackagesViewModel
    {
        public ProjectInfo Project { get; set; }
        public ObservableCollection<PackageViewModel> Packages { get; set; }

        public SolutionPackagesViewModel(ProjectInfo project)
        {
            Project = project;
            Packages = new ObservableCollection<PackageViewModel>();
        }
    }
}