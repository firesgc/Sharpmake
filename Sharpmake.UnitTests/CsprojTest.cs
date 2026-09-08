// Copyright (c) Ubisoft. All Rights Reserved.
// Licensed under the Apache 2.0 License. See LICENSE.md in the project root for license information.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sharpmake.Generators.VisualStudio;

namespace Sharpmake.UnitTests
{
    [TestFixture]
    public class EnableDefaultItemsFlagsTest
    {
        // Mirrors the sdkHandles* derivation in Csproj.cs so the unit tests stay
        // in sync with the generator without depending on a full generation run.
        private static (bool compile, bool embedded, bool none, bool page) ComputeFlags(
            CSharpProject p, bool isNetCoreSdk)
        {
            bool sdkBase = isNetCoreSdk && p.EnableDefaultItems;
            return (
                sdkBase && (p.EnableDefaultCompileItems          ?? true),
                sdkBase && (p.EnableDefaultEmbeddedResourceItems ?? true),
                sdkBase && (p.EnableDefaultNoneItems             ?? true),
                sdkBase && (p.EnableDefaultPageItems             ?? true)
            );
        }

        [Test]
        public void AllPropertiesDefaultToNull()
        {
            var p = new CSharpProject();
            Assert.That(p.EnableDefaultCompileItems,          Is.Null);
            Assert.That(p.EnableDefaultEmbeddedResourceItems, Is.Null);
            Assert.That(p.EnableDefaultNoneItems,             Is.Null);
            Assert.That(p.EnableDefaultPageItems,             Is.Null);
        }

        [Test]
        public void EnableDefaultItemsDefaultsToFalse()
        {
            Assert.That(new CSharpProject().EnableDefaultItems, Is.False);
        }

        [Test]
        public void MasterOffMeansAllFlagsAreFalse()
        {
            var p = new CSharpProject { EnableDefaultItems = false };
            // Fine-grained null (SDK default = true) is irrelevant when master is off.
            var (compile, embedded, none, page) = ComputeFlags(p, isNetCoreSdk: true);
            Assert.That(compile,  Is.False);
            Assert.That(embedded, Is.False);
            Assert.That(none,     Is.False);
            Assert.That(page,     Is.False);
        }

        [Test]
        public void NonSdkProjectMeansAllFlagsAreFalse()
        {
            var p = new CSharpProject { EnableDefaultItems = true };
            var (compile, embedded, none, page) = ComputeFlags(p, isNetCoreSdk: false);
            Assert.That(compile,  Is.False);
            Assert.That(embedded, Is.False);
            Assert.That(none,     Is.False);
            Assert.That(page,     Is.False);
        }

        [Test]
        public void AllNullMeansAllActiveWhenMasterOn()
        {
            // null on each fine-grained prop = "use SDK default" = treat as true
            var p = new CSharpProject { EnableDefaultItems = true };
            var (compile, embedded, none, page) = ComputeFlags(p, isNetCoreSdk: true);
            Assert.That(compile,  Is.True);
            Assert.That(embedded, Is.True);
            Assert.That(none,     Is.True);
            Assert.That(page,     Is.True);
        }

        [Test]
        public void FalseOnOneTypeDisablesOnlyThatType()
        {
            var p = new CSharpProject
            {
                EnableDefaultItems = true,
                EnableDefaultCompileItems = false  // only compile suppressed
            };
            var (compile, embedded, none, page) = ComputeFlags(p, isNetCoreSdk: true);
            Assert.That(compile,  Is.False, "compile should be suppressed");
            Assert.That(embedded, Is.True,  "embedded should still be active");
            Assert.That(none,     Is.True,  "none should still be active");
            Assert.That(page,     Is.True,  "page should still be active");
        }

        [Test]
        public void TrueExplicitlyMatchesNullBehaviour()
        {
            // Explicit true == null when master is on: both mean SDK owns discovery
            var pNull  = new CSharpProject { EnableDefaultItems = true, EnableDefaultNoneItems = null };
            var pTrue  = new CSharpProject { EnableDefaultItems = true, EnableDefaultNoneItems = true };
            var pFalse = new CSharpProject { EnableDefaultItems = true, EnableDefaultNoneItems = false };

            var (_, _, noneNull,  _) = ComputeFlags(pNull,  isNetCoreSdk: true);
            var (_, _, noneTrue,  _) = ComputeFlags(pTrue,  isNetCoreSdk: true);
            var (_, _, noneFalse, _) = ComputeFlags(pFalse, isNetCoreSdk: true);

            Assert.That(noneNull,  Is.True);
            Assert.That(noneTrue,  Is.True);
            Assert.That(noneFalse, Is.False);
        }
    }

