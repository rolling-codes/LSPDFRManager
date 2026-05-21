using LSPDFRManager.Services;
using Xunit;

namespace LSPDFRManager.Tests;

public class LspdfrThumbnailExtractorTests
{
    [Fact]
    public void ExtractThumbnailUrl_UsesOgImage_First()
    {
        var html = """
            <html>
              <head>
                <meta property="og:image" content="https://www.lcpdfr.com/images/og.jpg" />
                <meta name="twitter:image" content="https://www.lcpdfr.com/images/twitter.jpg" />
              </head>
            </html>
            """;

        var result = LspdfrThumbnailExtractor.ExtractThumbnailUrl(
            "https://www.lcpdfr.com/files/file/1001-test/",
            html);

        Assert.Equal("https://www.lcpdfr.com/images/og.jpg", result);
    }

    [Fact]
    public void ExtractThumbnailUrl_UsesTwitterImage_WhenOgMissing()
    {
        var html = """
            <html>
              <head>
                <meta name="twitter:image" content="https://www.lspdfr.com/images/twitter.jpg" />
              </head>
            </html>
            """;

        var result = LspdfrThumbnailExtractor.ExtractThumbnailUrl(
            "https://www.lspdfr.com/files/file/1002-test/",
            html);

        Assert.Equal("https://www.lspdfr.com/images/twitter.jpg", result);
    }

    [Fact]
    public void ExtractThumbnailUrl_UsesFirstImageFallback_WhenMetaMissing()
    {
        var html = """
            <html>
              <body>
                <img src="https://www.lcpdfr.com/images/first.jpg" />
                <img src="https://www.lcpdfr.com/images/second.jpg" />
              </body>
            </html>
            """;

        var result = LspdfrThumbnailExtractor.ExtractThumbnailUrl(
            "https://www.lcpdfr.com/files/file/1003-test/",
            html);

        Assert.Equal("https://www.lcpdfr.com/images/first.jpg", result);
    }

    [Fact]
    public void ExtractThumbnailUrl_RejectsUnsafeSchemes()
    {
        var html = """
            <html>
              <head>
                <meta property="og:image" content="javascript:alert(1)" />
              </head>
            </html>
            """;

        var result = LspdfrThumbnailExtractor.ExtractThumbnailUrl(
            "https://www.lcpdfr.com/files/file/1004-test/",
            html);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractThumbnailUrl_ResolvesRelativeUrlSafely()
    {
        var html = """
            <html>
              <head>
                <meta property="og:image" content="/uploads/monthly_2026_05/thumb.jpg" />
              </head>
            </html>
            """;

        var result = LspdfrThumbnailExtractor.ExtractThumbnailUrl(
            "https://www.lcpdfr.com/files/file/1005-test/",
            html);

        Assert.Equal("https://www.lcpdfr.com/uploads/monthly_2026_05/thumb.jpg", result);
    }

    [Fact]
    public void ExtractThumbnailUrl_MalformedHtml_DoesNotThrow()
    {
        var html = "<html><head><meta property=\"og:image\" content=\"https://www.lcpdfr.com/images/x.jpg\"";

        var ex = Record.Exception(() => LspdfrThumbnailExtractor.ExtractThumbnailUrl(
            "https://www.lcpdfr.com/files/file/1006-test/",
            html));

        Assert.Null(ex);
    }

    [Fact]
    public void ExtractThumbnailUrl_RejectsHostOutsideAllowlist()
    {
        var html = """
            <html>
              <head>
                <meta property="og:image" content="https://example.com/evil.jpg" />
              </head>
            </html>
            """;

        var result = LspdfrThumbnailExtractor.ExtractThumbnailUrl(
            "https://www.lcpdfr.com/files/file/1007-test/",
            html);

        Assert.Null(result);
    }
}
