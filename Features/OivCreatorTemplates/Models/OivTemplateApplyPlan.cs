using System.Collections.Generic;

namespace LSPDFRManager.Features.OivCreatorTemplates.Models;

public sealed record PathRule(string Match, string TargetPath, bool MatchFileName = true);

public sealed record OivTemplateApplyPlan(
    Dictionary<string, string> MetadataUpdates,
    List<PathRule> PathSuggestions);
