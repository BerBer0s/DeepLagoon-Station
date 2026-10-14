using Content.Packaging;
using Robust.Packaging;

IPackageLogger logger = new PackageLoggerConsole();

if (!CommandLineArgs.TryParse(args, out var parsed))
{
    logger.Error("Unable to parse args, aborting.");
    return;
}

// Reject incompatible sources before deleting any existing build outputs.
await EngineCompatibility.Validate();
if (parsed.SkipBuild)
    throw new InvalidOperationException("--skip-build cannot verify the engine provenance of existing content DLLs. Rebuild the release without --skip-build.");

if (parsed.WipeRelease)
    WipeRelease();
else
{
    // Ensure the release directory exists. Otherwise, the packaging will fail.
    Directory.CreateDirectory("release");
}

if (!parsed.SkipBuild)
    WipeBin();

await EngineCompatibility.Validate(writeMetadata: true);

if (parsed.Client)
{
    await ClientPackaging.PackageClient(parsed.SkipBuild, parsed.Configuration, logger);
}
else
{
    await ServerPackaging.PackageServer(parsed.SkipBuild, parsed.HybridAcz, logger, parsed.Configuration, parsed.Platforms);
}

void WipeBin()
{
    logger.Info("Clearing old build artifacts (if any)...");

    if (Directory.Exists("bin"))
        Directory.Delete("bin", recursive: true);
}

void WipeRelease()
{
    if (Directory.Exists("release"))
    {
        logger.Info("Cleaning old release packages (release/)...");
        Directory.Delete("release", recursive: true);
    }

    Directory.CreateDirectory("release");
}
