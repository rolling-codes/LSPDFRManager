namespace LSPDFRManager.OivPipeline;

using System.Reflection;
using LSPDFRManager.OivPipeline.Models;

public sealed class OivBuildPipeline
{
    private readonly IOivBuilder _oivBuilder;

    public OivBuildPipeline(IOivBuilder? oivBuilder = null)
    {
        _oivBuilder = oivBuilder ?? new StubOivBuilder();
    }

    public async Task<PipelineResult> RunAsync(
        string bundleRoot, string outputDir, bool dryRun = false, CancellationToken ct = default)
    {
        var ts = DateTimeOffset.UtcNow;
        var ver = Assembly.GetExecutingAssembly().GetName().Version?.ToString();

        // Stage 1: Scan
        IReadOnlyList<BundleFile> files;
        try
        {
            files = BundleScanner.Scan(bundleRoot);
        }
        catch (Exception ex)
        {
            return Refused(outputDir, dryRun, ts, ver, [$"Bundle scan failed: {ex.Message}"]);
        }

        // Stage 2: Read manifest
        var manifestResult = ManifestReader.Read(files, bundleRoot);
        if (manifestResult.ValidationErrors.Count > 0)
        {
            return Refused(outputDir, dryRun, ts, ver, manifestResult.ValidationErrors,
                hashes: BuildFileHashes(files));
        }

        // Stage 3: Classify
        var classification = BundleClassifier.Classify(files, manifestResult.Manifest);
        if (!classification.IsClassified)
        {
            return Refused(outputDir, dryRun, ts, ver, classification.RefusalReasons,
                classification: classification,
                manifest: manifestResult.Manifest,
                hashes: BuildFileHashes(files));
        }

        // Stage 4: Validate
        var validation = BundleValidator.Validate(classification, files, manifestResult.Manifest);
        if (!validation.Passed)
        {
            var packageName = manifestResult.Manifest?.PackageName
                ?? Path.GetFileName(bundleRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            var report = new BuildReport
            {
                PackageName = packageName,
                ManifestData = manifestResult.Manifest,
                DetectedType = classification.Type?.ToString(),
                ConfidenceSource = classification.Source?.ToString(),
                ValidationResults = validation.Gates,
                InstallOperations = [],
                Warnings = [],
                RefusalReasons = validation.RefusalReasons,
                FileHashes = BuildFileHashes(files),
                Timestamp = ts,
                AppVersion = ver,
                DryRun = dryRun
            };

            string? reportPath = null;
            if (!dryRun)
            {
                reportPath = BuildReporter.WriteReport(outputDir, report);
            }

            return new PipelineResult
            {
                Success = false,
                Report = report,
                ReportPath = reportPath,
                RefusalReasons = validation.RefusalReasons
            };
        }

        // Stage 5: Plan
        IReadOnlyList<InstallOperation> operations;
        try
        {
            operations = InstallPlanner.Plan(classification, files, manifestResult.Manifest!);
        }
        catch (Exception ex)
        {
            return Refused(outputDir, dryRun, ts, ver, [$"Install planning failed: {ex.Message}"],
                classification: classification,
                manifest: manifestResult.Manifest,
                gates: validation.Gates,
                hashes: BuildFileHashes(files));
        }

        var packageNameFinal = manifestResult.Manifest?.PackageName
            ?? Path.GetFileName(bundleRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        var warnings = new List<string>();

        // Emit warnings that apply regardless of dry-run
        if (classification.Type == BundleType.SirenPack)
        {
            warnings.Add(
                "siren_pack: no routing metadata found in manifest. Files are copied to their bundle-relative paths. " +
                "Verify install destinations manually before deploying.");
        }

        // Stage 6: Build (skip if dryRun)
        OivBuildResult? buildResult = null;
        if (!dryRun)
        {
            var buildInput = new OivBuildInput(
                PackageName: packageNameFinal,
                Version: ver ?? "0.0.0.0",
                Author: "",
                Operations: operations,
                OutputDirectory: outputDir
            );
            buildResult = await _oivBuilder.BuildAsync(buildInput, ct);
            if (!buildResult.Success)
            {
                warnings.Add($"OIV builder: {buildResult.Error}");
            }
        }

        // Stage 7: Report
        var finalReport = new BuildReport
        {
            PackageName = packageNameFinal,
            ManifestData = manifestResult.Manifest,
            DetectedType = classification.Type?.ToString(),
            ConfidenceSource = classification.Source?.ToString(),
            ValidationResults = validation.Gates,
            InstallOperations = operations,
            Warnings = warnings,
            RefusalReasons = [],
            FileHashes = BuildFileHashes(files),
            Timestamp = ts,
            AppVersion = ver,
            DryRun = dryRun
        };

        string? finalReportPath = null;
        if (!dryRun)
        {
            finalReportPath = BuildReporter.WriteReport(outputDir, finalReport);
        }

        return new PipelineResult
        {
            Success = buildResult?.Success ?? true,
            Report = finalReport,
            ReportPath = finalReportPath,
            RefusalReasons = []
        };
    }

    private static IReadOnlyDictionary<string, string> BuildFileHashes(IReadOnlyList<BundleFile> files) =>
        files.ToDictionary(f => f.RelativePath, f => f.ContentHash);

    private PipelineResult Refused(
        string outputDir,
        bool dryRun,
        DateTimeOffset ts,
        string? ver,
        IReadOnlyList<string> reasons,
        ClassificationResult? classification = null,
        BundleManifest? manifest = null,
        IReadOnlyList<ValidationGate>? gates = null,
        IReadOnlyDictionary<string, string>? hashes = null)
    {
        var report = new BuildReport
        {
            PackageName = manifest?.PackageName ?? "",
            ManifestData = manifest,
            DetectedType = classification?.Type?.ToString(),
            ConfidenceSource = classification?.Source?.ToString(),
            ValidationResults = gates ?? [],
            InstallOperations = [],
            Warnings = [],
            RefusalReasons = reasons,
            FileHashes = hashes ?? new Dictionary<string, string>(),
            Timestamp = ts,
            AppVersion = ver,
            DryRun = dryRun
        };

        string? reportPath = null;
        if (!dryRun)
            reportPath = BuildReporter.WriteReport(outputDir, report);

        return new PipelineResult
        {
            Success = false,
            Report = report,
            ReportPath = reportPath,
            RefusalReasons = reasons
        };
    }
}
