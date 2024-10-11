using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.Imaging.Interop;

namespace NuGetSwapper
{
    public class ProjectViewModel
    {
        public ProjectInfo Project { get; set; }
        public ObservableCollection<PackageViewModel> Packages { get; set; }

        public ProjectViewModel(ProjectInfo project)
        {
            Project = project;
            Packages = new ObservableCollection<PackageViewModel>();
        }
    }

    public class PackageViewModel: INotifyPropertyChanged
    {
        public string ProjectName
        {
            get => _projectName;
            set => SetField(ref _projectName, value);
        }
        private string _projectName;

        public PackageInfo Package
        {
            get => _package;
            set => SetField(ref _package, value);
        }
        private PackageInfo _package;

        public ImageMoniker Icon
        {
            get => _icon;
            set => SetField(ref _icon, value);
        }
        private ImageMoniker _icon;

        public PackageViewModel(PackageInfo package, string projectName)
        {
            ProjectName = projectName;
            Package = package;
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