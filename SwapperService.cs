using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using EnvDTE100;
using EnvDTE80;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Constants = EnvDTE.Constants;
using Project = Microsoft.Build.Evaluation.Project;
using System.Collections.Concurrent;
using NuGetSwapper.Models;

namespace NuGetSwapper
{
    public class SwapperService : ISwapperService
    {
        private const string SwapperSolutionFolderName = "NuGetSwapperProjects";
        private readonly DTE2 _dte;
        private readonly NuGetSwapperPackage _package;
        private readonly ConcurrentDictionary<string, string> _manualProjectFilePaths = new ConcurrentDictionary<string, string>();
        private readonly Dictionary<string, string> _projectFileCache = new Dictionary<string, string>();
        private DateTime _lastCacheUpdate = DateTime.MinValue;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

        public SwapperService(DTE2 dte)
        {
            _dte = dte;
            
            ThreadHelper.ThrowIfNotOnUIThread();
            var vsShell = (IVsShell)ServiceProvider.GlobalProvider.GetService(typeof(IVsShell));
            if (vsShell.IsPackageLoaded(Guid.Parse(NuGetSwapperPackage.PackageGuidString), out var package) == Microsoft.VisualStudio.VSConstants.S_OK)
            {
                _package = (NuGetSwapperPackage)package;
            }
        }

