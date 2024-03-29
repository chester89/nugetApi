﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NuGet.Resources;

namespace NuGet
{
    public class SharedPackageRepository : LocalPackageRepository, ISharedPackageRepository
    {
        private const string StoreFilePath = "repositories.config";
        private readonly PackageReferenceFile _packageReferenceFile;
        private readonly IFileSystem _storeFileSystem;

        public SharedPackageRepository(string path)
            : base(path)
        {
            _storeFileSystem = FileSystem;
        }

        public SharedPackageRepository(IPackagePathResolver resolver, IFileSystem fileSystem, IFileSystem configSettingsFileSystem)
            : this(resolver, fileSystem, fileSystem, configSettingsFileSystem)
        {
        }

        public SharedPackageRepository(IPackagePathResolver resolver, IFileSystem fileSystem, IFileSystem storeFileSystem, IFileSystem configSettingsFileSystem)
            : base(resolver, fileSystem)
        {
            if (configSettingsFileSystem == null)
            {
                throw new ArgumentNullException("configSettingsFileSystem");
            }

            _storeFileSystem = storeFileSystem ?? fileSystem;
            _packageReferenceFile = new PackageReferenceFile(configSettingsFileSystem, Constants.PackageReferenceFile);
        }

        public PackageReferenceFile PackageReferenceFile
        {
            get { return _packageReferenceFile; }
        }

        public override bool SupportsPrereleasePackages
        {
            get { return true; }
        }

        public void RegisterRepository(string path)
        {
            AddEntry(path);
        }

        public void UnregisterRepository(string path)
        {
            DeleteEntry(path);
        }

        public bool IsReferenced(string packageId, SemanticVersion version)
        {
            // See if this package exists in any other repository before we remove it
            return GetRepositories().Any(r => r.Exists(packageId, version));
        }

        public override bool Exists(string packageId, SemanticVersion version)
        {
            if (version != null)
            {
                // optimization: if we find the .nuspec file at "id.version"\"id.version".nuspec or 
                // the .nupkg file at "id.version"\"id.version".nupkg, consider it exists
                bool hasPackageDirectory = version.GetComparableVersionStrings()
                                                  .Select(v => packageId + "." + v)
                                                  .Any(path => FileSystem.FileExists(Path.Combine(path, path + Constants.PackageExtension)) ||
                                                               FileSystem.FileExists(Path.Combine(path, path + Constants.ManifestExtension)));
                if (hasPackageDirectory)
                {
                    return true;
                }
            }

            return FindPackage(packageId, version) != null;
        }

        public override IPackage FindPackage(string packageId, SemanticVersion version)
        {
            var package = base.FindPackage(packageId, version);
            if (package != null)
            {
                return package;
            }

            // if we didn't find the .nupkg file, search for .nuspec file
            if (version != null)
            {
                string packagePath = GetManifestFilePath(packageId, version);
                if (FileSystem.FileExists(packagePath))
                {
                    string packageDirectory = PathResolver.GetPackageDirectory(packageId, version);
                    return new UnzippedPackage(FileSystem, packageDirectory);
                }
            }
            
            return null;
        }

        public void AddPackageReferenceEntry(string packageId, SemanticVersion version)
        {
            if (_packageReferenceFile != null)
            {
                _packageReferenceFile.AddEntry(packageId, version);
            }
        }

        public override IQueryable<IPackage> GetPackages()
        {
            return SearchPackages().AsQueryable();
        }

        /// <summary>
        /// Search for either .nupkg-based or .nuspec-based packages
        /// </summary>
        /// <returns></returns>
        protected IEnumerable<IPackage> SearchPackages()
        {
            foreach (string directory in FileSystem.GetDirectories(""))
            {
                string partialPath = Path.Combine(directory, directory);

                // prefer .nupkg file over .nuspec file
                string nupkgPath = partialPath + Constants.PackageExtension;
                if (FileSystem.FileExists(nupkgPath))
                {
                    yield return new SharedOptimizedZipPackage(FileSystem, nupkgPath);
                }
                else if (FileSystem.FileExists(partialPath + Constants.ManifestExtension))
                {
                    // always search for .nuspec-based packages last
                    yield return new UnzippedPackage(FileSystem, directory);
                    continue;
                }
            }
        }

