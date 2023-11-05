using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using Robust.Packaging;
using Robust.Packaging.AssetProcessing;
using Robust.Packaging.AssetProcessing.Passes;
using Robust.Packaging.Utility;
using Robust.Shared.Audio;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Content.Packaging;

public static class ServerPackaging
{
    private static readonly List<PlatformReg> Platforms = new()
    {
        new PlatformReg("win-x64", "Windows", true),
        new PlatformReg("linux-x64", "Linux", true),
        new PlatformReg("linux-arm64", "Linux", true),
        new PlatformReg("osx-x64", "MacOS", true),
        // Non-default platforms (i.e. for Watchdog Git)
        new PlatformReg("win-x86", "Windows", false),
        new PlatformReg("linux-x86", "Linux", false),
        new PlatformReg("linux-arm", "Linux", false),
    };

    private static List<string> PlatformRids => Platforms
        .Select(o => o.Rid)
        .ToList();

    private static List<string> PlatformRidsDefault => Platforms
        .Where(o => o.BuildByDefault)
        .Select(o => o.Rid)
        .ToList();

    private static readonly List<string> ServerContentAssemblies = new()
    {
        "Content.Server.Database",
        "Content.Server",
        "Content.Shared",
        "Content.Shared.Database",
    };

    private static readonly List<string> ServerExtraAssemblies = new()
    {
        // Python script had Npgsql. though we want Npgsql.dll as well soooo
        "Npgsql",
        "Microsoft",
    };

    private static readonly List<string> ServerNotExtraAssemblies = new()
    {
        "Microsoft.CodeAnalysis",
    };

    private static readonly HashSet<string> BinSkipFolders = new()
    {
        // Roslyn localization files, screw em.
        "cs",
        "de",
        "es",
        "fr",
        "it",
        "ja",
        "ko",
        "pl",
        "pt-BR",
        "ru",
        "tr",
        "zh-Hans",
        "zh-Hant"
    };

    public static async Task PackageServer(bool skipBuild, bool hybridAcz, IPackageLogger logger, List<string>? platforms = null)
    {
        if (platforms == null)
        {
            platforms ??= PlatformRidsDefault;
        }

        if (hybridAcz)
        {
            // Hybrid ACZ involves a file "Content.Client.zip" in the server executable directory.
            // Rather than hosting the client ZIP on the watchdog or on a separate server,
            //  Hybrid ACZ uses the ACZ hosting functionality to host it as part of the status host,
            //  which means that features such as automatic UPnP forwarding still work properly.
            await ClientPackaging.PackageClient(skipBuild, logger);
        }

        // Good variable naming right here.
        foreach (var platform in Platforms)
        {
            if (!platforms.Contains(platform.Rid))
                continue;

            await BuildPlatform(platform, skipBuild, hybridAcz, logger);
        }
    }

    private static async Task BuildPlatform(PlatformReg platform, bool skipBuild, bool hybridAcz, IPackageLogger logger)
    {
        logger.Info("Building project for {platform}...");

        if (!skipBuild)
        {
            await ProcessHelpers.RunCheck(new ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList =
                {
                    "build",
                    Path.Combine("Content.Server", "Content.Server.csproj"),
                    "-c", "Release",
                    "--nologo",
                    "/v:m",
                    $"/p:TargetOs={platform.TargetOs}",
                    "/t:Rebuild",
                    "/p:FullRelease=true",
                    "/m"
                }
            });

            await PublishClientServer(platform.Rid, platform.TargetOs);
        }

        logger.Info($"Packaging {platform.Rid} server...");

        var sw = RStopwatch.StartNew();
        {
            await using var zipFile =
                File.Open(Path.Combine("release", $"SS14.Server_{platform.Rid}.zip"), FileMode.Create, FileAccess.ReadWrite);
            using var zip = new ZipArchive(zipFile, ZipArchiveMode.Update);
            var writer = new AssetPassZipWriter(zip);

            await WriteServerResources(platform, "", writer, logger, hybridAcz, default);
            await writer.FinishedTask;
        }

