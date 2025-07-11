// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.Json;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Moq;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests
{
    public class ComputeStaticWebAssetsForCurrentProjectTest
    {
        [Fact]
        public void IncludesAssetsFromCurrentProject()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeStaticWebAssetsForCurrentProject
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate("wwwroot\\candidate.js", "MyPackage", "Discovered", "candidate.js", "All", "All") },
                AssetKind = "Build",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(1);
        }

        [Fact]
        public void PrefersSpecificKindAssetsOverAllKindAssets()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeStaticWebAssetsForCurrentProject
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[]
                {
                    CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", "Discovered", "candidate.js", "All", "All"),
                    CreateCandidate(Path.Combine("wwwroot", "candidate.other.js"), "MyPackage", "Discovered", "candidate.js", "Build", "All")
                },
                AssetKind = "Build",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(1);
            task.StaticWebAssets[0].ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.other.js")));
        }

        [Fact]
        public void AllAssetGetsIgnoredWhenBuildAndPublishAssetsAreDefined()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeStaticWebAssetsForCurrentProject
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[]
                {
                    CreateCandidate(Path.Combine("wwwroot", "candidate.js"), "MyPackage", "Discovered", "candidate.js", "All", "All"),
                    CreateCandidate(Path.Combine("wwwroot", "candidate.other.js"), "MyPackage", "Discovered", "candidate.js", "Build", "All"),
                    CreateCandidate(Path.Combine("wwwroot", "candidate.publish.js"), "MyPackage", "Discovered", "candidate.js", "Publish", "All")
                },
                AssetKind = "Build",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(1);
            task.StaticWebAssets[0].ItemSpec.Should().Be(Path.GetFullPath(Path.Combine("wwwroot", "candidate.other.js")));
        }

        [Theory]
        [InlineData("Build", "Publish")]
        [InlineData("Publish", "Build")]
        public void FiltersAssetsForOppositeKind(string assetKind, string manifestKind)
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeStaticWebAssetsForCurrentProject
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate("wwwroot\\candidate.js", "MyPackage", "Discovered", "candidate.js", assetKind, "All") },
                AssetKind = manifestKind,
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(0);
        }

        [Fact]
        public void IncludesCurrentProjectOnlyAssetsInDefaultMode()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeStaticWebAssetsForCurrentProject
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate("wwwroot\\candidate.js", "MyPackage", "Discovered", "candidate.js", "All", "CurrentProject") },
                AssetKind = "Default",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(1);
        }

        [Fact]
        public void FiltersReferenceAssetsInDefaultMode()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeStaticWebAssetsForCurrentProject
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate("wwwroot\\candidate.js", "MyPackage", "Discovered", "candidate.js", "All", "Reference") },
                AssetKind = "Default",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(0);
        }

        [Fact]
        public void IncludesCurrentProjectAssetsInRootMode()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeStaticWebAssetsForCurrentProject
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate("wwwroot\\candidate.js", "MyPackage", "Discovered", "candidate.js", "All", "CurrentProject") },
                AssetKind = "Default",
                ProjectMode = "Root"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(1);
        }

        [Fact]
        public void FiltersReferenceOnlyAssetsInRootMode()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeStaticWebAssetsForCurrentProject
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate("wwwroot\\candidate.js", "MyPackage", "Discovered", "candidate.js", "All", "Reference") },
                AssetKind = "Default",
                ProjectMode = "Root"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(0);
        }

        [Fact]
        public void IncludesAssetsFromOtherProjects()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeStaticWebAssetsForCurrentProject
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate("wwwroot\\candidate.js", "Other", "Project", "candidate.js", "All", "All") },
                AssetKind = "Build",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(1);
        }

        [Fact]
        public void IncludesAssetsFromPackages()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var task = new ComputeStaticWebAssetsForCurrentProject
            {
                BuildEngine = buildEngine.Object,
                Source = "MyPackage",
                Assets = new[] { CreateCandidate("wwwroot\\candidate.js", "Other", "Package", "candidate.js", "All", "All") },
                AssetKind = "Build",
                ProjectMode = "Default"
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.StaticWebAssets.Should().HaveCount(1);
        }

        private static ITaskItem CreateCandidate(
            string itemSpec,
            string sourceId,
            string sourceType,
            string relativePath,
            string assetKind,
            string assetMode)
        {
            var result = new StaticWebAsset()
            {
                Identity = Path.GetFullPath(itemSpec),
                SourceId = sourceId,
                SourceType = sourceType,
                ContentRoot = Directory.GetCurrentDirectory(),
                BasePath = "base",
                RelativePath = relativePath,
                AssetKind = assetKind,
                AssetMode = assetMode,
                AssetRole = "Primary",
                RelatedAsset = "",
                AssetTraitName = "",
                AssetTraitValue = "",
                CopyToOutputDirectory = "",
                CopyToPublishDirectory = "",
                OriginalItemSpec = itemSpec,
                // Add these to avoid accessing the disk to compute them
                Integrity = "integrity",
                Fingerprint = "fingerprint",
                LastWriteTime = DateTime.UtcNow,
                FileLength = 10,
            };

            result.ApplyDefaults();
            result.Normalize();

            return result.ToTaskItem();
        }
    }
}
