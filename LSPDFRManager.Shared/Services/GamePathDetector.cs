using LSPDFRManager.Domain;
using Microsoft.Win32;

namespace LSPDFRManager.Services;

public class GamePathDetector
{
    public List<GamePathCandidate> Detect() => new SetupWizardService().DetectGamePaths();

    public GamePathCandidate? BestCandidate() =>
        Detect().FirstOrDefault(c => c.IsValid);
}