        logger.Info($"Finished packaging server in {sw.Elapsed}");
    }

    private static async Task PublishClientServer(string runtime, string targetOs)
    {
        await ProcessHelpers.RunCheck(new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList =
            {
                "publish",
                "--runtime", runtime,
                "--no-self-contained",
                "-c", "Release",
                $"/p:TargetOs={targetOs}",
                "/p:FullRelease=True",
                "/m",
                "RobustToolbox/Robust.Server/Robust.Server.csproj"
            }
        });
    }

    private static async Task WriteServerResources(
        PlatformReg platform,
        string contentDir,
        AssetPass pass,
        IPackageLogger logger,
        bool hybridAcz,
        CancellationToken cancel)
    {
        var graph = new RobustClientAssetGraph();
        var passes = graph.AllPasses.ToList();

        // Bundle audio metadata
        // TODO: Sometimes the pass gets skipped due to uhh dependency stuff
        // This is honestly a mess but I just want audio rework and
        // this is significantly easier to fix than porting it from python was.
        var audioPass = new AssetPassAudioMetadata("Resources/audio_metadata.yml");
        pass.Dependencies.Add(new AssetPassDependency(audioPass.Name));
        passes.Add(audioPass);
        audioPass.Dependencies.Add(new AssetPassDependency(graph.Output.Name));

        pass.Dependencies.Add(new AssetPassDependency(graph.Output.Name));
        passes.Add(pass);

        AssetGraph.CalculateGraph(passes, logger);

        var inputPass = graph.Input;
        var contentAssemblies = new List<string>(ServerContentAssemblies);

        // Additional assemblies that need to be copied such as EFCore.
        var sourcePath = Path.Combine(contentDir, "bin", "Content.Server");

        // Should this be an asset pass?
        // For future archaeologists I just want audio rework to work and need the audio pass so
        // just porting this as is from python.
        foreach (var fullPath in Directory.EnumerateFiles(sourcePath, "*.*", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileNameWithoutExtension(fullPath);

            if (!ServerNotExtraAssemblies.Any(o => fileName.StartsWith(o)) && ServerExtraAssemblies.Any(o => fileName.StartsWith(o)))
            {
                contentAssemblies.Add(fileName);
            }
        }

        await RobustSharedPackaging.DoResourceCopy(
            Path.Combine("RobustToolbox", "bin", "Server",
            platform.Rid,
            "publish"),
            inputPass,
            BinSkipFolders,
            cancel: cancel);

        await RobustSharedPackaging.WriteContentAssemblies(
            inputPass,
            contentDir,
            "Content.Server",
            contentAssemblies,
            Path.Combine("Resources", "Assemblies"),
            cancel);

        await RobustServerPackaging.WriteServerResources(contentDir, inputPass, cancel);

        if (hybridAcz)
        {
            inputPass.InjectFileFromDisk("Content.Client.zip", Path.Combine("release", "SS14.Client.zip"));
        }

        inputPass.InjectFinished();
    }

    /// <summary>
    /// Strips out audio files and writes them to a metadata .yml
    /// </summary>
    private sealed class AssetPassAudioMetadata : AssetPass
    {
        private string[] _audioExtensions = new[]
        {
            ".ogg",
            ".wav",
        };

        private List<AudioMetadataPrototype> _audioMetadata = new();

        private readonly string _metadataPath;

        public AssetPassAudioMetadata(string metadataPath = "audio_metadata.yml")
        {
            _metadataPath = metadataPath;
        }

        protected override AssetFileAcceptResult AcceptFile(AssetFile file)
        {
            var ext = Path.GetExtension(file.Path);

            if (!_audioExtensions.Contains(ext))
            {
                return AssetFileAcceptResult.Pass;
            }

            var updatedName = file.Path.Replace("/", "_");
            TimeSpan length;

            if (ext == ".ogg")
            {
                // TODO: This should just be using the engine to derive it.
                using var stream = file.Open();
                using var vorbis = new NVorbis.VorbisReader(stream, false);
                length = vorbis.TotalTime;
            }
            else if (ext == ".wav")
            {
                // Good luck
                throw new NotImplementedException();
            }
            else
            {
                throw new NotImplementedException($"No audio metadata processing implemented for {ext}");
            }

            _audioMetadata.Add(new AudioMetadataPrototype()
            {
                ID = updatedName,
                Length = length,
            });

            return AssetFileAcceptResult.Consumed;
        }

        protected override void AcceptFinished()
        {
            if (_audioMetadata.Count == 0)
                return;

            // ReSharper disable once InconsistentlySynchronizedField
            var root = new YamlSequenceNode();
            var document = new YamlDocument(root);

            foreach (var prototype in _audioMetadata)
            {
                // TODO: I know but sermanager and please get me out of this hell.
                var jaml = new YamlMappingNode();
                jaml.Add("id", new YamlScalarNode(prototype.ID));
                jaml.Add("length", new YamlScalarNode(prototype.Length.TotalSeconds.ToString(CultureInfo.InvariantCulture)));
                root.Add(jaml);
            }

            RunJob(() =>
            {
                using var memory = new MemoryStream();
                using var writer = new StreamWriter(memory);
                var yamlStream = new YamlStream(document);
                yamlStream.Save(new YamlNoDocEndDotsFix(new YamlMappingFix(new Emitter(writer))), false);
                writer.Flush();
                var result = new AssetFileMemory(_metadataPath, memory.ToArray());
                SendFile(result);
            });
        }
    }

    private readonly record struct PlatformReg(string Rid, string TargetOs, bool BuildByDefault);
}