    [TestFixture]
    public class CsprojTest
    {
        [TestFixture]
        public class GetProjectLinkedFolder
        {
            [Test]
            public void FileUnderSourceRootPath()
            {
                var filePath = "..\\..\\codebase\\helloworld\\program.cs";
                var projectPath = "d:\\git\\sharpmake\\sharpmake\\samples\\csharphelloworld\\projects\\helloworld";
                var sourceRootPath = "d:\\git\\sharpmake\\sharpmake\\samples\\csharphelloworld\\codebase\\helloworld";

                var project = new Project() { SourceRootPath = sourceRootPath };

                var result = CSproj.GetProjectLinkedFolder(filePath, projectPath, project);

                Assert.That(result, Is.EqualTo(""));
            }

            [Test]
            public void FileUnderRootPath()
            {
                var filePath = "..\\..\\codebase\\helloworld\\program.cs";
                var projectPath = "d:\\git\\sharpmake\\sharpmake\\samples\\csharphelloworld\\projects\\helloworld";
                var sourceRootPath = "d:\\git\\sharpmake\\sharpmake\\samples\\csharphelloworld\\source\\helloworld";
                var rootPath = "d:\\git\\sharpmake\\sharpmake\\samples\\csharphelloworld\\codebase\\helloworld";

                var project = new Project() { SourceRootPath = sourceRootPath, RootPath = rootPath };

                var result = CSproj.GetProjectLinkedFolder(filePath, projectPath, project);

                Assert.That(result, Is.EqualTo(""));
            }

            [Test]
            public void RootAndSourcePathCorrectOrder()
            {
                var filePath = "..\\..\\codebase\\helloworld\\program.cs";
                var projectPath = "d:\\git\\sharpmake\\sharpmake\\samples\\csharphelloworld\\projects\\helloworld";
                var sourceRootPath = "d:\\git\\sharpmake\\sharpmake\\samples\\csharphelloworld\\codebase\\helloworld";
                var rootPath = "d:\\git\\sharpmake\\sharpmake\\samples\\csharphelloworld";

                var project = new Project() { SourceRootPath = sourceRootPath, RootPath = rootPath };

                var result = CSproj.GetProjectLinkedFolder(filePath, projectPath, project);

                Assert.That(result, Is.Not.EqualTo("codebase\\helloworld"));
            }

            [Test]
            public void FileUnderProjectPath()
            {
                var filePath = "d:\\git\\sharpmake\\sharpmake\\samples\\csharphelloworld\\projects\\helloworld\\program.cs";
                var projectPath = "d:\\git\\sharpmake\\sharpmake\\samples\\csharphelloworld\\projects\\helloworld";
                var sourceRootPath = "d:\\git\\sharpmake\\sharpmake\\samples\\csharphelloworld\\codebase\\helloworld";
                var rootPath = "d:\\git\\sharpmake\\sharpmake\\samples\\csharphelloworld\\projects\\helloworld";

                var project = new Project() { SourceRootPath = sourceRootPath, RootPath = rootPath };

                var result = CSproj.GetProjectLinkedFolder(filePath, projectPath, project);

                Assert.That(result, Is.Null);
            }

            [Test]
            public void AbsoluteFilePath()
            {
                var filePath = "c:\\.nuget\\dd\\llvm\\build\\native\\llvm.sharpmake.cs";
                var projectPath = "d:\\git\\sharpmake\\sharpmake\\samples\\csharphelloworld\\projects\\helloworld";
                var sourceRootPath = "d:\\git\\sharpmake\\sharpmake\\samples\\csharphelloworld\\codebase\\helloworld";
                var rootPath = "d:\\git\\sharpmake\\sharpmake\\samples\\csharphelloworld\\projects\\helloworld";

                var project = new Project() { SourceRootPath = sourceRootPath, RootPath = rootPath };

                var result = CSproj.GetProjectLinkedFolder(filePath, projectPath, project);

                Assert.That(result, Is.EqualTo(".nuget\\dd\\llvm\\build\\native"));
            }

