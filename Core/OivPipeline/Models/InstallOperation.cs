using System.Text.Json.Serialization;

namespace LSPDFRManager.OivPipeline.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(CopyOperation), "copy")]
[JsonDerivedType(typeof(PatchXmlOperation), "patchXml")]
[JsonDerivedType(typeof(EnsureModsCopyOperation), "ensureModsCopy")]
public abstract record InstallOperation;

public sealed record CopyOperation(
    string SourceRelativePath,
    string TargetGamePath,
    bool Overwrite = true
) : InstallOperation;

public sealed record PatchXmlOperation(
    string TargetGamePath,
    string Snippet,
    string IdempotencyKey,
    string InsertionRule = "append-unique"
) : InstallOperation;

public sealed record EnsureModsCopyOperation(
    string SourceArchivePath,
    string TargetArchivePath
) : InstallOperation;
