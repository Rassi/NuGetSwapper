using System.Collections.ObjectModel;
using NuGetSwapper.Models;

namespace NuGetSwapper.ViewModels
{
    public class SolutionSwappedProjectViewModel
    {
        public ProjectInfo Project { get; set; }
        public ObservableCollection<SwappedProjectViewModel> SwappedProjects { get; set; }

        public SolutionSwappedProjectViewModel(ProjectInfo project)
        {
            Project = project;
            SwappedProjects = new ObservableCollection<SwappedProjectViewModel>();
        }
    }
}