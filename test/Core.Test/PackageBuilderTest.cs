﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Xml;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace NuGet.Test
{
    public class PackageBuilderTest
    {
        [Fact]
        public void OwnersFallsBackToAuthorsIfNoneSpecified()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = new SemanticVersion("1.0"),
                Description = "Description"
            };
            builder.Authors.Add("David");
            var ms = new MemoryStream();

            // Act
            Manifest.Create(builder).Save(ms);
            ms.Seek(0, SeekOrigin.Begin);

            // Assert
            Assert.Equal(@"<?xml version=""1.0""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0</version>
    <authors>David</authors>
    <owners>David</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Description</description>
  </metadata>
</package>", ms.ReadToEnd());
        }

        [Fact]
        public void ReleaseNotesAttributeIsRecognized()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = new SemanticVersion("1.0"),
                Description = "Description",
                ReleaseNotes = "Release Notes"
            };
            builder.Authors.Add("David");
            var ms = new MemoryStream();

            // Act
            Manifest.Create(builder).Save(ms);
            ms.Seek(0, SeekOrigin.Begin);

            // Assert
            Assert.Equal(@"<?xml version=""1.0""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0</version>
    <authors>David</authors>
    <owners>David</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Description</description>
    <releaseNotes>Release Notes</releaseNotes>
  </metadata>
</package>", ms.ReadToEnd());
        }

        [Fact]
        public void CreatePackageUsesV1SchemaNamespaceIfFrameworkAssembliesAreUsed()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = new SemanticVersion("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("David");
            builder.FrameworkReferences.Add(new FrameworkAssemblyReference("System.Web"));
            var ms = new MemoryStream();

            // Act
            Manifest.Create(builder).Save(ms);
            ms.Seek(0, SeekOrigin.Begin);

            // Assert
            Assert.Equal(@"<?xml version=""1.0""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0</version>
    <authors>David</authors>
    <owners>David</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
    <frameworkAssemblies>
      <frameworkAssembly assemblyName=""System.Web"" targetFramework="""" />
    </frameworkAssemblies>
  </metadata>
</package>", ms.ReadToEnd());
        }

        [Fact]
        public void CreatePackageUsesV2SchemaNamespaceIfReferenceAssembliesAreUsed()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = new SemanticVersion("1.0"),
                Description = "Descriptions",
            };
            builder.PackageAssemblyReferences.Add(new PackageReferenceSet(null, new string[] { "foo.dll" }));
            builder.Authors.Add("David");
            var ms = new MemoryStream();

            // Act
            Manifest.Create(builder).Save(ms);
            ms.Seek(0, SeekOrigin.Begin);

            // Assert
            Assert.Equal(@"<?xml version=""1.0""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0</version>
    <authors>David</authors>
    <owners>David</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
    <references>
      <reference file=""foo.dll"" />
    </references>
  </metadata>
</package>", ms.ReadToEnd());
        }

        [Fact]
        public void CreatePackageUsesV2SchemaNamespaceIfDependecyHasNoTargetFramework()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = new SemanticVersion("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("Luan");

            var dependencies = new PackageDependency[] { 
                new PackageDependency("B")
            };

            builder.DependencySets.Add(new PackageDependencySet(null, dependencies));
            var ms = new MemoryStream();

            // Act
            Manifest.Create(builder).Save(ms);
            ms.Seek(0, SeekOrigin.Begin);

            // Assert
            Assert.Equal(@"<?xml version=""1.0""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0</version>
    <authors>Luan</authors>
    <owners>Luan</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
    <dependencies>
      <dependency id=""B"" />
    </dependencies>
  </metadata>
</package>", ms.ReadToEnd());
        }

        [Fact]
        public void CreatePackageUsesV4SchemaNamespaceIfDependecyHasTargetFramework()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = new SemanticVersion("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("Luan");

            var fx = new FrameworkName("Silverlight", new Version("4.0"));
            var dependencies = new PackageDependency[] { 
                new PackageDependency("B", null)
            };
            builder.DependencySets.Add(new PackageDependencySet(fx, dependencies));

            var ms = new MemoryStream();

            // Act
            Manifest.Create(builder).Save(ms);
            ms.Seek(0, SeekOrigin.Begin);

            // Assert
            Assert.Equal(@"<?xml version=""1.0""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0</version>
    <authors>Luan</authors>
    <owners>Luan</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
    <dependencies>
      <group targetFramework=""Silverlight4.0"">
        <dependency id=""B"" />
      </group>
    </dependencies>
  </metadata>
</package>", ms.ReadToEnd());
        }

        [Fact]
        public void CreatePackageUsesV4SchemaNamespaceIfContentHasTargetFramework()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = new SemanticVersion("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("Luan");
            builder.Files.Add(CreatePackageFile("content\\winrt53\\one.txt"));

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                // Assert
                Assert.Equal(@"<?xml version=""1.0""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0</version>
    <authors>Luan</authors>
    <owners>Luan</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
  </metadata>
</package>", manifestStream.ReadToEnd());
            }
        }

        [Fact]
        public void CreatePackageDoesNotUseV4SchemaNamespaceIfContentHasUnsupportedTargetFramework()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = new SemanticVersion("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("Luan");
            builder.Files.Add(CreatePackageFile("content\\bar\\one.txt"));

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                // Assert
                Assert.Equal(@"<?xml version=""1.0""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0</version>
    <authors>Luan</authors>
    <owners>Luan</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
  </metadata>
</package>", manifestStream.ReadToEnd());
            }
        }

        [Fact]
        public void CreatePackageUsesV4SchemaNamespaceIfToolsHasTargetFramework()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = new SemanticVersion("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("Luan");
            builder.Files.Add(CreatePackageFile("tools\\sl4\\one.dll"));

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                // Assert
                Assert.Equal(@"<?xml version=""1.0""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0</version>
    <authors>Luan</authors>
    <owners>Luan</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
  </metadata>
</package>", manifestStream.ReadToEnd());
            }
        }

        [Fact]
        public void CreatePackageDoesNotUseV4SchemaNamespaceIfToolsHasUnsupportedTargetFramework()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = new SemanticVersion("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("Luan");
            builder.Files.Add(CreatePackageFile("tools\\foo\\one.dll"));

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                // Assert
                Assert.Equal(@"<?xml version=""1.0""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0</version>
    <authors>Luan</authors>
    <owners>Luan</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
  </metadata>
</package>", manifestStream.ReadToEnd());
            }
        }

        [Theory]
        [InlineData("lib\\sl4\\_._")]
        [InlineData("content\\winrt\\_._")]
        [InlineData("tools\\sl4-wp\\_._")]
        public void CreatePackageUsesV4SchemaNamespaceIfLibHasEmptyTargetFramework(string packagePath)
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = new SemanticVersion("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("Luan");
            builder.Files.Add(CreatePackageFile(packagePath));

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                // Assert
                Assert.Equal(@"<?xml version=""1.0""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0</version>
    <authors>Luan</authors>
    <owners>Luan</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
  </metadata>
</package>", manifestStream.ReadToEnd());
            }
        }

        [Fact]
        public void CreatePackageUsesV5SchemaNamespaceIfReferencesTargetFramework()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = new SemanticVersion("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("Luan");
            builder.PackageAssemblyReferences.Add(
                new PackageReferenceSet(
                    new FrameworkName(".NET, Version=3.0"),
                    new[] { "one.dll" }));
            builder.Files.Add(CreatePackageFile("lib\\one.dll"));

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                // Assert
                Assert.Equal(@"<?xml version=""1.0""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0</version>
    <authors>Luan</authors>
    <owners>Luan</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
    <references>
      <group targetFramework="".NET3.0"">
        <reference file=""one.dll"" />
      </group>
    </references>
  </metadata>
</package>", manifestStream.ReadToEnd());
            }
        }

        [Fact]
        public void CreatePackageDoesNotUseV5SchemaNamespaceIfReferencesHasOnlyNullTargetFramework()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = new SemanticVersion("1.0"),
                Description = "Descriptions",
            };
            builder.Authors.Add("Luan");
            builder.PackageAssemblyReferences.Add(
                new PackageReferenceSet(
                    null,
                    new[] { "one.dll" }));
            builder.Files.Add(CreatePackageFile("lib\\one.dll"));

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                // Assert
                Assert.Equal(@"<?xml version=""1.0""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0</version>
    <authors>Luan</authors>
    <owners>Luan</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
    <references>
      <reference file=""one.dll"" />
    </references>
  </metadata>
</package>", manifestStream.ReadToEnd());
            }
        }

        [Fact]
        public void CreatePackageUsesV5SchemaNamespaceIfMinClientVersionIsSet()
        {
            // Arrange
            var builder = new PackageBuilder()
            {
                Id = "A",
                Version = new SemanticVersion("1.0"),
                Description = "Descriptions",
                MinClientVersion = new Version("2.0")
            };
            builder.Authors.Add("Luan");
            builder.Files.Add(CreatePackageFile("a.txt"));

            using (var ms = new MemoryStream())
            {
                builder.Save(ms);

                ms.Seek(0, SeekOrigin.Begin);

                var manifestStream = GetManifestStream(ms);

                // Assert
                Assert.Equal(@"<?xml version=""1.0""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"">
  <metadata minClientVersion=""2.0"">
    <id>A</id>
    <version>1.0</version>
    <authors>Luan</authors>
    <owners>Luan</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
  </metadata>
</package>", manifestStream.ReadToEnd());
            }
        }

        [Fact]
        public void CreatePackageTrimsExtraWhitespace()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "                 A                 ",
                Version = new SemanticVersion("1.0"),
                Description = "Descriptions                                         ",
                Summary = "                            Summary",
                Language = "     en-us   ",
                Copyright = "            Copyright 2012                "
            };
            builder.Authors.Add("David");
            builder.Owners.Add("John");
            builder.Tags.Add("t1");
            builder.Tags.Add("t2");
            builder.Tags.Add("t3");
            var dependencies = new PackageDependency[] { 
                new PackageDependency("    X     ")
            };
            builder.DependencySets.Add(new PackageDependencySet(null, dependencies));
            var ms = new MemoryStream();

            // Act
            Manifest.Create(builder).Save(ms);
            ms.Seek(0, SeekOrigin.Begin);

            // Assert
            Assert.Equal(@"<?xml version=""1.0""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0</version>
    <authors>David</authors>
    <owners>John</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
    <summary>Summary</summary>
    <copyright>Copyright 2012</copyright>
    <language>en-us</language>
    <tags>t1 t2 t3</tags>
    <dependencies>
      <dependency id=""X"" />
    </dependencies>
  </metadata>
</package>", ms.ReadToEnd());
        }

        [Fact]
        public void VersionFormatIsPreserved()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = new SemanticVersion("1.0"),
                Description = "Descriptions",
                Summary = "Summary",
            };
            builder.Authors.Add("David");
            
            var dependencySet = new PackageDependencySet(null, new [] {
                new PackageDependency("B", new VersionSpec
                    {
                        MinVersion = new SemanticVersion("1.0"),
                        IsMinInclusive = true
                    }),
                new PackageDependency("C", new VersionSpec
                {
                    MinVersion = new SemanticVersion("1.0"),
                    MaxVersion = new SemanticVersion("5.0"),
                    IsMinInclusive = false
                })
            });

            builder.DependencySets.Add(dependencySet);

            var ms = new MemoryStream();

            // Act
            Manifest.Create(builder).Save(ms);
            ms.Seek(0, SeekOrigin.Begin);

            // Assert
            Assert.Equal(@"<?xml version=""1.0""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
  <metadata>
    <id>A</id>
    <version>1.0</version>
    <authors>David</authors>
    <owners>David</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <description>Descriptions</description>
    <summary>Summary</summary>
    <dependencies>
      <dependency id=""B"" version=""1.0"" />
      <dependency id=""C"" version=""(1.0, 5.0)"" />
    </dependencies>
  </metadata>
</package>", ms.ReadToEnd());
        }


        [Fact]
        public void SavingPackageWithDuplicateDependenciesThrows()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = new SemanticVersion("1.0"),
                Description = "Descriptions",
                Summary = "Summary",
            };
            builder.Authors.Add("David");

            var dependencySet = new PackageDependencySet(null, new[] {
                new PackageDependency("B", new VersionSpec
                    {
                        MinVersion = new SemanticVersion("1.0"),
                        IsMinInclusive = true
                    }),
                new PackageDependency("B", new VersionSpec
                {
                    MinVersion = new SemanticVersion("1.0"),
                    MaxVersion = new SemanticVersion("5.0"),
                    IsMinInclusive = false
                })
            });

            builder.DependencySets.Add(dependencySet);

            var ms = new MemoryStream();

            // Act
            ExceptionAssert.Throws<InvalidOperationException>(() => Manifest.Create(builder).Save(ms), "'A' already has a dependency defined for 'B'.");
        }

        [Fact]
        public void SavingPackageWithInvalidDependencyRangeThrows()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = new SemanticVersion("1.0"),
                Description = "Descriptions",
                Summary = "Summary",
            };
            builder.Authors.Add("David");

            var dependencySet = new PackageDependencySet(null, new[] {
                new PackageDependency("B", new VersionSpec
                {
                    MinVersion = new SemanticVersion("1.0"),
                    MaxVersion = new SemanticVersion("1.0")
                })
            });

            builder.DependencySets.Add(dependencySet);

            var ms = new MemoryStream();

            // Act
            ExceptionAssert.Throws<InvalidOperationException>(() => Manifest.Create(builder).Save(ms), "Dependency 'B' has an invalid version.");
        }

        [Fact]
        public void SavingPackageValidatesReferences()
        {
            // Arrange
            var builder = new PackageBuilder
            {
                Id = "A",
                Version = new SemanticVersion("1.0"),
                Description = "Test",
            };
            builder.Authors.Add("Test");
            builder.Files.Add(new PhysicalPackageFile { TargetPath = @"lib\Foo.dll" });
            builder.PackageAssemblyReferences.Add(new PackageReferenceSet(null, new string[] { "Bar.dll" }));

            ExceptionAssert.Throws<InvalidDataException>(() => builder.Save(new MemoryStream()),
                "Invalid assembly reference 'Bar.dll'. Ensure that a file named 'Bar.dll' exists in the lib directory.");
        }

        [Fact]
        public void SavingPackageWithInvalidDependencyVersionMaxLessThanMinThrows()
        {
            // Arrange
            PackageBuilder builder = new PackageBuilder()
            {
                Id = "A",
                Version = new SemanticVersion("1.0"),
                Description = "Descriptions",
                Summary = "Summary",
            };
            builder.Authors.Add("David");

            var dependencySet = new PackageDependencySet(null, new[] {
                new PackageDependency("B", new VersionSpec
                {
                    MinVersion = new SemanticVersion("2.0"),
                    MaxVersion = new SemanticVersion("1.0")
                })
            });

            builder.DependencySets.Add(dependencySet);


            var ms = new MemoryStream();

            // Act
            ExceptionAssert.Throws<InvalidOperationException>(() => Manifest.Create(builder).Save(ms), "Dependency 'B' has an invalid version.");
        }

        [Fact]
        public void SaveThrowsIfRequiredPropertiesAreMissing()
        {
            // Arrange
            var builder = new PackageBuilder();
            builder.Id = "Package";
            builder.Files.Add(new Mock<IPackageFile>().Object);

            // Act & Assert
            ExceptionAssert.Throws<ValidationException>(() => builder.Save(new MemoryStream()), @"Version is required.
Authors is required.
Description is required.");
        }

        [Fact]
        public void SaveThrowsIfNoFilesOrDependencies()
        {
            // Arrange
            var builder = new PackageBuilder();
            builder.Id = "A";
            builder.Version = new SemanticVersion("1.0");
            builder.Description = "Description";

            // Act & Assert
            ExceptionAssert.Throws<InvalidOperationException>(() => builder.Save(new MemoryStream()), "Cannot create a package that has no dependencies nor content.");
        }

        [Fact]
        public void PackageBuilderThrowsIfXmlIsMalformed()
        {
            // Arrange
            string spec1 = "kjdkfj";
            string spec2 = @"<?xml version=""1.0"" encoding=""utf-8""?>";
            string spec3 = @"<?xml version=""1.0"" encoding=""utf-8""?><package />";
            string spec4 = @"<?xml version=""1.0"" encoding=""utf-8""?><package><metadata></metadata></package>";

            // Switch to invariant culture to ensure the error message is in english.
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            // Act and Assert
            ExceptionAssert.Throws<XmlException>(() => new PackageBuilder(spec1.AsStream(), null), "Data at the root level is invalid. Line 1, position 1.");
            ExceptionAssert.Throws<XmlException>(() => new PackageBuilder(spec2.AsStream(), null), "Root element is missing.");
            ExceptionAssert.Throws<InvalidOperationException>(() => new PackageBuilder(spec3.AsStream(), null));
            ExceptionAssert.Throws<InvalidOperationException>(() => new PackageBuilder(spec4.AsStream(), null));
        }

        [Fact]
        public void MissingIdThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package><metadata>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <language>en-us</language>
    <description>Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).</description>
  </metadata></package>";

            // Switch to invariant culture to ensure the error message is in english.
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            // Act & Assert
            ExceptionAssert.Throws<InvalidOperationException>(() => new PackageBuilder(spec.AsStream(), null), "The element 'metadata' in namespace 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd' has incomplete content. List of possible elements expected: 'id' in namespace 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd'.");
        }

        [Fact]
        public void IdExceedingMaxLengthThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package><metadata>
    <id>aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <language>en-us</language>
    <description>Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).</description>
  </metadata></package>";

            // Act & Assert
            ExceptionAssert.Throws<ValidationException>(() => new PackageBuilder(spec.AsStream(), null), "Id must not exceed 100 characters.");
        }

        [Fact]
        public void SpecialVersionExceedingMaxLengthThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package><metadata>
    <id>aaa</id>
    <version>2.5.0-vvvvvvvvvvvvvvvvvvvvv</version>
    <authors>Velio Ivanov</authors>
    <language>en-us</language>
    <description>Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).</description>
    <dependencies>
       <dependency id=""A"" />
    </dependencies>
  </metadata>
</package>";

            var builder = new PackageBuilder(spec.AsStream(), null);

            // Act & Assert
            ExceptionAssert.Throws<InvalidOperationException>(
                () => builder.Save(new MemoryStream()),
                "The special version part cannot exceed 20 characters.");
        }

        [Fact]
        public void MissingVersionThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package><metadata>
    <id>Artem.XmlProviders</id>
    <authors>Velio Ivanov</authors>
    <language>en-us</language>    
    <description>Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).</description>
  </metadata></package>";

            // Switch to invariant culture to ensure the error message is in english.
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            // Act & Assert
            ExceptionAssert.Throws<InvalidOperationException>(() => new PackageBuilder(spec.AsStream(), null), "The element 'metadata' in namespace 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd' has incomplete content. List of possible elements expected: 'version' in namespace 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd'.");
        }

        [Fact]
        public void MissingAuthorsThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package><metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <language>en-us</language>
    <description>Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).</description>
  </metadata></package>";

            // Switch to invariant culture to ensure the error message is in english.
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            // Act & Assert
            ExceptionAssert.Throws<InvalidOperationException>(() => new PackageBuilder(spec.AsStream(), null), "The element 'metadata' in namespace 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd' has incomplete content. List of possible elements expected: 'authors' in namespace 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd'.");
        }

        [Fact]
        public void MissingDescriptionThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package><metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <language>en-us</language>
  </metadata></package>";

            // Switch to invariant culture to ensure the error message is in english.
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            // Act & Assert
            ExceptionAssert.Throws<InvalidOperationException>(() => new PackageBuilder(spec.AsStream(), null), "The element 'metadata' in namespace 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd' has incomplete content. List of possible elements expected: 'description' in namespace 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd'.");
        }

        [Fact]
        public void MalformedDependenciesThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package><metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <language>en-us</language>
    <description>Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).</description>
    <dependencies>
        <dependency />
    </dependencies>
  </metadata></package>";

            // Switch to invariant culture to ensure the error message is in english.
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            // Act & Assert
            ExceptionAssert.Throws<InvalidOperationException>(() => new PackageBuilder(spec.AsStream(), null), "The required attribute 'id' is missing.");
        }

        [Fact]
        public void ReferencesContainMixedElementsThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package><metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <language>en-us</language>
    <description>Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).</description>
    <references>
        <reference file=""a.dll"" />
        <group>
           <reference file=""b.dll"" />
        </group>
    </references>
</metadata></package>";

            // Act & Assert
            ExceptionAssert.Throws<InvalidDataException>(() => new PackageBuilder(spec.AsStream(), null), "<references> element must not contain both <group> and <reference> child elements.");
        }

        [Fact]
        public void MissingFileSrcThrows()
        {
            // Act
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package><metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <language>en-us</language>
    <description>Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).</description>
    <dependencies>
        <dependency id=""foo"" />
    </dependencies>
  </metadata>
  <files>
    <file />
  </files>
</package>";

            // Switch to invariant culture to ensure the error message is in english.
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            // Assert
            ExceptionAssert.Throws<InvalidOperationException>(() => new PackageBuilder(spec.AsStream(), null), "The required attribute 'src' is missing.");
        }

        [Fact]
        public void MisplacedFileNodeThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package><metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <language>en-us</language>
    <description>Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).</description>
    <dependencies>
        <dependency id=""foo"" />
    </dependencies>
  <files>
    <file />
  </files>
  </metadata>
</package>";

            // Act & Assert
            ExceptionAssert.Throws<InvalidOperationException>(() => new PackageBuilder(spec.AsStream(), null));
        }

        [Fact]
        public void ReadingManifestWithNamespaceBuilderFromStreamCopiesMetadata()
        {
            // Arrange
            string spec = @"<?xml version=""1.0""?>
<package>
    <metadata>
    <id>Artem.XmlProviders  </id>
    <version>2.5</version>
    <title>Some awesome package       </title>
    <authors>Velio Ivanov</authors>
    <description>Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).</description>
    <language>en-US</language>
    <licenseUrl>http://somesite/somelicense.txt</licenseUrl>
    <requireLicenseAcceptance>true</requireLicenseAcceptance>
    <tags>t1      t2    foo-bar</tags>
    <copyright>David Fowler 2011</copyright>
  </metadata>
</package>";

            // Act
            PackageBuilder builder = new PackageBuilder(spec.AsStream(), null);
            var authors = builder.Authors.ToList();
            var owners = builder.Owners.ToList();
            var tags = builder.Tags.ToList();

            // Assert
            Assert.Equal("Artem.XmlProviders", builder.Id);
            Assert.Equal(new SemanticVersion(2, 5, 0, 0), builder.Version);
            Assert.Equal("Some awesome package", builder.Title);
            Assert.Equal(1, builder.Authors.Count);
            Assert.Equal("Velio Ivanov", authors[0]);
            Assert.Equal("Velio Ivanov", owners[0]);
            Assert.Equal(3, builder.Tags.Count);
            Assert.Equal("t1", tags[0]);
            Assert.Equal("t2", tags[1]);
            Assert.Equal("foo-bar", tags[2]);
            Assert.Equal("en-US", builder.Language);
            Assert.Equal("David Fowler 2011", builder.Copyright);
            Assert.Equal("Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).", builder.Description);
            Assert.Equal(new Uri("http://somesite/somelicense.txt"), builder.LicenseUrl);
            Assert.True(builder.RequireLicenseAcceptance);
        }

        [Fact]
        public void ReadingManifestWithSerializationNamespaceBuilderFromStreamCopiesMetadata()
        {
            // Arrange
            string spec = @"<?xml version=""1.0""?>
<package>
    <metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <title>Some awesome package</title>
    <authors>Velio Ivanov</authors>
    <owners>John Doe</owners>
    <description>Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).</description>
    <language>en-US</language>
    <licenseUrl>http://somesite/somelicense.txt</licenseUrl>
    <requireLicenseAcceptance>true</requireLicenseAcceptance>
  </metadata>
</package>";

            // Act
            PackageBuilder builder = new PackageBuilder(spec.AsStream(), null);
            var authors = builder.Authors.ToList();
            var owners = builder.Owners.ToList();

            // Assert
            Assert.Equal("Artem.XmlProviders", builder.Id);
            Assert.Equal(new SemanticVersion(2, 5, 0, 0), builder.Version);
            Assert.Equal("Some awesome package", builder.Title);
            Assert.Equal(1, builder.Authors.Count);
            Assert.Equal("Velio Ivanov", authors[0]);
            Assert.Equal(1, builder.Owners.Count);
            Assert.Equal("John Doe", owners[0]);
            Assert.Equal("en-US", builder.Language);
            Assert.Equal("Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).", builder.Description);
            Assert.Equal(new Uri("http://somesite/somelicense.txt"), builder.LicenseUrl);
            Assert.True(builder.RequireLicenseAcceptance);
        }

        [Fact]
        public void ReadingManifestWithOldStyleXmlnsDeclaratoinsFromStreamCopiesMetadata()
        {
            // Arrange
            string spec = @"<?xml version=""1.0""?>
<package xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
    <metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <title>Some awesome package</title>
    <authors>Velio Ivanov</authors>
    <owners>John Doe</owners>
    <description>Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).</description>
    <language>en-US</language>
    <licenseUrl>http://somesite/somelicense.txt</licenseUrl>
    <requireLicenseAcceptance>true</requireLicenseAcceptance>
  </metadata>
</package>";

            // Act
            PackageBuilder builder = new PackageBuilder(spec.AsStream(), null);
            var authors = builder.Authors.ToList();
            var owners = builder.Owners.ToList();

            // Assert
            Assert.Equal("Artem.XmlProviders", builder.Id);
            Assert.Equal(new SemanticVersion(2, 5, 0, 0), builder.Version);
            Assert.Equal("Some awesome package", builder.Title);
            Assert.Equal(1, builder.Authors.Count);
            Assert.Equal("Velio Ivanov", authors[0]);
            Assert.Equal(1, builder.Owners.Count);
            Assert.Equal("John Doe", owners[0]);
            Assert.Equal("en-US", builder.Language);
            Assert.Equal("Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).", builder.Description);
            Assert.Equal(new Uri("http://somesite/somelicense.txt"), builder.LicenseUrl);
            Assert.True(builder.RequireLicenseAcceptance);
        }

        [Fact]
        public void ReadingPackageManifestFromStreamCopiesMetadata()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <title>Some awesome package</title>
    <authors>Velio Ivanov</authors>
    <description>Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).</description>
    <language>en-US</language>
    <licenseUrl>http://somesite/somelicense.txt</licenseUrl>
    <requireLicenseAcceptance>true</requireLicenseAcceptance>
    <copyright>2010</copyright>
    <dependencies>
        <dependency id=""A"" version=""[1.0]"" />
        <dependency id=""B"" version=""[1.0, 2.5)"" />
    </dependencies>
  </metadata>
</package>";

            // Act
            PackageBuilder builder = new PackageBuilder(spec.AsStream(), null);
            var authors = builder.Authors.ToList();

            // Assert
            Assert.Equal("Artem.XmlProviders", builder.Id);
            Assert.Equal(new SemanticVersion(2, 5, 0, 0), builder.Version);
            Assert.Equal("Some awesome package", builder.Title);
            Assert.Equal(1, builder.Authors.Count);
            Assert.Equal("Velio Ivanov", authors[0]);
            Assert.Equal("en-US", builder.Language);
            Assert.Equal("2010", builder.Copyright);
            Assert.Equal("Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).", builder.Description);
            Assert.Equal(new Uri("http://somesite/somelicense.txt"), builder.LicenseUrl);
            Assert.True(builder.RequireLicenseAcceptance);

            Assert.Equal(1, builder.DependencySets.Count);
            var dependencySet = builder.DependencySets[0];

            IDictionary<string, IVersionSpec> dependencies = dependencySet.Dependencies.ToDictionary(p => p.Id, p => p.VersionSpec);
            // <dependency id="A" version="[1.0]" />
            Assert.True(dependencies["A"].IsMinInclusive);
            Assert.True(dependencies["A"].IsMaxInclusive);
            Assert.Equal(new SemanticVersion("1.0"), dependencies["A"].MinVersion);
            Assert.Equal(new SemanticVersion("1.0"), dependencies["A"].MaxVersion);
            // <dependency id="B" version="[1.0, 2.5)" />
            Assert.True(dependencies["B"].IsMinInclusive);
            Assert.False(dependencies["B"].IsMaxInclusive);
            Assert.Equal(new SemanticVersion("1.0"), dependencies["B"].MinVersion);
            Assert.Equal(new SemanticVersion("2.5"), dependencies["B"].MaxVersion);
        }

        [Fact]
        public void ReadingPackageManifestRecognizeDependencyWithTargetFramework()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>SuperPackage</id>
    <version>2.5</version>
    <authors>Luan</authors>
    <description>description</description>
    <dependencies>
        <group targetFramework=""sl4"">
            <dependency id=""A"" />
        </group>
    </dependencies>
  </metadata>
</package>";

            // Act
            PackageBuilder builder = new PackageBuilder(spec.AsStream(), null);

            // Assert
            Assert.Equal(1, builder.DependencySets.Count);
            var dependencySet = builder.DependencySets[0];

            Assert.Equal(new FrameworkName("Silverlight, Version=4.0"), dependencySet.TargetFramework);
            var dependencies = dependencySet.Dependencies.ToList();
            Assert.Equal(1, dependencies.Count);
            Assert.Equal("A", dependencies[0].Id);
            Assert.Null(dependencies[0].VersionSpec);
        }

        [Fact]
        public void ReadingPackageManifestRecognizeMultipleDependenciesWithTargetFramework()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>SuperPackage</id>
    <version>2.5</version>
    <authors>Luan</authors>
    <description>description</description>
    <dependencies>
        <group targetFramework=""sl4"">
            <dependency id=""A"" />
        </group>
        <group targetFramework=""net1"">
            <dependency id=""B"" />
            <dependency id=""C"" />
        </group>
        <group targetFramework=""net40-client"">
        </group>
    </dependencies>
  </metadata>
</package>";

            // Act
            PackageBuilder builder = new PackageBuilder(spec.AsStream(), null);

            // Assert
            Assert.Equal(3, builder.DependencySets.Count);
            var dependencySet1 = builder.DependencySets[0];
            var dependencySet2 = builder.DependencySets[1];
            var dependencySet3 = builder.DependencySets[2];

            Assert.Equal(new FrameworkName("Silverlight, Version=4.0"), dependencySet1.TargetFramework);
            var dependencies1 = dependencySet1.Dependencies.ToList();
            Assert.Equal(1, dependencies1.Count);
            Assert.Equal("A", dependencies1[0].Id);
            Assert.Null(dependencies1[0].VersionSpec);

            Assert.Equal(new FrameworkName(".NETFramework, Version=1.0"), dependencySet2.TargetFramework);
            var dependencies2 = dependencySet2.Dependencies.ToList();
            Assert.Equal(2, dependencies2.Count);
            Assert.Equal("B", dependencies2[0].Id);
            Assert.Null(dependencies2[0].VersionSpec);
            Assert.Equal("C", dependencies2[1].Id);
            Assert.Null(dependencies2[0].VersionSpec);

            Assert.Equal(new FrameworkName(".NETFramework, Version=4.0, Profile=Client"), dependencySet3.TargetFramework);
            Assert.False(dependencySet3.Dependencies.Any());
        }

        [Fact]
        public void PackageBuilderThrowsWhenDependenciesHasMixedDependencyAndGroupChildren()
        {
            // Arrange
            string spec =
                @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>SuperPackage</id>
    <version>2.5</version>
    <authors>Luan</authors>
    <description>description</description>
    <dependencies>
        <dependency id=""A"" />
        <group targetFramework=""net40-client"">
        </group>
    </dependencies>
  </metadata>
</package>";

            // Act
            ExceptionAssert.Throws<InvalidDataException>(
                () => { new PackageBuilder(spec.AsStream(), null); },
                "<dependencies> element must not contain both <group> and <dependency> child elements.");
        }

        [Fact]
        public void PackageBuilderThrowsWhenLicenseUrlIsPresentButEmpty()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <description>Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).</description>
    <language>en-US</language>
    <licenseUrl></licenseUrl>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
  </metadata>
</package>";

            // Act
            ExceptionAssert.Throws<ValidationException>(() => new PackageBuilder(spec.AsStream(), null), "LicenseUrl cannot be empty.");
        }

        [Fact]
        public void PackageBuilderThrowsWhenLicenseUrlIsWhiteSpace()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <description>Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).</description>
    <language>en-US</language>
    <licenseUrl>    </licenseUrl>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
  </metadata>
</package>";

            // Act
            ExceptionAssert.Throws<ValidationException>(() => new PackageBuilder(spec.AsStream(), null), "LicenseUrl cannot be empty.");
        }

        [Fact]
        public void ValidateReferencesAllowsPartialFileNames()
        {
            // Arrange
            var files = new[] {
                new PhysicalPackageFile { TargetPath = @"lib\net40\foo.dll" },
                new PhysicalPackageFile { TargetPath = @"lib\net40\bar.dll" },
                new PhysicalPackageFile { TargetPath = @"lib\net40\baz.exe" },
            };
            var packageAssemblyReferences = new PackageReferenceSet(null, new string[] { "foo.dll", "bar", "baz" });

            // Act and Assert
            PackageBuilder.ValidateReferenceAssemblies(files, new[] { packageAssemblyReferences });

            // If we've got this far, no exceptions were thrown.
            Assert.True(true);
        }

        [Fact]
        public void ValidateReferencesThrowsForPartialNamesThatDoNotHaveAKnownExtension()
        {
            // Arrange
            var files = new[] {
                new PhysicalPackageFile { TargetPath = @"lib\net20\foo.dll" },
                new PhysicalPackageFile { TargetPath = @"lib\net20\bar.dll" },
                new PhysicalPackageFile { TargetPath = @"lib\net20\baz.qux" },
            };
            var packageAssemblyReferences = new PackageReferenceSet(new FrameworkName("Silverlight, Version=1.0"), new string[] { "foo.dll", "bar", "baz" });

            // Act and Assert
            ExceptionAssert.Throws<InvalidDataException>(() => PackageBuilder.ValidateReferenceAssemblies(files, new [] { packageAssemblyReferences }),
                "Invalid assembly reference 'baz'. Ensure that a file named 'baz' exists in the lib directory.");
        }

        [Theory]
        [PropertyData("InvalidDependencyData")]
        public void ValidateDependenciesThrowsIfAnyDependencyForAStableReleaseIsPrerelease(VersionSpec versionSpec)
        {
            // Arrange
            var badDependency = new PackageDependency("A", versionSpec);
            var dependencies = new[] {
                badDependency,
                new PackageDependency("B", new VersionSpec()),
            };
            var packageVersion = new SemanticVersion("1.0.0");

            var dependencySets = new PackageDependencySet[] {
                new PackageDependencySet(null, dependencies)
            };

            // Act and Assert
            ExceptionAssert.Throws<InvalidDataException>(() => PackageBuilder.ValidateDependencySets(packageVersion, dependencySets),
                String.Format(CultureInfo.InvariantCulture,
                    "A stable release of a package should not have on a prerelease dependency. Either modify the version spec of dependency \"{0}\" or update the version field.",
                    badDependency));
        }

        [Theory]
        [PropertyData("InvalidDependencyData")]
        public void ValidateDependenciesDoesNotThrowIfDependencyForAPrereleaseVersionIsPrerelease(VersionSpec versionSpec)
        {
            // Arrange
            var dependencies = new[] {
                new PackageDependency("A", versionSpec),
                new PackageDependency("B", new VersionSpec()),
            };
            var packageVersion = new SemanticVersion("1.0.0-beta");

            var dependencySets = new PackageDependencySet[] {
                new PackageDependencySet(null, dependencies)
            };

            // Act
            PackageBuilder.ValidateDependencySets(packageVersion, dependencySets);

            // Assert
            // If we've got this far, no exceptions were thrown.
            Assert.True(true);
        }

        [Fact]
        public void ValidateDependenciesDoesNotThrowIfDependencyForAStableVersionIsStable()
        {
            // Arrange
            var dependencies = new[] {
                new PackageDependency("A", new VersionSpec(new SemanticVersion("1.0.0"))),
                new PackageDependency("B", new VersionSpec { MinVersion = new SemanticVersion("1.0.1"), MaxVersion = new SemanticVersion("1.2.3") }),
            };
            var packageVersion = new SemanticVersion("1.0.0");

            var dependencySets = new PackageDependencySet[] {
                new PackageDependencySet(null, dependencies)
            };

            // Act
            PackageBuilder.ValidateDependencySets(packageVersion, dependencySets);

            // Assert
            // If we've got this far, no exceptions were thrown.
            Assert.True(true);
        }

        public static IEnumerable<object[]> InvalidDependencyData
        {
            get
            {
                var prereleaseVer = new SemanticVersion("1.0.0-a");
                var version = new SemanticVersion("2.3.0.6232");

                yield return new object[] { new VersionSpec(prereleaseVer) };
                yield return new object[] { new VersionSpec { MinVersion = prereleaseVer, MaxVersion = version } };
                yield return new object[] { new VersionSpec { MinVersion = version, MaxVersion = prereleaseVer, IsMaxInclusive = true } };
                yield return new object[] { new VersionSpec { MinVersion = prereleaseVer, MaxVersion = prereleaseVer, IsMinInclusive = true } };
            }
        }

        [Fact]
        public void PackageBuilderRequireLicenseAcceptedWithoutLicenseUrlThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <description>Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).</description>
    <language>en-US</language>
    <licenseUrl></licenseUrl>
    <requireLicenseAcceptance>true</requireLicenseAcceptance>
  </metadata>
</package>";

            // Act
            ExceptionAssert.Throws<ValidationException>(() => new PackageBuilder(spec.AsStream(), null), @"LicenseUrl cannot be empty.
Enabling license acceptance requires a license url.");
        }

        [Fact]
        public void PackageBuilderThrowsWhenLicenseUrlIsMalformed()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <description>Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).</description>
    <language>en-US</language>
    <licenseUrl>this-is-a-malformed-url</licenseUrl>
    <requireLicenseAcceptance>true</requireLicenseAcceptance>
  </metadata>
</package>";

            // Switch to invariant culture to ensure the error message is in english.
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            // Act
            ExceptionAssert.Throws<UriFormatException>(() => new PackageBuilder(spec.AsStream(), null), "Invalid URI: The format of the URI could not be determined.");
        }

        [Fact]
        public void PackageBuilderThrowsIfPackageIdInvalid()
        {
            // Arrange
            var builder = new PackageBuilder
            {
                Id = "  a.  b",
                Version = new SemanticVersion("1.0"),
                Description = "Description"
            };
            builder.Authors.Add("Me");

            // Act & Assert            
            ExceptionAssert.ThrowsArgumentException(() => builder.Save(new MemoryStream()), "The package ID '  a.  b' contains invalid characters. Examples of valid package IDs include 'MyPackage' and 'MyPackage.Sample'.");
        }

        [Fact]
        public void PackageBuilderThrowsIfPackageIdExceedsMaxLengthLimit()
        {
            // Arrange
            var builder = new PackageBuilder
            {
                Id = new string('c', 101),
                Version = new SemanticVersion("1.0"),
                Description = "Description"
            };
            builder.Authors.Add("Me");

            // Act & Assert            
            ExceptionAssert.ThrowsArgumentException(() => builder.Save(new MemoryStream()), "Id must not exceed 100 characters.");
        }

        [Fact]
        public void PackageBuilderThrowsIfSpecialVersionExceedsMaxLengthLimit()
        {
            // Arrange
            var builder = new PackageBuilder
            {
                Id = "cool",
                Version = new SemanticVersion("1.0-vvvvvvvvvvvvvvvvvvvvK"),
                Description = "Description"
            };
            builder.Authors.Add("Me");

            var dependencies = new PackageDependency[] {
                new PackageDependency("X")
            };
            builder.DependencySets.Add(new PackageDependencySet(null, dependencies));

            // Act & Assert            
            ExceptionAssert.Throws<InvalidOperationException>(() => builder.Save(new MemoryStream()), "The special version part cannot exceed 20 characters.");
        }

        [Fact]
        public void ReadingPackageWithUnknownSchemaThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2011/03/nuspec.xsd"">
  <metadata>
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <description>Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).</description>
    <language>en-US</language>
  </metadata>
</package>";

            // Act & Assert
            ExceptionAssert.Throws<InvalidOperationException>(() => new PackageBuilder(spec.AsStream(), null), "The schema version of 'Artem.XmlProviders' is incompatible with version " + typeof(Manifest).Assembly.GetName().Version + " of NuGet. Please upgrade NuGet to the latest version from http://go.microsoft.com/fwlink/?LinkId=213942.");
        }

        [Fact]
        public void ReadingPackageWithUnknownSchemaAndMissingIdThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2011/03/nuspec.xsd"">
  <metadata>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <description>Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).</description>
    <language>en-US</language>
  </metadata>
</package>";

            // Act & Assert
            ExceptionAssert.Throws<InvalidOperationException>(() => new PackageBuilder(spec.AsStream(), null), "The schema version of '' is incompatible with version " + typeof(Manifest).Assembly.GetName().Version + " of NuGet. Please upgrade NuGet to the latest version from http://go.microsoft.com/fwlink/?LinkId=213942.");
        }

        [Fact]
        public void ReadingPackageWithSchemaWithOlderVersionAttribute()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata schemaVersion=""2.0"">
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <description>Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).</description>
    <language>en-US</language>
  </metadata>
</package>";

            // Act
            var packageBuilder = new PackageBuilder(spec.AsStream(), null);

            // Assert
            Assert.Equal("Artem.XmlProviders", packageBuilder.Id);
            Assert.Equal(new SemanticVersion("2.5"), packageBuilder.Version);
            Assert.Equal("Velio Ivanov", packageBuilder.Authors.Single());
            Assert.Equal("Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).", packageBuilder.Description);
            Assert.Equal("en-US", packageBuilder.Language);
        }

        [Fact]
        public void ReadingPackageWithSchemaVersionAttribute()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata schemaVersion=""3.0"">
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <description>Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).</description>
    <language>en-US</language>
    <references>
        <reference file=""foo.dll"" />
    </references>
  </metadata>
</package>";

            // Act
            var packageBuilder = new PackageBuilder(spec.AsStream(), null);

            // Assert
            Assert.Equal("Artem.XmlProviders", packageBuilder.Id);
            Assert.Equal(new SemanticVersion("2.5"), packageBuilder.Version);
            Assert.Equal("Velio Ivanov", packageBuilder.Authors.Single());
            Assert.Equal("Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).", packageBuilder.Description);
            Assert.Equal("en-US", packageBuilder.Language);

            var packageReferenceSet = packageBuilder.PackageAssemblyReferences.Single();
            Assert.Null(packageReferenceSet.TargetFramework);
            Assert.Equal("foo.dll", packageReferenceSet.References.Single());
        }

        [Fact]
        public void ReadingPackageWithSchemaVersionAttributeWithNamespace()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>
  <metadata schemaVersion=""2.0"">
    <id>Artem.XmlProviders</id>
    <version>2.5</version>
    <authors>Velio Ivanov</authors>
    <description>Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).</description>
    <language>en-US</language>
  </metadata>
</package>";

            // Act
            var packageBuilder = new PackageBuilder(spec.AsStream(), null);

            // Assert
            Assert.Equal("Artem.XmlProviders", packageBuilder.Id);
            Assert.Equal(new SemanticVersion("2.5"), packageBuilder.Version);
            Assert.Equal("Velio Ivanov", packageBuilder.Authors.Single());
            Assert.Equal("Implementation of XML ASP.NET Providers (XmlRoleProvider, XmlMembershipProvider and XmlProfileProvider).", packageBuilder.Description);
            Assert.Equal("en-US", packageBuilder.Language);
        }

        [Fact]
        public void MissingMetadataNodeThrows()
        {
            // Arrange
            string spec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package>  
</package>";

            // Switch to invariant culture to ensure the error message is in english.
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            // Act
            ExceptionAssert.Throws<InvalidOperationException>(() => new PackageBuilder(spec.AsStream(), null), "The element 'package' in namespace 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd' has incomplete content. List of possible elements expected: 'metadata' in namespace 'http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd'.");
        }

        [Fact]
        public void PackageBuilderWorksWithFileNamesContainingSpecialCharacters()
        {
            // Arrange
            var fileNames = new[] {
                @"lib\regular.file.dll",
                @"lib\name with spaces.dll",
                @"lib\C#\test.dll",
                @"content\images\logo123?#78.png",
                @"content\images\bread&butter.jpg",
            };

            // Act
            var builder = new PackageBuilder { Id = "test", Version = new SemanticVersion("1.0"), Description = "test" };
            builder.Authors.Add("test");
            foreach (var name in fileNames)
            {
                builder.Files.Add(CreatePackageFile(name));
            }

            // Assert
            using (MemoryStream stream = new MemoryStream())
            {
                builder.Save(stream);

                var zipPackage = new ZipPackage(() => new MemoryStream(stream.ToArray()), enableCaching: false);
                Assert.Equal(@"content\images\bread&butter.jpg", zipPackage.GetFiles().ElementAt(0).Path);
                Assert.Equal(@"content\images\logo123?#78.png", zipPackage.GetFiles().ElementAt(1).Path);
                Assert.Equal(@"lib\C#\test.dll", zipPackage.GetFiles().ElementAt(2).Path);
                Assert.Equal(@"lib\name with spaces.dll", zipPackage.GetFiles().ElementAt(3).Path);
                Assert.Equal(@"lib\regular.file.dll", zipPackage.GetFiles().ElementAt(4).Path);
            }
        }

        private static IPackageFile CreatePackageFile(string name)
        {
            var file = new Mock<IPackageFile>();
            file.SetupGet(f => f.Path).Returns(name);
            file.Setup(f => f.GetStream()).Returns(new MemoryStream());

            string effectivePath;
            var fx = VersionUtility.ParseFrameworkNameFromFilePath(name, out effectivePath);
            file.SetupGet(f => f.EffectivePath).Returns(effectivePath);
            file.SetupGet(f => f.TargetFramework).Returns(fx);

            return file.Object;
        }

        private Stream GetManifestStream(Stream packageStream)
        {
            Package package = Package.Open(packageStream);
            PackageRelationship relationshipType = package.GetRelationshipsByType(Constants.PackageRelationshipNamespace + PackageBuilder.ManifestRelationType).SingleOrDefault();
            PackagePart manifestPart = package.GetPart(relationshipType.TargetUri);
            return manifestPart.GetStream();
        }
    }
}