using System.Diagnostics;
using Robust.Packaging.Utility;

namespace Content.Packaging;

/// <summary>Recheck source before packaging, including --skip-build.</summary>
internal static class EngineCompatibility
{
    public const string MetadataPath = "release/engine-compatibility.json";

    public static Task Validate(bool writeMetadata = false)
    {
        var start = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "python" : "python3",
            ArgumentList = { "Tools/check_engine_compatibility.py" }
        };
        if (writeMetadata)
        {
            start.ArgumentList.Add("--metadata-output");
            start.ArgumentList.Add(MetadataPath);
        }
        return ProcessHelpers.RunCheck(start);
    }
}
