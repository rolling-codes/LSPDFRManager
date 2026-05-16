# Feature Slice Template

Use this template for new features and for strangler-style migrations of existing flows. Keep the first slice small: move workflow orchestration behind a controller while preserving existing ViewModel bindable state and UX.

## Feature Slice Checklist

- Create `Features/<FeatureName>/` with module, controller, models, commands, and tests.
- Wire module registration at the current composition point.
- Keep ViewModel orchestration as delegation to the controller.
- Put side effects behind explicit commands; do not start writes/downloads/installs from passive events.
- Add or extend one architecture guard only when the boundary could regress.
- Add two tests: controller happy path and one regression tripwire.

For a scaffold, run:

```powershell
.\tools\New-FeatureSlice.ps1 -Name Update
.\tools\New-FeatureSlice.ps1 -Name Update -WithArchitectureTest
```

## Folder Layout

```text
Features/<FeatureName>/
  <FeatureName>FeatureModule.cs
  I<FeatureName>Controller.cs
  <FeatureName>WorkflowController.cs
  Commands/
    <Verb><Object>Command.cs
  Models/
    <FeatureName>Result.cs

LSPDFRManager.Tests/
  <FeatureName>ControllerTests.cs
  <FeatureName>ArchitectureTests.cs
```

Only add feature-local `Views/` or `ViewModels/` folders when a view is new or when an existing view is intentionally migrated. Do not move existing XAML just to satisfy the template.

## Copy/Paste Skeleton

```csharp
using LSPDFRManager.Core.Features;

namespace LSPDFRManager.Features.Example;

public interface IExampleController : IFeatureController
{
    Task<ExampleResult> RunAsync(ExampleRequest request, CancellationToken cancellationToken = default);
}
```

```csharp
using LSPDFRManager.Core.Commands;

namespace LSPDFRManager.Features.Example;

public sealed class ExampleWorkflowController : IExampleController
{
    public string FeatureKey => "Example";
    public IReadOnlyDictionary<string, IAppCommand> Commands { get; } =
        new Dictionary<string, IAppCommand>();

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<ExampleResult> RunAsync(ExampleRequest request, CancellationToken cancellationToken = default)
    {
        // Orchestrate services here. ViewModels should only delegate and update bindable state.
        return Task.FromResult(new ExampleResult());
    }
}
```

```csharp
using LSPDFRManager.Core.Features;

namespace LSPDFRManager.Features.Example;

public sealed class ExampleFeatureModule : IFeatureModule
{
    public ExampleFeatureModule()
    {
        var controller = new ExampleWorkflowController();
        Controller = controller;
        ViewModel = new ExampleViewModel(controller);
    }

    public string Key => "Example";
    public object ViewModel { get; }
    public IFeatureController Controller { get; }
}
```

## How To Add A Feature

1. Create `Features/<FeatureName>/`.
2. Define `I<FeatureName>Controller` with explicit async methods for user-intent workflows.
3. Implement `<FeatureName>WorkflowController` and keep multi-step service orchestration there.
4. Keep ViewModels responsible for bindable state, validation display, and command delegation only.
5. Register or manually construct `<FeatureName>FeatureModule` at the existing composition point.
6. Add controller tests for workflow decisions and ViewModel tests for state/binding behavior.
7. Add or update architecture guard tests when the feature introduces a new side-effect boundary.

## Guard Test Strategy

Guard tests should block bad dependency directions and hidden side effects without hard-coding today should-have class names.

- Prefer boundary rules: ViewModels cannot call `InstallQueue.Enqueue`, installer services, filesystem writes, or network download APIs directly.
- Prefer ownership rules: archive library types stay behind adapter/service boundaries.
- Prefer lifecycle rules: singleton event subscriptions in lifecycle-owned objects must be unsubscribed in `Dispose`.
- Avoid assertions like "InstallViewModel must reference IInstallController"; they are brittle during safe renames or slice splits.
- Add a guard only when a mistake would be easy to reintroduce and expensive to catch manually.

## UI Smoke Run

Use this for event-heavy flows before merge when a desktop UI environment is available:

- Download a ZIP in WebView and confirm it stages only: no install, no queue.
- Select/detect a ZIP, including high-confidence archives, and confirm it stages only.
- Click confirm/install and verify it enqueues exactly once and installs once.
- Close, navigate away, reopen Browse and Install views; confirm no duplicate staging, prompts, or subscriptions.
- Record the result in the PR: `UI smoke run: done by <name> on <yyyy-mm-dd>` or `not run: no WPF/WebView UI available`.

## Next Slice Selection

Choose the flow with the highest rate of change plus the most event-driven coupling. Score candidates by churn, coupling, event side-effect risk, testability, and payoff.

- First candidate: update checking/download management, especially if release checks or downloads change often.
- Second candidate: mod library browsing/search filtering if it has frequent UI behavior churn.
- Tie-breaker: migrate bridges and event-heavy flows before straightforward form/state features.

## Implementation Notes

- Controllers own orchestration and return results; ViewModels update bindable state from those results.
- Commands represent explicit user intent. Passive events may update status/progress, but should not install, enqueue, delete, or write durable state.
- Objects that subscribe to singleton events should implement `IDisposable` and detach every handler they attach.
- Prefer one attach path per lifecycle. Guard against duplicate subscriptions with an `_isDisposed` or `_subscriptionsAttached` flag only when the object can be reattached.
- Marshal UI updates through `UiDispatcher` from background continuations; keep controllers UI-agnostic unless the feature already owns UI-thread integration.

## Rules

- Do not start installs, writes, downloads, or destructive actions from passive events.
- Do not let ViewModels enqueue installs or call installer services directly for new workflows.
- Do not introduce a DI container only for one feature slice.
- Do not expose SharpCompress types outside archive adapter boundaries.
- Promote feature-local code to `Services/` only after a second consumer needs it.
