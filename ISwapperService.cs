using System.Collections.Generic;
using System.Threading.Tasks;
using NuGetSwapper.Models;

namespace NuGetSwapper
{
    public interface ISwapperService
    {
        Task<bool> SwapPackage(string solutionProjectName, string packageName, string packageVersion, string packageProjectFilename = null);
        Task<bool> SwapProject(string solutionProjectName, string packageName);
        Task<Dictionary<ProjectInfo, IEnumerable<PackageInfo>>> GetPackageReferencesBySolutionProject();
        Task<Dictionary<ProjectInfo, IEnumerable<ProjectReferenceInfo>>> GetProjectReferencesBySolutionProject();
        string FindPackageProjectFilename(string packageName);
        void SetManualProjectFilePath(string packageName, string projectFilePath);
    }
}
