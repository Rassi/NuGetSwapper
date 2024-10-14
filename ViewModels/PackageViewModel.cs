using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.Imaging.Interop;
using NuGetSwapper.Models;

namespace NuGetSwapper.ViewModels
{
    public class PackageViewModel: INotifyPropertyChanged
    {
        public string SolutionProjectName
        {
            get => _solutionProjectName;
            set => SetField(ref _solutionProjectName, value);
        }
        private string _solutionProjectName;

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

        public PackageViewModel(PackageInfo package, string solutionProjectName)
        {
            SolutionProjectName = solutionProjectName;
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