        public override void AddPackage(IPackage package)
        {
            // Starting from 2.1, we save the nuspec file into the subdirectory with the name as <packageId>.<version>
            // for example, for jQuery version 1.0, it will be "jQuery.1.0\\jQuery.1.0.nuspec"
            string packageFilePath = GetManifestFilePath(package.Id, package.Version);
            Manifest manifest = Manifest.Create(package);

            // The IPackage object doesn't carry the References information.
            // Thus we set the References for the manifest to the set of all valid assembly references
            manifest.Metadata.ReferenceSets = package.AssemblyReferences
                                                  .GroupBy(f => f.TargetFramework)
                                                  .Select(
                                                    g => new ManifestReferenceSet
                                                         {
                                                             TargetFramework = g.Key == null ? null : VersionUtility.GetFrameworkString(g.Key),
                                                             References = g.Select(p => new ManifestReference { File = p.Name }).ToList()
                                                         })
                                                  .ToList();

            FileSystem.AddFileWithCheck(packageFilePath, manifest.Save);

            // But in order to maintain backwards compatibility with older versions of NuGet, 
            // we will save the .nupkg file too. This way, 2.1 will read the .nuspec file, and 
            // pre 2.1 will read the .nupkg
            base.AddPackage(package);

            // if this is a solution-level package, add it to the solution's packages.config file
            if (_packageReferenceFile != null && IsSolutionLevel(package))
            {
                _packageReferenceFile.AddEntry(package.Id, package.Version);
            }
        }

        public override void RemovePackage(IPackage package)
        {
            // Delete the entire package directory
            FileSystem.DeleteDirectorySafe(PathResolver.GetPackageDirectory(package), recursive: true);

            // If this is the last package delete the package directory
            if (!FileSystem.GetFilesSafe(String.Empty).Any() &&
                !FileSystem.GetDirectoriesSafe(String.Empty).Any())
            {
                FileSystem.DeleteDirectorySafe(String.Empty, recursive: false);
            }

            if (_packageReferenceFile != null)
            {
                _packageReferenceFile.DeleteEntry(package.Id, package.Version);
            }
        }

        public bool IsSolutionReferenced(string packageId, SemanticVersion version)
        {
            return _packageReferenceFile != null && _packageReferenceFile.EntryExists(packageId, version);
        }

        protected virtual IPackageRepository CreateRepository(string path)
        {
            string root = PathUtility.EnsureTrailingSlash(FileSystem.Root);
            string absolutePath = PathUtility.GetAbsolutePath(root, path);
            string directory = Path.GetDirectoryName(absolutePath);
            return new PackageReferenceRepository(new PhysicalFileSystem(directory), this);
        }

