using System.Collections.Generic;

namespace LSPDFRManager.Features.OivCreatorTemplates.Models;

public sealed record OivWizardSnapshot(
    string PackageName,
    string CurrentDescription,
    string CurrentVersion,
    IReadOnlyList<string> FileNames);
