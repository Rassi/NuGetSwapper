using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.Imaging.Interop;
using NuGetSwapper.Models;

namespace NuGetSwapper.ViewModels
{
    public class SwappedProjectViewModel: INotifyPropertyChanged
    {
        public string SolutionProjectName
        {
            get => _solutionProjectName;
            set => SetField(ref _solutionProjectName, value);
        }
        private string _solutionProjectName;

        public ProjectReferenceInfo SwappedProject
        {
            get => _swappedProject;
            set => SetField(ref _swappedProject, value);
        }
        private ProjectReferenceInfo _swappedProject;

        public ImageMoniker Icon
        {
            get => _icon;
            set => SetField(ref _icon, value);
        }
        private ImageMoniker _icon;

        public SwappedProjectViewModel(ProjectReferenceInfo swappedProject, string solutionProjectName)
        {
            SolutionProjectName = solutionProjectName;
            SwappedProject = swappedProject;
            Icon = Microsoft.VisualStudio.Imaging.KnownMonikers.Loading;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}