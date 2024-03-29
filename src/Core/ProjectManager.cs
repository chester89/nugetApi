using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Xml.Linq;
using NuGet.Resources;

namespace NuGet
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
    public class ProjectManager : IProjectManager
    {
        public event EventHandler<PackageOperationEventArgs> PackageReferenceAdding;
        public event EventHandler<PackageOperationEventArgs> PackageReferenceAdded;
        public event EventHandler<PackageOperationEventArgs> PackageReferenceRemoving;
        public event EventHandler<PackageOperationEventArgs> PackageReferenceRemoved;

        private ILogger _logger;
        private IPackageConstraintProvider _constraintProvider;
        private readonly IPackageReferenceRepository _packageReferenceRepository;

        private static readonly IDictionary<string, IPackageFileTransformer> _fileTransformers = new Dictionary<string, IPackageFileTransformer>(StringComparer.OrdinalIgnoreCase) {
            { ".transform", new XmlTransfomer(GetConfigMappings()) },
            { ".pp", new Preprocessor() }
        };

        public ProjectManager(IPackageRepository sourceRepository, IPackagePathResolver pathResolver, IProjectSystem project, IPackageRepository localRepository)
        {
            if (sourceRepository == null)
            {
                throw new ArgumentNullException("sourceRepository");
            }
            if (pathResolver == null)
            {
                throw new ArgumentNullException("pathResolver");
            }
            if (project == null)
            {
                throw new ArgumentNullException("project");
            }
            if (localRepository == null)
            {
                throw new ArgumentNullException("localRepository");
            }

            SourceRepository = sourceRepository;
            Project = project;
            PathResolver = pathResolver;
            LocalRepository = localRepository;
            _packageReferenceRepository = LocalRepository as IPackageReferenceRepository;
        }

        public IPackagePathResolver PathResolver
        {
            get;
            private set;
        }

        public IPackageRepository LocalRepository
        {
            get;
            private set;
        }

        public IPackageRepository SourceRepository
        {
            get;
            private set;
        }

        public IPackageConstraintProvider ConstraintProvider
        {
            get
            {
                return _constraintProvider ?? NullConstraintProvider.Instance;
            }
            set
            {
                _constraintProvider = value;
            }
        }

        public IProjectSystem Project
        {
            get;
            private set;
        }

        public ILogger Logger
        {
            get
            {
                return _logger ?? NullLogger.Instance;
            }
            set
            {
                _logger = value;
            }
        }

        public virtual void AddPackageReference(string packageId)
        {
            AddPackageReference(packageId, version: null, ignoreDependencies: false, allowPrereleaseVersions: false);
        }

        public virtual void AddPackageReference(string packageId, SemanticVersion version)
        {
            AddPackageReference(packageId, version: version, ignoreDependencies: false, allowPrereleaseVersions: false);
        }

        public virtual void AddPackageReference(string packageId, SemanticVersion version, bool ignoreDependencies, bool allowPrereleaseVersions)
        {
            IPackage package = PackageHelper.ResolvePackage(SourceRepository, LocalRepository, NullConstraintProvider.Instance, packageId, version, allowPrereleaseVersions);
            AddPackageReference(package, ignoreDependencies, allowPrereleaseVersions);
        }

        public virtual void AddPackageReference(IPackage package, bool ignoreDependencies, bool allowPrereleaseVersions)
        {
            Execute(package, new UpdateWalker(LocalRepository,
                                              SourceRepository,
                                              new DependentsWalker(LocalRepository, GetPackageTargetFramework(package.Id)),
                                              ConstraintProvider,
                                              Project.TargetFramework,
                                              NullLogger.Instance,
                                              !ignoreDependencies,
                                              allowPrereleaseVersions)
                                              {
                                                  AcceptedTargets = PackageTargets.Project
                                              });
        }

        private void Execute(IPackage package, IPackageOperationResolver resolver)
        {
            IEnumerable<PackageOperation> operations = resolver.ResolveOperations(package);
            if (operations.Any())
            {
                foreach (PackageOperation operation in operations)
                {
                    Execute(operation);
                }
            }
            else if (LocalRepository.Exists(package))
            {
                Logger.Log(MessageLevel.Info, NuGetResources.Log_ProjectAlreadyReferencesPackage, Project.ProjectName, package.GetFullName());
            }
        }

        protected void Execute(PackageOperation operation)
        {
            bool packageExists = LocalRepository.Exists(operation.Package);

            if (operation.Action == PackageAction.Install)
            {
                // If the package is already installed, then skip it
                if (packageExists)
                {
                    Logger.Log(MessageLevel.Info, NuGetResources.Log_ProjectAlreadyReferencesPackage, Project.ProjectName, operation.Package.GetFullName());
                }
                else
                {
                    AddPackageReferenceToProject(operation.Package);
                }
            }
            else
            {
                if (packageExists)
                {
                    RemovePackageReferenceFromProject(operation.Package);
                }
            }
        }

        protected void AddPackageReferenceToProject(IPackage package)
        {
            string packageFullName = package.GetFullName();
            Logger.Log(MessageLevel.Info, NuGetResources.Log_BeginAddPackageReference, packageFullName, Project.ProjectName);

            PackageOperationEventArgs args = CreateOperation(package);
            OnPackageReferenceAdding(args);

            if (args.Cancel)
            {
                return;
            }

            ExtractPackageFilesToProject(package);

            Logger.Log(MessageLevel.Info, NuGetResources.Log_SuccessfullyAddedPackageReference, packageFullName, Project.ProjectName);
            OnPackageReferenceAdded(args);
        }

        protected virtual void ExtractPackageFilesToProject(IPackage package)
        {
            // BUG 491: Installing a package with incompatible binaries still does a partial install.
            // Resolve assembly references and content files first so that if this fails we never do anything to the project
            List<IPackageAssemblyReference> assemblyReferences = Project.GetCompatibleItemsCore(package.AssemblyReferences).ToList();
            List<FrameworkAssemblyReference> frameworkReferences = Project.GetCompatibleItemsCore(package.FrameworkAssemblies).ToList();
            List<IPackageFile> contentFiles = Project.GetCompatibleItemsCore(package.GetContentFiles()).ToList();
            List<IPackageFile> buildFiles = Project.GetCompatibleItemsCore(package.GetBuildFiles()).ToList();

            // If the package doesn't have any compatible assembly references or content files,
            // throw, unless it's a meta package.
            if (assemblyReferences.Count == 0 && frameworkReferences.Count == 0 && contentFiles.Count == 0 && buildFiles.Count == 0 &&
                (package.FrameworkAssemblies.Any() || package.AssemblyReferences.Any() || package.GetContentFiles().Any() || package.GetBuildFiles().Any()))
            {
                // for portable framework, we want to show the friendly short form (e.g. portable-win8+net45+wp8) instead of ".NETPortable, Profile=Profile104".
                string targetFrameworkString = VersionUtility.IsPortableFramework(Project.TargetFramework) 
                                                    ? VersionUtility.GetShortFrameworkName(Project.TargetFramework) 
                                                    : Project.TargetFramework.ToString();

                throw new InvalidOperationException(
                           String.Format(CultureInfo.CurrentCulture,
                           NuGetResources.UnableToFindCompatibleItems, package.GetFullName(), targetFrameworkString));
            }

            // IMPORTANT: this filtering has to be done AFTER the 'if' statement above,
            // so that we don't throw the exception in case the <References> filters out all assemblies.
            FilterAssemblyReferences(assemblyReferences, package.PackageAssemblyReferences);

            try
            {
                // Add content files
                Project.AddFiles(contentFiles, _fileTransformers);

                // Add the references to the reference path
                foreach (IPackageAssemblyReference assemblyReference in assemblyReferences)
                {
                    if (assemblyReference.IsEmptyFolder())
                    {
                        continue;
                    }

                    // Get the physical path of the assembly reference
                    string referencePath = Path.Combine(PathResolver.GetInstallPath(package), assemblyReference.Path);
                    string relativeReferencePath = PathUtility.GetRelativePath(Project.Root, referencePath);

                    if (Project.ReferenceExists(assemblyReference.Name))
                    {
                        Project.RemoveReference(assemblyReference.Name);
                    }

                    // The current implementation of all ProjectSystem does not use the Stream parameter at all.
                    // We can't change the API now, so just pass in a null stream.
                    Project.AddReference(relativeReferencePath, Stream.Null);
                }

                // Add GAC/Framework references
                foreach (FrameworkAssemblyReference frameworkReference in frameworkReferences)
                {
                    if (!Project.ReferenceExists(frameworkReference.AssemblyName))
                    {
                        Project.AddFrameworkReference(frameworkReference.AssemblyName);
                    }
                }

                foreach (var importFile in buildFiles)
                {
                    string fullImportFilePath = Path.Combine(PathResolver.GetInstallPath(package), importFile.Path);
                    Project.AddImport(
                        fullImportFilePath, 
                        importFile.Path.EndsWith(".props", StringComparison.OrdinalIgnoreCase) ? ProjectImportLocation.Top : ProjectImportLocation.Bottom);
                }
            }
            finally
            {
                if (_packageReferenceRepository != null)
                {
                    // save the used project's framework if the repository supports it.
                    _packageReferenceRepository.AddPackage(package.Id, package.Version, Project.TargetFramework);
                }
                else
                {
                    // Add package to local repository in the finally so that the user can uninstall it
                    // if any exception occurs. This is easier than rolling back since the user can just
                    // manually uninstall things that may have failed.
                    // If this fails then the user is out of luck.
                    LocalRepository.AddPackage(package);
                }
            }
        }

        private void FilterAssemblyReferences(List<IPackageAssemblyReference> assemblyReferences, ICollection<PackageReferenceSet> packageAssemblyReferences)
        {
            if (packageAssemblyReferences != null && packageAssemblyReferences.Count > 0)
            {
                var packageReferences = Project.GetCompatibleItemsCore(packageAssemblyReferences).FirstOrDefault();
                if (packageReferences != null)
                {
                    // remove all assemblies of which names do not appear in the References list
                    assemblyReferences.RemoveAll(assembly => !packageReferences.References.Contains(assembly.Name, StringComparer.OrdinalIgnoreCase));
                }
            }
        }

        public bool IsInstalled(IPackage package)
        {
            return LocalRepository.Exists(package);
        }

        public void RemovePackageReference(string packageId)
        {
            RemovePackageReference(packageId, forceRemove: false, removeDependencies: false);
        }

        public void RemovePackageReference(string packageId, bool forceRemove)
        {
            RemovePackageReference(packageId, forceRemove: forceRemove, removeDependencies: false);
        }

        public virtual void RemovePackageReference(string packageId, bool forceRemove, bool removeDependencies)
        {
            if (String.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "packageId");
            }

            IPackage package = LocalRepository.FindPackage(packageId);

            if (package == null)
            {
                throw new InvalidOperationException(String.Format(
                    CultureInfo.CurrentCulture,
                    NuGetResources.UnknownPackage, packageId));
            }

            RemovePackageReference(package, forceRemove, removeDependencies);
        }

        public virtual void RemovePackageReference(IPackage package, bool forceRemove, bool removeDependencies)
        {
            FrameworkName targetFramework = GetPackageTargetFramework(package.Id);
            Execute(package, new UninstallWalker(LocalRepository,
                                                 new DependentsWalker(LocalRepository, targetFramework),
                                                 targetFramework,
                                                 NullLogger.Instance,
                                                 removeDependencies,
                                                 forceRemove));
        }

        [SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
        private void RemovePackageReferenceFromProject(IPackage package)
        {
            string packageFullName = package.GetFullName();
            Logger.Log(MessageLevel.Info, NuGetResources.Log_BeginRemovePackageReference, packageFullName, Project.ProjectName);

            PackageOperationEventArgs args = CreateOperation(package);
            OnPackageReferenceRemoving(args);

            if (args.Cancel)
            {
                return;
            }

            // Get other packages
            IEnumerable<IPackage> otherPackages = from p in LocalRepository.GetPackages()
                                                  where p.Id != package.Id
                                                  select p;

            // Get other references
            var otherAssemblyReferences = from p in otherPackages
                                          let assemblyReferences = GetFilteredAssembliesToDelete(p)
                                          from assemblyReference in assemblyReferences ?? Enumerable.Empty<IPackageAssemblyReference>() // This can happen if package installed left the project in a bad state
                                          select assemblyReference;

            // Get content files from other packages
            // Exclude transform files since they are treated specially
            var otherContentFiles = from p in otherPackages
                                    from file in GetCompatibleInstalledItemsForPackage(p.Id, p.GetContentFiles())
                                    where !_fileTransformers.ContainsKey(Path.GetExtension(file.Path))
                                    select file;

            // Get the files and references for this package, that aren't in use by any other packages so we don't have to do reference counting
            var assemblyReferencesToDelete = GetFilteredAssembliesToDelete(package)
                                             .Except(otherAssemblyReferences, PackageFileComparer.Default);

            var contentFilesToDelete = GetCompatibleInstalledItemsForPackage(package.Id, package.GetContentFiles())
                                       .Except(otherContentFiles, PackageFileComparer.Default);

            var buildFilesToDelete = GetCompatibleInstalledItemsForPackage(package.Id, package.GetBuildFiles());

            // Delete the content files
            Project.DeleteFiles(contentFilesToDelete, otherPackages, _fileTransformers);

            // Remove references
            foreach (IPackageAssemblyReference assemblyReference in assemblyReferencesToDelete)
            {
                Project.RemoveReference(assemblyReference.Name);
            }

            // remove the <Import> statement from projects for the .targets and .props files
            foreach (var importFile in buildFilesToDelete)
            {
                string fullImportFilePath = Path.Combine(PathResolver.GetInstallPath(package), importFile.Path);
                Project.RemoveImport(fullImportFilePath);
            }

            // Remove package to the repository
            LocalRepository.RemovePackage(package);

            Logger.Log(MessageLevel.Info, NuGetResources.Log_SuccessfullyRemovedPackageReference, packageFullName, Project.ProjectName);
            OnPackageReferenceRemoved(args);
        }

        private IList<IPackageAssemblyReference> GetFilteredAssembliesToDelete(IPackage package)
        {
            List<IPackageAssemblyReference> assemblyReferences = GetCompatibleInstalledItemsForPackage(package.Id, package.AssemblyReferences).ToList();
            if (assemblyReferences.Count == 0)
            {
                return assemblyReferences;
            }

            var packageReferences = GetCompatibleInstalledItemsForPackage(package.Id, package.PackageAssemblyReferences).FirstOrDefault();
            if (packageReferences != null) 
            {
                assemblyReferences.RemoveAll(p => !packageReferences.References.Contains(p.Name, StringComparer.OrdinalIgnoreCase));
            }

            return assemblyReferences;
        }

        public void UpdatePackageReference(string packageId)
        {
            UpdatePackageReference(packageId, version: null, updateDependencies: true, allowPrereleaseVersions: false);
        }

        public void UpdatePackageReference(string packageId, SemanticVersion version)
        {
            UpdatePackageReference(packageId, version: version, updateDependencies: true, allowPrereleaseVersions: false);
        }

        public void UpdatePackageReference(string packageId, IVersionSpec versionSpec, bool updateDependencies, bool allowPrereleaseVersions)
        {
            UpdatePackageReference(
                packageId, 
                () => SourceRepository.FindPackage(packageId, versionSpec, ConstraintProvider, allowPrereleaseVersions, allowUnlisted: false),
                updateDependencies, 
                allowPrereleaseVersions, 
                targetVersionSetExplicitly: versionSpec != null);
        }

        public virtual void UpdatePackageReference(string packageId, SemanticVersion version, bool updateDependencies, bool allowPrereleaseVersions)
        {
            UpdatePackageReference(packageId, () => SourceRepository.FindPackage(packageId, version, ConstraintProvider, allowPrereleaseVersions, allowUnlisted: false), updateDependencies, allowPrereleaseVersions, targetVersionSetExplicitly: version != null);
        }

        private void UpdatePackageReference(string packageId, Func<IPackage> resolvePackage, bool updateDependencies, bool allowPrereleaseVersions, bool targetVersionSetExplicitly)
        {
            if (String.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "packageId");
            }

            IPackage oldPackage = LocalRepository.FindPackage(packageId);

            // Check to see if this package is installed
            if (oldPackage == null)
            {
                throw new InvalidOperationException(
                    String.Format(CultureInfo.CurrentCulture,
                    NuGetResources.ProjectDoesNotHaveReference, Project.ProjectName, packageId));
            }

            Logger.Log(MessageLevel.Debug, NuGetResources.Debug_LookingForUpdates, packageId);

            IPackage package = resolvePackage();

            // the condition (allowPrereleaseVersions || targetVersionSetExplicitly || oldPackage.IsReleaseVersion() || !package.IsReleaseVersion() || oldPackage.Version < package.Version)
            // is to fix bug 1574. We want to do nothing if, let's say, you have package 2.0alpha installed, and you do:
            //      update-package
            // without specifying a version explicitly, and the feed only has version 1.0 as the latest stable version.
            if (package != null &&
                oldPackage.Version != package.Version &&
                (allowPrereleaseVersions || targetVersionSetExplicitly || oldPackage.IsReleaseVersion() || !package.IsReleaseVersion() || oldPackage.Version < package.Version))
            {
                Logger.Log(MessageLevel.Info, NuGetResources.Log_UpdatingPackages, package.Id, oldPackage.Version, package.Version, Project.ProjectName);
                UpdatePackageReference(package, updateDependencies, allowPrereleaseVersions);
            }
            else
            {
                IVersionSpec constraint = ConstraintProvider.GetConstraint(packageId);
                if (constraint != null)
                {
                    Logger.Log(MessageLevel.Info, NuGetResources.Log_ApplyingConstraints, packageId, VersionUtility.PrettyPrint(constraint), ConstraintProvider.Source);
                }

                Logger.Log(MessageLevel.Info, NuGetResources.Log_NoUpdatesAvailableForProject, packageId, Project.ProjectName);
            }
        }

        protected void UpdatePackageReference(IPackage package)
        {
            UpdatePackageReference(package, updateDependencies: true, allowPrereleaseVersions: false);
        }

        protected void UpdatePackageReference(IPackage package, bool updateDependencies, bool allowPrereleaseVersions)
        {
            AddPackageReference(package, !updateDependencies, allowPrereleaseVersions);
        }

        private void OnPackageReferenceAdding(PackageOperationEventArgs e)
        {
            if (PackageReferenceAdding != null)
            {
                PackageReferenceAdding(this, e);
            }
        }

        private void OnPackageReferenceAdded(PackageOperationEventArgs e)
        {
            if (PackageReferenceAdded != null)
            {
                PackageReferenceAdded(this, e);
            }
        }

        private void OnPackageReferenceRemoved(PackageOperationEventArgs e)
        {
            if (PackageReferenceRemoved != null)
            {
                PackageReferenceRemoved(this, e);
            }
        }

        private void OnPackageReferenceRemoving(PackageOperationEventArgs e)
        {
            if (PackageReferenceRemoving != null)
            {
                PackageReferenceRemoving(this, e);
            }
        }

        private FrameworkName GetPackageTargetFramework(string packageId)
        {
            if (_packageReferenceRepository != null)
            {
                return _packageReferenceRepository.GetPackageTargetFramework(packageId) ?? Project.TargetFramework;
            }

            return Project.TargetFramework;
        }

        /// <summary>
        /// This method uses the 'targetFramework' attribute in the packages.config to determine compatible items.
        /// Hence, it's only good for uninstall operations.
        /// </summary>
        private IEnumerable<T> GetCompatibleInstalledItemsForPackage<T>(string packageId, IEnumerable<T> items) where T : IFrameworkTargetable
        {
            FrameworkName packageFramework = GetPackageTargetFramework(packageId);
            if (packageFramework == null)
            {
                return items;
            }

            IEnumerable<T> compatibleItems;
            if (VersionUtility.TryGetCompatibleItems(packageFramework, items, out compatibleItems))
            {
                return compatibleItems;
            }
            return Enumerable.Empty<T>();
        }
       
        private PackageOperationEventArgs CreateOperation(IPackage package)
        {
            return new PackageOperationEventArgs(package, Project, PathResolver.GetInstallPath(package));
        }

        private static IDictionary<XName, Action<XElement, XElement>> GetConfigMappings()
        {
            // REVIEW: This might be an edge case, but we're setting this rule for all xml files.
            // If someone happens to do a transform where the xml file has a configSections node
            // we will add it first. This is probably fine, but this is a config specific scenario
            return new Dictionary<XName, Action<XElement, XElement>>() {
                { "configSections" , (parent, element) => parent.AddFirst(element) }
            };
        }

        private class PackageFileComparer : IEqualityComparer<IPackageFile>
        {
            internal readonly static PackageFileComparer Default = new PackageFileComparer();
            private PackageFileComparer()
            {
            }

            public bool Equals(IPackageFile x, IPackageFile y)
            {
                // technically, this check will fail if, for example, 'x' is a content file and 'y' is a lib file.
                // However, because we only use this comparer to compare files within the same folder type, 
                // this check is sufficient.
                return x.TargetFramework == y.TargetFramework &&
                       x.EffectivePath.Equals(y.EffectivePath, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(IPackageFile obj)
            {
                return obj.Path.GetHashCode();
            }
        }
    }
}