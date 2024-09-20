using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetSwapper
{
    public interface ISwapperService
    {
        Task<bool> SwapPackage(string solutionProjectName, string packageName, string packageVersion, string packageProjectFilename = null);
        Task<bool> SwapProject(string solutionProjectName, string packageName);
        Task<Dictionary<ProjectInfo, IEnumerable<PackageInfo>>> GetPackageReferencesByProject();
        Task<Dictionary<ProjectInfo, IEnumerable<ProjectReferenceInfo>>> GetProjectReferencesByProject();
        string FindPackageProjectFilename(string packageName);
    }
}
