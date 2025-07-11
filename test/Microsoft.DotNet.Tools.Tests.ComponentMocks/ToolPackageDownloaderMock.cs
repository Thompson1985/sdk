﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Microsoft.DotNet.Tools.Tests.ComponentMocks
{
    //  This class should be superceded by ToolPackageDownloaderMock2.  The Mock2 version derives from ToolPackageDownloaderBase, so a lot less
    //  business logic needs to be duplicated.
    //  The class here on the other hand has a lot of code/behavior copied from the product and they could get out of sync.
    //  So ideally we should migrate existing tests that use this class to the Mock2 version, then delete this version and rename the Mock2 version
    //  to ToolPackageDownlnoaderMock.
    internal class ToolPackageDownloaderMock : IToolPackageDownloader
    {
        private readonly IToolPackageStore _toolPackageStore;

        protected DirectoryPath _toolDownloadDir;

        protected readonly DirectoryPath _globalToolStageDir;
        protected readonly DirectoryPath _localToolDownloadDir;
        protected readonly DirectoryPath _localToolAssetDir;

        public const string FakeEntrypointName = "SimulatorEntryPoint.dll";
        public const string DefaultToolCommandName = "SimulatorCommand";
        public const string DefaultPackageName = "global.tool.console.demo";
        public const string DefaultPackageVersion = "1.0.4";
        public const string FakeCommandSettingsFileName = "FakeDotnetToolSettings.json";


        private const string ProjectFileName = "TempProject.csproj";
        private readonly IFileSystem _fileSystem;
        private readonly IReporter? _reporter;
        private readonly List<MockFeed> _feeds;

        private readonly Dictionary<PackageId, IEnumerable<string>> _warningsMap;
        private readonly Dictionary<PackageId, IReadOnlyList<FilePath>> _packagedShimsMap;
        private readonly Dictionary<PackageId, IEnumerable<NuGetFramework>> _frameworksMap;
        private readonly Action? _downloadCallback;

        public ToolPackageDownloaderMock(
            IToolPackageStore store,
            IFileSystem fileSystem,
            IReporter? reporter = null,
            List<MockFeed>? feeds = null,
            Action? downloadCallback = null,
            Dictionary<PackageId, IEnumerable<string>>? warningsMap = null,
            Dictionary<PackageId, IReadOnlyList<FilePath>>? packagedShimsMap = null,
            Dictionary<PackageId, IEnumerable<NuGetFramework>>? frameworksMap = null
        )
        {
            _toolPackageStore = store ?? throw new ArgumentNullException(nameof(store)); ;
            _globalToolStageDir = _toolPackageStore.GetRandomStagingDirectory();
            _localToolDownloadDir = new DirectoryPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "nuget", "package"));
            _localToolAssetDir = new DirectoryPath(PathUtilities.CreateTempSubdirectory());

            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _reporter = reporter;

            _warningsMap = warningsMap ?? new Dictionary<PackageId, IEnumerable<string>>();
            _packagedShimsMap = packagedShimsMap ?? new Dictionary<PackageId, IReadOnlyList<FilePath>>();
            _frameworksMap = frameworksMap ?? new Dictionary<PackageId, IEnumerable<NuGetFramework>>();
            _downloadCallback = downloadCallback;

            if (feeds == null)
            {
                _feeds = new List<MockFeed>();
                _feeds.Add(new MockFeed
                {
                    Type = MockFeedType.FeedFromGlobalNugetConfig,
                    Packages = new List<MockFeedPackage>
                        {
                            new MockFeedPackage
                            {
                                PackageId = DefaultPackageName,
                                Version = DefaultPackageVersion,
                                ToolCommandName = DefaultToolCommandName,
                            }
                        }
                });
            }
            else
            {
                _feeds = feeds;
            }
        }

        public IToolPackage InstallPackage(PackageLocation packageLocation, PackageId packageId,
            VerbosityOptions verbosity,
            VersionRange? versionRange = null,
            string? targetFramework = null,
            bool isGlobalTool = false,
            bool isGlobalToolRollForward = false,
            bool verifySignatures = false,
            RestoreActionConfig? restoreActionConfig = null
            )
        {
            string? rollbackDirectory = null;
            var packageRootDirectory = _toolPackageStore.GetRootPackageDirectory(packageId);

            return TransactionalAction.Run<IToolPackage>(
                action: () =>
                {
                    var versionString = versionRange?.OriginalString ?? "*";
                    versionRange = VersionRange.Parse(versionString);
                    _toolDownloadDir = isGlobalTool ? _globalToolStageDir : _localToolDownloadDir;
                    var assetFileDirectory = isGlobalTool ? _globalToolStageDir : _localToolAssetDir;
                    rollbackDirectory = _toolDownloadDir.Value;

                    if (string.IsNullOrEmpty(packageId.ToString()))
                    {
                        throw new ToolPackageException(CliCommandStrings.ToolInstallationRestoreFailed);
                    }

                    var feedPackage = GetPackage(
                        packageId.ToString(),
                        versionRange,
                        packageLocation.NugetConfig,
                        packageLocation.RootConfigDirectory,
                        packageLocation.SourceFeedOverrides);

                    var packageVersion = feedPackage.Version;
                    targetFramework = string.IsNullOrEmpty(targetFramework) ? "targetFramework" : targetFramework;

                    rollbackDirectory = isGlobalTool ? _toolDownloadDir.Value : Path.Combine(_toolDownloadDir.Value, packageId.ToString(), packageVersion.ToString());

                    var fakeExecutableSubDirectory = Path.Combine(
                       packageId.ToString().ToLowerInvariant(),
                       packageVersion.ToLowerInvariant(),
                       "tools",
                       targetFramework,
                       Constants.AnyRid);
                    var fakeExecutablePath = Path.Combine(fakeExecutableSubDirectory, FakeEntrypointName);

                    _fileSystem.Directory.CreateDirectory(Path.Combine(_toolDownloadDir.Value, fakeExecutableSubDirectory));
                    _fileSystem.File.CreateEmptyFile(Path.Combine(_toolDownloadDir.Value, fakeExecutablePath));
                    _fileSystem.File.WriteAllText(
                        _toolDownloadDir.WithFile("project.assets.json").Value,
                        fakeExecutablePath);
                    _fileSystem.File.WriteAllText(
                        _toolDownloadDir.WithFile(FakeCommandSettingsFileName).Value,
                        JsonSerializer.Serialize(new { Name = feedPackage.ToolCommandName }));

                    if (_downloadCallback != null)
                    {
                        _downloadCallback();
                    }

                    var version = _toolPackageStore.GetStagedPackageVersion(_toolDownloadDir, packageId);
                    var packageDirectory = _toolPackageStore.GetPackageDirectory(packageId, version);
                    if (_fileSystem.Directory.Exists(packageDirectory.Value))
                    {
                        throw new ToolPackageException(
                            string.Format(
                                CliStrings.ToolPackageConflictPackageId,
                                packageId,
                                version.ToNormalizedString()));
                    }

                    if (!isGlobalTool)
                    {
                        packageDirectory = new DirectoryPath(NuGetGlobalPackagesFolder.GetLocation()).WithSubDirectories(packageId.ToString());
                        _fileSystem.Directory.CreateDirectory(packageDirectory.Value);
                        var executable = packageDirectory.WithFile("exe");
                        _fileSystem.File.CreateEmptyFile(executable.Value);
                        rollbackDirectory = Path.Combine(packageDirectory.Value, packageVersion);


                        return new TestToolPackage
                        {
                            Id = packageId,
                            Version = NuGetVersion.Parse(feedPackage.Version),
                            Command = new ToolCommand(new ToolCommandName(feedPackage.ToolCommandName), "runner", executable),
                            Warnings = Array.Empty<string>(),
                            PackagedShims = Array.Empty<FilePath>()
                        };
                    }
                    else
                    {
                        var packageRootDirectory = _toolPackageStore.GetRootPackageDirectory(packageId);

                        _fileSystem.Directory.CreateDirectory(packageRootDirectory.Value);
                        _fileSystem.Directory.Move(_toolDownloadDir.Value, packageDirectory.Value);
                        rollbackDirectory = packageDirectory.Value;

                        IEnumerable<string>? warnings = null;
                        _warningsMap.TryGetValue(packageId, out warnings);

                        IReadOnlyList<FilePath>? packedShims = null;
                        _packagedShimsMap.TryGetValue(packageId, out packedShims);

                        IEnumerable<NuGetFramework>? frameworks = null;
                        _frameworksMap.TryGetValue(packageId, out frameworks);

                        return new ToolPackageMock(_fileSystem, id: packageId,
                                        version: version,
                                        packageDirectory: packageDirectory,
                                        warnings: warnings, packagedShims: packedShims, frameworks: frameworks);
                    }
                },
                rollback: () =>
                {
                    if (rollbackDirectory != null && _fileSystem.Directory.Exists(rollbackDirectory))
                    {
                        _fileSystem.Directory.Delete(rollbackDirectory, true);
                    }
                    if (_fileSystem.Directory.Exists(packageRootDirectory.Value) &&
                    !_fileSystem.Directory.EnumerateFileSystemEntries(packageRootDirectory.Value).Any())
                    {
                        _fileSystem.Directory.Delete(packageRootDirectory.Value, false);
                    }

                });
        }

        public MockFeedPackage GetPackage(
            string packageId,
            VersionRange versionRange,
            FilePath? nugetConfig = null,
            DirectoryPath? rootConfigDirectory = null,
            string[]? sourceFeedOverrides = null)
        {
            var allPackages = _feeds
                .Where(feed =>
                {
                    if (sourceFeedOverrides is not null && sourceFeedOverrides.Length > 0)
                    {
                        return sourceFeedOverrides.Contains(feed.Uri);
                    }
                    else if (nugetConfig == null)
                    {
                        return SimulateNugetSearchNugetConfigAndMatch(
                            rootConfigDirectory,
                            feed);
                    }
                    else
                    {
                        return ExcludeOtherFeeds(nugetConfig.Value, feed);
                    }
                })
                .SelectMany(f => f.Packages)
                .Where(f => f.PackageId == packageId)
                .ToList();

            var bestVersion = versionRange.FindBestMatch(allPackages.Select(p => NuGetVersion.Parse(p.Version)));

            var package = allPackages.FirstOrDefault(p => NuGetVersion.Parse(p.Version).Equals(bestVersion));

            if (package == null)
            {
                _reporter?.WriteLine($"Error: failed to restore package {packageId}.");
                throw new ToolPackageException(CliCommandStrings.ToolInstallationRestoreFailed);
            }

            return package;
        }

        /// <summary>
        /// Simulate NuGet search nuget config from parent directories.
        /// Assume all nuget.config has Clear
        /// And then filter against mock feed
        /// </summary>
        private bool SimulateNugetSearchNugetConfigAndMatch(
            DirectoryPath? rootConfigDirectory,
            MockFeed feed)
        {
            if (rootConfigDirectory != null)
            {
                var probedNugetConfig = EnumerateDefaultAllPossibleNuGetConfig(rootConfigDirectory.Value)
                    .FirstOrDefault(possibleNugetConfig =>
                        _fileSystem.File.Exists(possibleNugetConfig.Value));

                if (!Equals(probedNugetConfig, default(FilePath)))
                {
                    return (feed.Type == MockFeedType.FeedFromLookUpNugetConfig) ||
                           (feed.Type == MockFeedType.ImplicitAdditionalFeed) ||
                           (feed.Type == MockFeedType.FeedFromLookUpNugetConfig
                            && feed.Uri == probedNugetConfig.Value);
                }
            }

            return feed.Type != MockFeedType.ExplicitNugetConfig
                    && feed.Type != MockFeedType.FeedFromLookUpNugetConfig;
        }

        private static IEnumerable<FilePath> EnumerateDefaultAllPossibleNuGetConfig(DirectoryPath probStart)
        {
            DirectoryPath? currentSearchDirectory = probStart;
            while (currentSearchDirectory.HasValue)
            {
                var tryNugetConfig = currentSearchDirectory.Value.WithFile("nuget.config");
                yield return tryNugetConfig;
                currentSearchDirectory = currentSearchDirectory.Value.GetParentPathNullable();
            }
        }

        private static bool ExcludeOtherFeeds(FilePath nugetConfig, MockFeed f)
        {
            return f.Type == MockFeedType.ImplicitAdditionalFeed
                   || (f.Type == MockFeedType.ExplicitNugetConfig && f.Uri == nugetConfig.Value);
        }

        public (NuGetVersion version, PackageSource source) GetNuGetVersion(
            PackageLocation packageLocation,
            PackageId packageId,
            VerbosityOptions verbosity,
            VersionRange? versionRange = null,
            RestoreActionConfig? restoreActionConfig = null)
        {
            versionRange = VersionRange.Parse(versionRange?.OriginalString ?? "*");

            if (string.IsNullOrEmpty(packageId.ToString()))
            {
                throw new ToolPackageException(CliCommandStrings.ToolInstallationRestoreFailed);
            }

            var feedPackage = GetPackage(
                packageId.ToString(),
                versionRange,
                packageLocation.NugetConfig,
                packageLocation.RootConfigDirectory,
                packageLocation.SourceFeedOverrides);

            return (NuGetVersion.Parse(feedPackage.Version), new PackageSource("http://mock-feed", "MockFeed"));
        }

        public bool TryGetDownloadedTool(PackageId packageId, NuGetVersion packageVersion, string? targetFramework, VerbosityOptions verbosity, [NotNullWhen(true)] out IToolPackage? toolPackage) => throw new NotImplementedException();

        private class TestToolPackage : IToolPackage
        {
            public PackageId Id { get; set; }

            public NuGetVersion? Version { get; set; }
            public DirectoryPath PackageDirectory { get; set; }

            public ToolCommand? Command { get; set; }

            public IEnumerable<string>? Warnings { get; set; }

            public IReadOnlyList<FilePath>? PackagedShims { get; set; }

            public IEnumerable<NuGetFramework> Frameworks => throw new NotImplementedException();

            public PackageId ResolvedPackageId { get; set; }

            public NuGetVersion? ResolvedPackageVersion { get; set; }
        }
    }
}
