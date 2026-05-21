using System.Security;
using System.Text;
using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public sealed class OivAssemblyXmlWriter : IOivAssemblyXmlWriter
{
    public string Write(OivPackagePlan plan)
    {
        var id = Guid.NewGuid().ToString("D");
        var sb = new StringBuilder();

        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendLine($"""<package version="2.2" id="{id}" target="Five">""");
        sb.AppendLine("  <metadata>");
        sb.AppendLine($"    <name>{X(plan.Name)}</name>");

        var (major, minor) = SplitVersion(plan.Version);
        sb.AppendLine( "    <version>");
        sb.AppendLine($"      <major>{X(major)}</major>");
        sb.AppendLine($"      <minor>{X(minor)}</minor>");
        sb.AppendLine( "    </version>");

        sb.AppendLine($"    <author>{X(plan.Author)}</author>");
        sb.AppendLine($"    <description>{X(plan.Description)}</description>");
        sb.AppendLine("  </metadata>");
        sb.AppendLine("  <content>");

        foreach (var f in plan.Files)
        {
            // content/ prefix is the OIV spec convention for the source location inside the ZIP.
            var src = $"content/{f.InstallPath.TrimStart('/')}";
            sb.AppendLine($"""    <add source="{X(src)}">{X(f.InstallPath)}</add>""");
        }

        sb.AppendLine("  </content>");
        sb.Append("</package>");

        return sb.ToString();
    }

    private static string X(string s) => SecurityElement.Escape(s) ?? s;

    private static (string major, string minor) SplitVersion(string v)
    {
        // Strip a leading 'v' (e.g. "v1.2" → "1.2"), then take the first two numeric segments.
        var trimmed = v.TrimStart('v', 'V');
        var parts   = trimmed.Split('.');
        var major   = ParseSegment(parts.Length > 0 ? parts[0] : "", 1);
        var minor   = ParseSegment(parts.Length > 1 ? parts[1] : "", 0);
        return (major.ToString(), minor.ToString());
    }

    private static int ParseSegment(string s, int fallback) =>
        int.TryParse(s, out var n) ? n : fallback;
}