            [Test]
            public void RelativePathFileOutsideProject()
            {
                var filePath = "..\\..\\..\\..\\code\\platform\\standalone.main.sharpmake.cs";
                var projectPath =       "d:\\versioncontrol\\workspace\\generated\\platform\\sharpmake\\debugsolution";
                var sourceRootPath =    "d:\\versioncontrol\\workspace\\generated\\platform\\sharpmake\\debugsolution";
                var rootPath =          "d:\\versioncontrol\\workspace\\generated\\platform\\sharpmake\\debugsolution";

                var project = new Project() { SourceRootPath = sourceRootPath, RootPath = rootPath };

                var result = CSproj.GetProjectLinkedFolder(filePath, projectPath, project);

                Assert.That(result, Is.EqualTo("code\\platform"));
            }

            [Test]
            public void AbsolutePathFileInProjectFolder()
            {
                var filePath = "d:\\git\\sharpmake\\sharpmake\\samples\\csharphelloworld\\projects\\helloworld\\program.cs";
                var projectPath = "d:\\git\\sharpmake\\sharpmake\\samples\\csharphelloworld\\projects\\helloworld";
                var sourceRootPath = "d:\\git\\sharpmake\\sharpmake\\samples\\csharphelloworld\\codebase\\helloworld";

                var project = new Project() { SourceRootPath = sourceRootPath, RootPath = sourceRootPath };

                var result = CSproj.GetProjectLinkedFolder(filePath, projectPath, project);

                Assert.That(result, Is.Null);
            }

            [Test]
            public void RelativePathFileInProjectFolder()
            {
                var filePath = "..\\helloworld\\program.cs";
                var projectPath = "d:\\git\\sharpmake\\sharpmake\\samples\\csharphelloworld\\projects\\helloworld";
                var sourceRootPath = "d:\\git\\sharpmake\\sharpmake\\samples\\csharphelloworld\\codebase\\helloworld";

                var project = new Project() { SourceRootPath = sourceRootPath, RootPath = sourceRootPath };

                var result = CSproj.GetProjectLinkedFolder(filePath, projectPath, project);

                Assert.That(result, Is.Null);
            }

            [Test]
            public void CasingUnchanged()
            {
                var filePathLowerCase = "..\\..\\codebase\\helloworld\\program.cs";
                var projectPathLowerCase = "D:\\Git\\Sharpmake\\sharpmake\\samples\\CSharpHelloWorld\\projects\\helloworld";
                var sourceRootPathLowerCase = "d:\\git\\sharpmake\\sharpmake\\samples\\csharphelloworld\\codebase\\";

                var filePathCamelCase = "..\\..\\CodeBase\\HelloWorld\\Program.cs";
                var projectPathCamelCase = "D:\\Git\\Sharpmake\\Sharpmake\\Samples\\CSharpHelloWorld\\Projects\\HelloWorld";
                var sourceRootPathCamelCase = "D:\\Git\\Sharpmake\\Sharpmake\\Samples\\CSharpHelloWorld\\Codebase\\";

                var projectLowerCase = new Project() { SourceRootPath = sourceRootPathLowerCase };
                var result = CSproj.GetProjectLinkedFolder(filePathLowerCase, projectPathLowerCase, projectLowerCase);

                Assert.That(string.Equals("helloworld", result, System.StringComparison.Ordinal), Is.True);
                Assert.That(string.Equals("HelloWorld", result, System.StringComparison.Ordinal), Is.False);

                var projectCamelCase = new Project() { SourceRootPath = sourceRootPathCamelCase };
                result = CSproj.GetProjectLinkedFolder(filePathCamelCase, projectPathCamelCase, projectCamelCase);

                Assert.That(string.Equals("HelloWorld", result, System.StringComparison.Ordinal), Is.True);
                Assert.That(string.Equals("helloworld", result, System.StringComparison.Ordinal), Is.False);
            }
        }
    }

    [TestFixture]
    public class CsprojProjectPropertiesTest : CSharpTestProjectBuilder
    {
        public CsprojProjectPropertiesTest()
            : base(typeof(CsprojProjectPropertiesTestProjects.DefaultPropertiesProject).Namespace)
        {
        }

        [Test]
        public void AppDesignerFolderDefaultValue()
        {
            var project = GetProject<CsprojProjectPropertiesTestProjects.DefaultPropertiesProject>() as CSharpProject;
            Assert.That(project, Is.Not.Null);
            Assert.That(project.AppDesignerFolder, Is.EqualTo("Properties"));
        }

        [Test]
        public void AppDesignerFolderCustomValue()
        {
            var project = GetProject<CsprojProjectPropertiesTestProjects.CustomAppDesignerFolderProject>() as CSharpProject;
            Assert.That(project, Is.Not.Null);
            Assert.That(project.AppDesignerFolder, Is.EqualTo("MyProperties"));
        }

