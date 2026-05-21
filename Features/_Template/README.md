# Feature Slice Template

Use `tools/New-FeatureSlice.ps1` instead of copying compilable files from this folder. This folder is documentation-only so it cannot drift into production builds.

## Generate A Slice

```powershell
.\tools\New-FeatureSlice.ps1 -Name Example
.\tools\New-FeatureSlice.ps1 -Name Example -WithArchitectureTest
```

## Generated Shape

```text
Features/<Name>/
  <Name>FeatureModule.cs
  I<Name>Controller.cs
  <Name>WorkflowController.cs
  Commands/Run<Name>Command.cs
  Models/<Name>Request.cs
  Models/<Name>Result.cs

LSPDFRManager.Tests/
  <Name>ControllerTests.cs
  <Name>ArchitectureTests.cs   // only with -WithArchitectureTest
```

## Rules

- Controllers orchestrate services and return results.
- ViewModels keep bindable state and delegate workflows to controllers.
- Commands represent explicit user intent.
- Passive events may update status/progress, but must not install, enqueue, delete, or write durable state.
- Add an architecture test only for a boundary that is easy to regress and expensive to catch manually.
