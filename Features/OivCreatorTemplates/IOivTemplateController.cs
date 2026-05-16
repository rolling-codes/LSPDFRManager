using System.Collections.Generic;
using LSPDFRManager.Features.OivCreatorTemplates.Models;

namespace LSPDFRManager.Features.OivCreatorTemplates;

public interface IOivTemplateController
{
    IReadOnlyList<OivTemplateDefinition> GetAvailableTemplates();
    OivTemplateApplyPlan BuildPlan(OivTemplateId id, OivWizardSnapshot snapshot);
}

public sealed record OivTemplateDefinition(OivTemplateId Id, string DisplayName, string Description);