        [Test]
        public void AppDesignerFolderSuppressed()
        {
            var project = GetProject<CsprojProjectPropertiesTestProjects.NoAppDesignerFolderProject>() as CSharpProject;
            Assert.That(project, Is.Not.Null);
            Assert.That(project.AppDesignerFolder, Is.EqualTo(string.Empty));
        }

        [Test]
        public void IntermediatePathDefaultNotNull()
        {
            var project = GetProject<CsprojProjectPropertiesTestProjects.DefaultPropertiesProject>();
            Assert.That(project, Is.Not.Null);
            foreach (var conf in project.Configurations)
                Assert.That(conf.IntermediatePath, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void IntermediatePathNullAllowedAndResolvesSafely()
        {
            var project = GetProject<CsprojProjectPropertiesTestProjects.NullIntermediatePathProject>();
            Assert.That(project, Is.Not.Null);
            foreach (var conf in project.Configurations)
                Assert.That(conf.IntermediatePath, Is.Null);
        }
    }

    [TestFixture]
    public class ProjectReferenceGuidTest
    {
        private static string Resolve(CSproj.ItemGroups.ProjectReference pr)
            => pr.Resolve(new Resolver());

        [Test]
        public void GuidAndNamePresentForNonSdkProject()
        {
            var guid = Guid.NewGuid();
            var pr = new CSproj.ItemGroups.ProjectReference
            {
                Include = "Foo.csproj",
                Project = guid,
                Name = "Foo",
                IsNetCore = false,
            };
            var xml = Resolve(pr);
            Assert.That(xml, Does.Contain($"<Project>{guid:B}</Project>"));
            Assert.That(xml, Does.Contain("<Name>Foo</Name>"));
        }

        [Test]
        public void GuidAbsentForSdkProjectByDefault()
        {
            var guid = Guid.NewGuid();
            var pr = new CSproj.ItemGroups.ProjectReference
            {
                Include = "Bar.csproj",
                Project = guid,
                Name = "Bar",
                IsNetCore = true,
                ForceGuid = false,
            };
            var xml = Resolve(pr);
            Assert.That(xml, Does.Not.Contain("<Project>"));
            Assert.That(xml, Does.Not.Contain("<Name>"));
        }

        [Test]
        public void GuidPresentForSdkProjectWhenForceGuidTrue()
        {
            var guid = Guid.NewGuid();
            var pr = new CSproj.ItemGroups.ProjectReference
            {
                Include = "Bar.csproj",
                Project = guid,
                Name = "Bar",
                IsNetCore = true,
                ForceGuid = true,
            };
            var xml = Resolve(pr);
            Assert.That(xml, Does.Contain($"<Project>{guid:B}</Project>"));
            Assert.That(xml, Does.Not.Contain("<Name>"));
        }
    }

    namespace CsprojProjectPropertiesTestProjects
    {
        public abstract class CsprojPropertyTestBaseProject : CSharpProject
        {
            public CsprojPropertyTestBaseProject()
                : base(typeof(Target))
            {
                IsFileNameToLower = false;
                SourceRootPath = Directory.GetCurrentDirectory() + "/[project.Name]";
                AddTargets(new Target(Platform.anycpu, DevEnv.vs2019, Optimization.Debug));
            }

            [ConfigurePriority(-100)]
            [Configure]
            public virtual void Configure(Configuration conf, Target target)
            {
                conf.Output = Configuration.OutputType.DotNetClassLibrary;
            }
        }

        [Generate]
        public class DefaultPropertiesProject : CsprojPropertyTestBaseProject
        {
            public DefaultPropertiesProject() { }
        }

        [Generate]
        public class CustomAppDesignerFolderProject : CsprojPropertyTestBaseProject
        {
            public CustomAppDesignerFolderProject()
            {
                AppDesignerFolder = "MyProperties";
            }
        }

        [Generate]
        public class NoAppDesignerFolderProject : CsprojPropertyTestBaseProject
        {
            public NoAppDesignerFolderProject()
            {
                AppDesignerFolder = string.Empty;
            }
        }

        [Generate]
        public class NullIntermediatePathProject : CsprojPropertyTestBaseProject
        {
            public NullIntermediatePathProject() { }

            [Configure]
            public override void Configure(Configuration conf, Target target)
            {
                base.Configure(conf, target);
                conf.IntermediatePath = null;
            }
        }
    }
}
