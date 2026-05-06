using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

/// <summary>
/// Tests for <see cref="PathSafety.GetSafePath"/> path traversal protection.
/// Ensures malicious or accidental traversal outside target root is blocked.
/// </summary>
public class PathSafetyTests
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"path_safety_{Guid.NewGuid():N}");

    public PathSafetyTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    // ── Valid paths ────────────────────────────────────────────────────────

    [Fact]
    public void GetSafePath_RelativePathWithinRoot_Succeeds()
    {
        var result = PathSafety.GetSafePath(_tempRoot, "mod/file.dll");
        var expected = Path.Combine(_tempRoot, "mod", "file.dll");
        Assert.Equal(expected, result, ignoreCase: true);
    }

    [Fact]
    public void GetSafePath_DeepNestedPath_Succeeds()
    {
        var result = PathSafety.GetSafePath(_tempRoot, "a/b/c/d/e/file.dll");
        var expected = Path.Combine(_tempRoot, "a", "b", "c", "d", "e", "file.dll");
        Assert.Equal(expected, result, ignoreCase: true);
    }

    [Fact]
    public void GetSafePath_SingleFileName_Succeeds()
    {
        var result = PathSafety.GetSafePath(_tempRoot, "file.txt");
        var expected = Path.Combine(_tempRoot, "file.txt");
        Assert.Equal(expected, result, ignoreCase: true);
    }

    [Fact]
    public void GetSafePath_ForwardSlashesInWindowsPath_Succeeds()
    {
        var result = PathSafety.GetSafePath(_tempRoot, "plugins/lspdfr/callout.dll");
        var expected = Path.Combine(_tempRoot, "plugins", "lspdfr", "callout.dll");
        Assert.Equal(expected, result, ignoreCase: true);
    }

    [Fact]
    public void GetSafePath_BackslashesInWindowsPath_Succeeds()
    {
        var result = PathSafety.GetSafePath(_tempRoot, @"plugins\lspdfr\callout.dll");
        var expected = Path.Combine(_tempRoot, "plugins", "lspdfr", "callout.dll");
        Assert.Equal(expected, result, ignoreCase: true);
    }

    [Fact]
    public void GetSafePath_LeadingSlash_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => PathSafety.GetSafePath(_tempRoot, "/file.txt")
        );
        Assert.Contains("Path traversal detected", ex.Message);
    }

    // ── Traversal attacks ──────────────────────────────────────────────────

    [Fact]
    public void GetSafePath_ParentTraversal_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => PathSafety.GetSafePath(_tempRoot, "../escape.dll")
        );
        Assert.Contains("Path traversal detected", ex.Message);
    }

    [Fact]
    public void GetSafePath_MultipleParentTraversal_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => PathSafety.GetSafePath(_tempRoot, "../../outside.dll")
        );
        Assert.Contains("Path traversal detected", ex.Message);
    }

    [Fact]
    public void GetSafePath_DeepTraversalAttack_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => PathSafety.GetSafePath(_tempRoot, "../../../../../../../evil.dll")
        );
        Assert.Contains("Path traversal detected", ex.Message);
    }

    [Fact]
    public void GetSafePath_TraversalInMiddle_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => PathSafety.GetSafePath(_tempRoot, "mod/../../../escape.dll")
        );
        Assert.Contains("Path traversal detected", ex.Message);
    }

    [Fact]
    public void GetSafePath_DoubleSlashTraversal_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => PathSafety.GetSafePath(_tempRoot, "..\\..\\escape.dll")
        );
        Assert.Contains("Path traversal detected", ex.Message);
    }

    [Fact]
    public void GetSafePath_LeadingBackslash_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => PathSafety.GetSafePath(_tempRoot, "\\file.txt")
        );
        Assert.Contains("Path traversal detected", ex.Message);
    }

    [Fact]
    public void GetSafePath_DriveQualifiedPath_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => PathSafety.GetSafePath(_tempRoot, "C:\\Windows\\win.ini")
        );
        Assert.Contains("Path traversal detected", ex.Message);
    }

    [Fact]
    public void GetSafePath_UncPath_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => PathSafety.GetSafePath(_tempRoot, "\\\\server\\share\\file.txt")
        );
        Assert.Contains("Path traversal detected", ex.Message);
    }

    // ── Edge cases ─────────────────────────────────────────────────────────

    [Fact]
    public void GetSafePath_EmptyRelativePath_Succeeds()
    {
        var result = PathSafety.GetSafePath(_tempRoot, "");
        Assert.Equal(_tempRoot, result, ignoreCase: true);
    }

    [Fact]
    public void GetSafePath_RootWithoutTrailingSeparator_Succeeds()
    {
        var root = _tempRoot.TrimEnd(Path.DirectorySeparatorChar);
        var result = PathSafety.GetSafePath(root, "file.dll");
        var expected = Path.Combine(root, "file.dll");
        Assert.Equal(expected, result, ignoreCase: true);
    }

    [Fact]
    public void GetSafePath_RootWithTrailingSeparator_Succeeds()
    {
        var root = _tempRoot.EndsWith(Path.DirectorySeparatorChar)
            ? _tempRoot
            : _tempRoot + Path.DirectorySeparatorChar;
        var result = PathSafety.GetSafePath(root, "file.dll");
        var expected = Path.Combine(_tempRoot, "file.dll");
        Assert.Equal(expected, result, ignoreCase: true);
    }

    [Fact]
    public void GetSafePath_LongPath_Succeeds()
    {
        var longPath = string.Join("/", Enumerable.Range(0, 20).Select(i => $"dir{i}"));
        var result = PathSafety.GetSafePath(_tempRoot, longPath);
        var expected = Path.Combine(new[] { _tempRoot }.Concat(Enumerable.Range(0, 20).Select(i => $"dir{i}")).ToArray());
        Assert.Equal(expected, result, ignoreCase: true);
    }

    [Fact]
    public void GetSafePath_SpecialCharactersInFilename_Succeeds()
    {
        var result = PathSafety.GetSafePath(_tempRoot, "file (1).dll");
        var expected = Path.Combine(_tempRoot, "file (1).dll");
        Assert.Equal(expected, result, ignoreCase: true);
    }

    [Fact]
    public void GetSafePath_DotInPathButNotTraversal_Succeeds()
    {
        var result = PathSafety.GetSafePath(_tempRoot, "mod.1.0/file.dll");
        var expected = Path.Combine(_tempRoot, "mod.1.0", "file.dll");
        Assert.Equal(expected, result, ignoreCase: true);
    }
}