        protected override IPackage OpenPackage(string path)
        {
            string extension = Path.GetExtension(path);
            if (extension.Equals(Constants.PackageExtension, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return new SharedOptimizedZipPackage(FileSystem, path);
                }
                catch (FileFormatException ex)
                {
                    throw new InvalidDataException(String.Format(CultureInfo.CurrentCulture, NuGetResources.ErrorReadingPackage, path), ex);
                }
            }
            else
            {
                if (extension.Equals(Constants.ManifestExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return new UnzippedPackage(FileSystem, Path.GetDirectoryName(path));
                }
            }

            return null;
        }

        private IEnumerable<IPackageRepository> GetRepositories()
        {
            return GetRepositoryPaths().Select(CreateRepository);
        }

        internal IEnumerable<string> GetRepositoryPaths()
        {
            // The store file is in this format
            // <repositories>
            //     <repository path="..\packages.config" />
            // </repositories>
            XDocument document = GetStoreDocument();

            // The document doesn't exist so do nothing
            if (document == null)
            {
                return Enumerable.Empty<string>();
            }

            // Only save if we changed the document
            bool requiresSave = false;

            // Paths have to be relative to the this repository           
            var paths = new HashSet<string>();
            foreach (var e in GetRepositoryElements(document).ToList())
            {
                string path = NormalizePath(e.GetOptionalAttributeValue("path"));

                if (String.IsNullOrEmpty(path) ||
                    !FileSystem.FileExists(path) ||
                    !paths.Add(path))
                {

                    // Skip bad entries
                    e.Remove();
                    requiresSave = true;
                }
            }

            if (requiresSave)
            {
                SaveDocument(document);
            }

            return paths;
        }

        private void AddEntry(string path)
        {
            path = NormalizePath(path);

            // Create the document if it doesn't exist
            XDocument document = GetStoreDocument(createIfNotExists: true);

            XElement element = FindEntry(document, path);

            if (element != null)
            {
                // The path exists already so do nothing
                return;
            }

            document.Root.Add(new XElement("repository",
                                  new XAttribute("path", path)));

            SaveDocument(document);
        }

        private void DeleteEntry(string path)
        {
            // Get the relative path
            path = NormalizePath(path);

            // Remove the repository from the document
            XDocument document = GetStoreDocument();

            if (document == null)
            {
                return;
            }

            XElement element = FindEntry(document, path);

            if (element != null)
            {
                element.Remove();

                // No more entries so remove the file
                if (!document.Root.HasElements)
                {
                    _storeFileSystem.DeleteFile(StoreFilePath);
                }
                else
                {
                    SaveDocument(document);
                }
            }
        }

        private static IEnumerable<XElement> GetRepositoryElements(XDocument document)
        {
            return from e in document.Root.Elements("repository")
                   select e;
        }

        private XElement FindEntry(XDocument document, string path)
        {
            path = NormalizePath(path);

            return (from e in GetRepositoryElements(document)
                    let entryPath = NormalizePath(e.GetOptionalAttributeValue("path"))
                    where path.Equals(entryPath, StringComparison.OrdinalIgnoreCase)
                    select e).FirstOrDefault();
        }

        private void SaveDocument(XDocument document)
        {
            // Sort the elements by path
            var repositoryElements = (from e in GetRepositoryElements(document)
                                      let path = e.GetOptionalAttributeValue("path")
                                      where !String.IsNullOrEmpty(path)
                                      orderby path.ToUpperInvariant()
                                      select e).ToList();

            // Remove all elements
            document.Root.RemoveAll();

            // Re-add them sorted
            repositoryElements.ForEach(e => document.Root.Add(e));

            _storeFileSystem.AddFile(StoreFilePath, document.Save);
        }

        private XDocument GetStoreDocument(bool createIfNotExists = false)
        {
            try
            {
                // If the file exists then open and return it
                if (_storeFileSystem.FileExists(StoreFilePath))
                {
                    using (Stream stream = _storeFileSystem.OpenFile(StoreFilePath))
                    {
                        try
                        {
                            return XDocument.Load(stream);
                        }
                        catch (XmlException)
                        {
                            // There was an error reading the file, but don't throw as a result
                        }
                    }
                }

                // If it doesn't exist and we're creating a new file then return a
                // document with an empty packages node
                if (createIfNotExists)
                {
                    return new XDocument(new XElement("repositories"));
                }

                return null;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(
                    String.Format(CultureInfo.CurrentCulture,
                                  NuGetResources.ErrorReadingFile,
                                  _storeFileSystem.GetFullPath(StoreFilePath)), e);
            }
        }

        private string NormalizePath(string path)
        {
            if (String.IsNullOrEmpty(path))
            {
                return path;
            }

            if (Path.IsPathRooted(path))
            {
                string root = PathUtility.EnsureTrailingSlash(FileSystem.Root);
                return PathUtility.GetRelativePath(root, path);
            }
            return path;
        }

        private bool IsSolutionLevel(IPackage package)
        {
            return !package.HasProjectContent() && !IsReferenced(package.Id, package.Version);
        }

        private string GetManifestFilePath(string packageId, SemanticVersion version)
        {
            string packageDirectory = PathResolver.GetPackageDirectory(packageId, version);
            string manifestFileName = packageDirectory + Constants.ManifestExtension;

            return Path.Combine(packageDirectory, manifestFileName);
        }

        private class SharedOptimizedZipPackage : OptimizedZipPackage
        {
            private readonly string _folderPath;

            public SharedOptimizedZipPackage(IFileSystem fileSystem, string packagePath)
                : base(fileSystem, packagePath, fileSystem)
            {
                _folderPath = Path.GetDirectoryName(packagePath);
            }

            protected override string GetExpandedFolderPath()
            {
                return _folderPath;
            }
        }
    }
}