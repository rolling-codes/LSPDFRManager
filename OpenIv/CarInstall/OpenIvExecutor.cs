using LSPDFRManager.Core;
using LSPDFRManager.Models;
using LSPDFRManager.OpenIv.CarInstall.Models;
using LSPDFRManager.Services;

namespace LSPDFRManager.OpenIv.CarInstall;

/// <summary>
/// Executes a validated OpenIvInstallPlan by:
/// 1. Extracting files from archive to target root
/// 2. Applying XML patches
/// 3. Tracking written files for rollback on failure
///
/// This is a dumb procedural layer: no decisions, no validation, no re-analysis.
/// Assumes plan is valid (Validator has passed).
/// </summary>
public class OpenIvExecutor
{
    private readonly IXmlPatcher _xmlPatcher;

    public OpenIvExecutor(IXmlPatcher xmlPatcher)
    {
        _xmlPatcher = xmlPatcher;
    }

    /// <summary>
    /// Executes plan: extracts files from archive, applies XML patches.
    /// Returns InstallResult with success/failure/rollback state.
    /// </summary>
    public async Task<InstallResult> ExecuteAsync(
        OpenIvInstallPlan plan,
        IArchive archive,
        string targetRoot)
    {
        var writtenFiles = new List<string>();

        try
        {
            // 1. Extract files from archive
            foreach (var operation in plan.Operations)
            {
                var sourceEntry = archive.Entries
                    .FirstOrDefault(e => e.Key == operation.SourcePath);

                if (sourceEntry is null)
                    throw new InvalidOperationException(
                        $"Archive entry not found: {operation.SourcePath}");

                var destPath = Path.Combine(targetRoot, operation.DestinationPath);
                var destDir = Path.GetDirectoryName(destPath);

                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);

                writtenFiles.Add(destPath);

                using (var entryStream = sourceEntry.OpenEntryStream())
                {
                    using (var destFile = File.Create(destPath))
                    {
                        await entryStream.CopyToAsync(destFile);
                    }
                }
            }

            // 2. Apply XML patches
            foreach (var patch in plan.XmlPatches)
            {
                var patchFilePath = Path.Combine(targetRoot, patch.FilePath);
                var xmlPatch = new XmlPatch
                {
                    FilePath = patchFilePath,
                    XPath = patch.XPath,
                    Value = patch.Value
                };
                _xmlPatcher.Apply(xmlPatch);
            }

            return new InstallResult
            {
                Success = true,
                FilesWritten = writtenFiles.Count
            };
        }
        catch (Exception ex)
        {
            await RollbackAsync(writtenFiles);

            return new InstallResult
            {
                Success = false,
                IsPartial = writtenFiles.Count > 0,
                FilesWritten = writtenFiles.Count,
                Error = ex.Message
            };
        }
    }

    private static Task RollbackAsync(List<string> files)
    {
        foreach (var file in files.AsEnumerable().Reverse())
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch
            {
            }
        }

        return Task.CompletedTask;
    }
}