        public async Task<bool> SwapPackage(string solutionProjectName, string packageName, string packageVersion, string packageProjectFilename = null)
        {
            if (packageProjectFilename == null)
            {
                // First, check if there's a manually specified project file path
                if (_manualProjectFilePaths.TryGetValue(packageName, out var manualPath))
                {
                    packageProjectFilename = manualPath;
                }
                else
                {
                    // If not, use the automatic search
                    packageProjectFilename = FindPackageProjectFilename(packageName);
                }

                if (packageProjectFilename == null)
                {
                    MessageBox.Show($"Couldn't find project file for {packageName}", "Project file not found", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }

            var projectFile = await GetSolutionProjectFile(solutionProjectName);
            if (projectFile == null)
            {
                return false;
            }

            var packageReferences = projectFile.GetItems("PackageReference");
            var package = packageReferences.FirstOrDefault(item => item.EvaluatedInclude.Equals(packageName));
            if (package == null)
            {
                return false;
            }

            package.SetMetadataValue("NuGetSwapperPackageName", packageName);
            package.ItemType = "ProjectReference";
            package.UnevaluatedInclude = packageProjectFilename;
            projectFile.Save();

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var solution = (Solution4)_dte.Solution;

            var nugetProjectsFolder = GetSolutionFolder(SwapperSolutionFolderName);
            if (nugetProjectsFolder == null)
            {
                // Add "NuGetSwapperProjects" solution folder if it doesn't exist
                nugetProjectsFolder = (SolutionFolder)solution.AddSolutionFolder(SwapperSolutionFolderName).Object;
            }
            nugetProjectsFolder.AddFromFile(packageProjectFilename);
            solution.SaveAs(solution.FullName);

            return true;
        }

        private async Task<Project> GetSolutionProjectFile(string solutionProjectName)
        {
            var solutionProject = await GetSolutionProject(solutionProjectName);
            if (solutionProject == null)
            {
                return null;
            }

            var projectFile = GetProjectFile(solutionProject);
            return projectFile;
        }

        public async Task<bool> SwapProject(string solutionProjectName, string packageName)
        {
            //var solution = (Solution4)_dte.Solution;
            //var project = solution.Projects.Cast<EnvDTE.Project>().FirstOrDefault(p => p.Name == solutionProjectName);
            //solution.Remove(project);

            //*** Remove projects
            await RemoveProjectReferenceFromSolution(packageName);

            if (!await SwapProjectToPackage(solutionProjectName, packageName))
            {
                return false;
            }
            //solution.AddFromFile(project.FileName);

            //await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            //var dte = await _dte.GetServiceAsync();
            //var solution4 = (Solution4)_dte.Solution;
            //var project = solution4.Projects.Cast<EnvDTE.Project>().FirstOrDefault(p => p.Name == solutionProjectName);
            //var vsSolution = (IVsSolution4)_dte.Solution;
            //vsSolution.UnloadProject(project., (uint)_VSProjectUnloadStatus.UNLOADSTATUS_UnloadedByUser);
            //solution4.Remove(swapperSolutionFolder);



            return true;
        }

        private async Task<bool> SwapProjectToPackage(string solutionProjectName, string packageName)
        {
            var projectFile = await GetSolutionProjectFile(solutionProjectName);
            if (projectFile == null)
            {
                return false;
            }

            //projectFile.SkipEvaluation = true;

            var nuGetProjectReferences = projectFile.GetItems("ProjectReference").Where(i => i.HasMetadata("NuGetSwapperPackageName")).ToList();
            var nuGetProjectReference = nuGetProjectReferences.FirstOrDefault(item => item.Metadata.Any(metadata => metadata.Name.Equals("NuGetSwapperPackageName") && metadata.EvaluatedValue.Equals(packageName)));
            if (nuGetProjectReference == null)
            {
                return false;
            }

            var packageName2 = nuGetProjectReference.GetMetadataValue("NuGetSwapperPackageName");
            nuGetProjectReference.ItemType = "PackageReference";
            nuGetProjectReference.UnevaluatedInclude = packageName2;
            nuGetProjectReference.RemoveMetadata("NuGetSwapperPackageName");
            projectFile.Save();

            //_dte.ExecuteCommand("File.SaveAll");

            //projectFile.SkipEvaluation = false;
            return true;
        }

        private async Task RemoveProjectReferenceFromSolution(string packageName)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            //var dte = await _dte.GetServiceAsync();
            var solution = (Solution4)_dte.Solution;

            var swapperSolutionFolder = solution.Projects.Cast<EnvDTE.Project>().FirstOrDefault(p => p.Name == SwapperSolutionFolderName);
            var swappedProjectItem = swapperSolutionFolder.ProjectItems.Cast<EnvDTE.ProjectItem>().FirstOrDefault(projectItem => projectItem.Name.Equals(packageName));

            if (swappedProjectItem != null)
            {
                swappedProjectItem.Remove();
            }

            if (swapperSolutionFolder.ProjectItems.Count == 0)
            {
                solution.Remove(swapperSolutionFolder);
            }

            _dte.ExecuteCommand("File.SaveAll"); // solution.SaveAs() results in csproj changed outside of Visual Studio conflict
        }

        public SolutionFolder GetSolutionFolder(string solutionFolderName)
        {
            var solution = (Solution4)_dte.Solution;

            foreach (EnvDTE.Project project in solution.Projects)
            {
                if (project.Kind == Constants.vsProjectKindSolutionItems)
                {
                    var solutionFolder = project.Object as SolutionFolder;
                    if (solutionFolder != null && project.Name.Equals(solutionFolderName))
                    {
                        return solutionFolder;
                    }
                }
            }

            //var item = solution.Projects.GetEnumerator();
            //while (item.MoveNext())
            //{
            //    var project = item.Current as EnvDTE.Project;
            //    if (project == null) { continue; }

            //    if (project.Name.Equals(solutionFolderName))
            //    {
            //        return item.Current as SolutionFolder;
            //    }

            //    //if (project.Name.Equals(packageName))
            //    //{
            //    //    dte.Solution.Remove(project);
            //    //    continue;
            //    //}
            //}

            return null;
        }

        private async Task<ProjectInSolution> GetSolutionProject(string solutionProjectName)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            //var dte = await _dte.GetServiceAsync();

            var solutionFile = SolutionFile.Parse(_dte.Solution.FileName);
            var solutionProject = solutionFile.ProjectsByGuid.FirstOrDefault(pair => pair.Value.ProjectName.Equals(solutionProjectName)
                                                                                     && pair.Value.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat).Value;
            return solutionProject;
        }

        public string FindPackageProjectFilename(string packageName)
        {
            // First, check if there's a manually specified project file path
            if (_manualProjectFilePaths.TryGetValue(packageName, out var manualPath))
            {
                return manualPath;
            }

            var searchPath = _package.OptionProjectSearchRootPath;

            if (!Directory.Exists(searchPath))
            {
                return null;
            }

            // Update cache if it's expired
            if (DateTime.Now - _lastCacheUpdate > _cacheDuration)
            {
                UpdateProjectFileCache(searchPath);
            }

            var packageProjectFilename = $"{packageName}.csproj";

            // Look for the project file in the cache
            if (_projectFileCache.TryGetValue(packageProjectFilename, out var cachedPath))
            {
                return cachedPath;
            }

            return null;
        }

        private void UpdateProjectFileCache(string searchPath)
        {
            _projectFileCache.Clear();
            var allProjectFiles = Directory.GetFiles(searchPath, "*.csproj", SearchOption.AllDirectories);
            
            foreach (var file in allProjectFiles)
            {
                var fileName = Path.GetFileName(file);
                if (!_projectFileCache.ContainsKey(fileName))
                {
                    _projectFileCache[fileName] = file;
                }
            }

            _lastCacheUpdate = DateTime.Now;
        }

