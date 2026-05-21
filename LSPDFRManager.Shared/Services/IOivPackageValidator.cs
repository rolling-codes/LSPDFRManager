using LSPDFRManager.Domain;

namespace LSPDFRManager.Services;

public interface IOivPackageValidator
{
    /// <summary>
    /// Validates <paramref name="plan"/> and returns a derived plan with Errors/Warnings
    /// augmented by any findings.  Does not mutate the source plan.
    /// </summary>
    OivPackagePlan Validate(OivPackagePlan plan);
}