        public async Task<Dictionary<ProjectInfo, IEnumerable<PackageInfo>>> GetPackageReferencesBySolutionProject()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var packageReferencesByProject = new Dictionary<ProjectInfo, IEnumerable<PackageInfo>>();
            if (_dte.Solution?.IsOpen != true)
            {
                return packageReferencesByProject;
            }

            var solutionFile = SolutionFile.Parse(_dte.Solution.FileName);
            foreach (var projectInSolution in solutionFile.ProjectsByGuid)
            {
                var project = projectInSolution.Value;
                if (project.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat)
                {
                    var projectFile = GetProjectFile(project);
                    var packageReferences = projectFile.GetItems("PackageReference");
                    var packageInfos = packageReferences.Select(item => 
                    {
                        var packageName = item.EvaluatedInclude;
                        var version = item.GetMetadataValue("Version");
                        return new PackageInfo 
                        { 
                            Name = packageName, 
                            Version = version
                        };
                    }).OrderBy(info => info.Name);
                    var projectInfo = new ProjectInfo { Name = Path.GetFileNameWithoutExtension(projectFile.FullPath), Filename = projectFile.FullPath };
                    packageReferencesByProject.Add(projectInfo, packageInfos);
                }
            }

            return packageReferencesByProject;
        }

        private static Project GetProjectFile(ProjectInSolution project)
        {
            var projectFile = ProjectCollection.GlobalProjectCollection.LoadedProjects.FirstOrDefault(pr => pr.FullPath == project.AbsolutePath);
            if (projectFile == null)
            {
                projectFile = new Project(project.AbsolutePath);
            }

            return projectFile;
        }

        public async Task<Dictionary<ProjectInfo, IEnumerable<ProjectReferenceInfo>>> GetProjectReferencesBySolutionProject()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            
            var projectReferencesByProject = new Dictionary<ProjectInfo, IEnumerable<ProjectReferenceInfo>>();
            if (_dte.Solution?.IsOpen != true)
            {
                return projectReferencesByProject;
            }

            var solutionFile = SolutionFile.Parse(_dte.Solution.FileName);
            foreach (var projectInSolution in solutionFile.ProjectsByGuid)
            {
                var project = projectInSolution.Value;
                if (project.ProjectType != SolutionProjectType.KnownToBeMSBuildFormat)
                {
                    continue;
                }

                var projectFile = GetProjectFile(project);

                var nuGetProjectReferences = projectFile.GetItems("ProjectReference").Where(i => i.HasMetadata("NuGetSwapperPackageName")).ToList();
                if (!nuGetProjectReferences.Any())
                {
                    continue;
                }
                //var nuGetProjectReference = nuGetProjectReferences.FirstOrDefault(item => item.Metadata.Any(metadata => metadata.Name.Equals("NuGetSwapperPackageName")));
                var projectReferenceInfos = nuGetProjectReferences.Select(item => new ProjectReferenceInfo
                {
                    Filename = item.EvaluatedInclude,
                    Version = item.GetMetadataValue("Version"),
                    PackageName = item.GetMetadataValue("NuGetSwapperPackageName")
                }).OrderBy(item => item.Filename); // Order the project references alphabetically;

                var projectInfo = new ProjectInfo { Name = Path.GetFileNameWithoutExtension(projectFile.FullPath), Filename = projectFile.FullPath };
                projectReferencesByProject.Add(projectInfo, projectReferenceInfos);


                //var packageName2 = nuGetProjectReference.GetMetadataValue("NuGetSwapperPackageName");

                //var project = projectInSolution.Value;
                //if (project.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat)
                //{
                //    var projectFile = GetProjectFile(project);
                //    var projectReferences = projectFile.GetItems("ProjectReference");
                //    var projectReferenceInfos = projectReferences.Select(item => new PackageInfo { Name = item.EvaluatedInclude, Version = item.GetMetadataValue("Version") });
                //    var projectReferenceInfo = new ProjectInfo { Name = Path.GetFileNameWithoutExtension(projectFile.FullPath), Filename = projectFile.FullPath };
                //    packageReferencesByProject.Add(projectReferenceInfo, projectReferenceInfos);
                //}
            }

            return projectReferencesByProject;
        }

        public void SetManualProjectFilePath(string packageName, string projectFilePath)
        {
            _manualProjectFilePaths[packageName] = projectFilePath;
        }
    }
}
