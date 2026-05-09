# LSPDFR Manager — New Features Implementation Guide

This document contains **15 new features** ready to implement, organized by complexity with step-by-step instructions and testing procedures.

---

## 🟢 Quick Wins (1-2 hours each, low risk)

### Feature 1: Mod Favorite Tagging

**User Story:**
Users want to mark favorite mods with a star so they appear first in the library.

**Implementation Steps:**

1. **Update Domain** (`Domain/InstalledMod.cs`):
```csharp
public bool IsFavorite { get; set; } = false;
```

2. **Add ViewModel Property** (`ViewModels/ModItemViewModel.cs`):
```csharp
public ICommand ToggleFavoriteCommand { get; }

public ModItemViewModel(InstalledMod mod)
{
    Model = mod;
    ToggleFavoriteCommand = new RelayCommand(() =>
    {
        mod.IsFavorite = !mod.IsFavorite;
        ModLibraryService.Instance.SaveProxy();
    });
}
```

3. **Update Sort Options** (`ViewModels/LibraryViewModel.cs`):
```csharp
public List<string> SortOptions { get; } =
[
    "Favorites first",  // NEW
    "Installed: Newest first",
    "Installed: Oldest first",
    "Name: A to Z",
    "Name: Z to A",
    "Author: A to Z",
    "Enabled first",
];

// In ApplyFilters method:
"Favorites first" => mods.OrderByDescending(mod => mod.IsFavorite)
    .ThenByDescending(mod => mod.InstalledAt),
```

4. **Update XAML** (`Views/Components/ModCard.xaml`):
```xaml
<!-- Add star button to mod card -->
<Button Command="{Binding ToggleFavoriteCommand}" 
        Content="{Binding Model.IsFavorite, Converter={StaticResource BoolToStar}}"
        ToolTip="Add to Favorites" />
```

**Testing Checklist:**
- [ ] Install 3 mods
- [ ] Mark mod 1 and 3 as favorite
- [ ] Switch sort to "Favorites first"
- [ ] Verify mods 1 and 3 appear at top
- [ ] Close and reopen app
- [ ] Verify favorites are persisted
- [ ] Run: `dotnet test --filter FavoriteTests`

---

### Feature 2: Mod Search History

**User Story:**
Show recent searches so users don't retype common queries.

**Implementation Steps:**

1. **Update AppConfig** (`Domain/AppConfig.cs`):
```csharp
public List<string> RecentSearches { get; set; } = [];
private const int MaxSearchHistory = 10;

public void AddSearchToHistory(string query)
{
    if (string.IsNullOrWhiteSpace(query))
        return;
    
    RecentSearches.Remove(query);  // Avoid duplicates
    RecentSearches.Insert(0, query);
    if (RecentSearches.Count > MaxSearchHistory)
        RecentSearches.RemoveAt(MaxSearchHistory);
    
    Save();
}
```

2. **Update LibraryViewModel** (`ViewModels/LibraryViewModel.cs`):
```csharp
public ObservableCollection<string> RecentSearches { get; } = [];

public LibraryViewModel()
{
    // ... existing code ...
    RefreshSearchHistory();
}

public void SearchQueryChanged(string query)
{
    SearchQuery = query;
    AppConfig.Instance.AddSearchToHistory(query);
}

private void RefreshSearchHistory()
{
    RecentSearches.Clear();
    foreach (var search in AppConfig.Instance.RecentSearches)
        RecentSearches.Add(search);
}
```

3. **Update XAML** (`Views/LibraryView.xaml`):
```xaml
<!-- Add autocomplete dropdown to search box -->
<ComboBox ItemsSource="{Binding RecentSearches}"
          Text="{Binding SearchQuery, UpdateSourceTrigger=PropertyChanged}"
          IsEditable="True"
          Margin="0,0,10,0"/>
```

**Testing Checklist:**
- [ ] Search for "ELS"
- [ ] Search for "Vehicle"
- [ ] Search for "ELS" again
- [ ] Verify dropdown shows ["ELS", "Vehicle", "ELS"] with duplicates merged to top
- [ ] Clear library search
- [ ] Click dropdown
- [ ] Verify history appears
- [ ] Close app, reopen
- [ ] Verify history persisted
- [ ] Run: `dotnet test --filter SearchHistoryTests`

---

### Feature 3: Quick Copy Mod Name

**User Story:**
Users want to easily copy a mod's name (for discussions, bug reports).

**Implementation Steps:**

1. **Add Command** (`ViewModels/ModItemViewModel.cs`):
```csharp
public ICommand CopyNameCommand { get; }

public ModItemViewModel(InstalledMod mod)
{
    Model = mod;
    CopyNameCommand = new RelayCommand(() =>
    {
        System.Windows.Forms.Clipboard.SetText($"{mod.Name} v{mod.Version}");
    });
}
```

2. **Update XAML** (`Views/Components/ModCard.xaml`):
```xaml
<!-- Add copy button next to mod name -->
<Button Command="{Binding CopyNameCommand}"
        Content="📋 Copy"
        ToolTip="Copy: [ModName] v[Version]"
        Margin="0,0,8,0"
        Padding="6,4" />
```

**Testing Checklist:**
- [ ] Install a mod with version (e.g., "ELS v1.5.0")
- [ ] Right-click mod card → Copy button
- [ ] Paste into Notepad
- [ ] Verify format: "ELS v1.5.0"
- [ ] Run: `dotnet test --filter ClipboardTests`

---

### Feature 4: Disabled Mods Counter in Sidebar

**User Story:**
Show count of disabled mods in sidebar so users know when they have inactive mods.

**Implementation Steps:**

1. **Update MainViewModel** (`ViewModels/MainViewModel.cs`):
```csharp
public int DisabledModCount
{
    get => ModLibraryService.Instance.Mods.Count(m => !m.IsEnabled);
}

public bool HasDisabledMods => DisabledModCount > 0;
```

2. **Update XAML** (`MainWindow.xaml`):
```xaml
<!-- In sidebar, near Library button -->
<TextBlock Text="{Binding DisabledModCount, Mode=OneWay}"
           Visibility="{Binding HasDisabledMods, Converter={StaticResource BoolToVis}}"
           Foreground="#F59E0B"
           Margin="0,2,0,0" />
```

**Testing Checklist:**
- [ ] Install 3 mods
- [ ] Disable 2 mods
- [ ] Verify sidebar shows "2" badge next to Library
- [ ] Enable 1 mod
- [ ] Verify badge updates to "1"
- [ ] Disable all mods
- [ ] Verify badge persists
- [ ] Run: `dotnet test --filter SidebarTests`

---

### Feature 5: Export Enabled Mods List

**User Story:**
Users want to export a simple text list of currently enabled mods for sharing.

**Implementation Steps:**

1. **Add Command** (`ViewModels/LibraryViewModel.cs`):
```csharp
public ICommand ExportEnabledModsCommand { get; }

public LibraryViewModel()
{
    // ... existing code ...
    ExportEnabledModsCommand = new RelayCommand(ExportEnabledMods);
}

private void ExportEnabledMods()
{
    var saveDialog = new SaveFileDialog
    {
        FileName = $"Mods_{DateTime.Now:yyyy-MM-dd}.txt",
        Filter = "Text files (*.txt)|*.txt"
    };

    if (saveDialog.ShowDialog() != true)
        return;

    var enabledMods = _library.Mods
        .Where(mod => mod.IsEnabled)
        .OrderBy(mod => mod.Name);

    var text = string.Join("\n", enabledMods.Select(mod =>
        $"{mod.Name} ({mod.TypeLabel}) v{mod.Version} by {mod.Author}"));

    File.WriteAllText(saveDialog.FileName, text);
}
```

2. **Update XAML** (`Views/LibraryView.xaml`):
```xaml
<!-- Add button to toolbar -->
<Button Content="Export List"
        Command="{Binding ExportEnabledModsCommand}"
        Margin="0,0,8,0" />
```

**Testing Checklist:**
- [ ] Install 5 mods (enable 3, disable 2)
- [ ] Click "Export List"
- [ ] Save as `test_mods.txt`
- [ ] Open file and verify it contains only 3 enabled mods
- [ ] Verify format: "[Name] ([Type]) v[Version] by [Author]"
- [ ] Run: `dotnet test --filter ExportTests`

---

## 🟡 Medium Features (3-6 hours, moderate risk)

### Feature 6: Mod Duplicate Detector

**User Story:**
Prevent users from accidentally installing the same mod twice.

**Implementation Steps:**

1. **Add Service** (`Services/ModDuplicateDetector.cs`):
```csharp
public class ModDuplicateDetector
{
    private readonly ModLibraryService _library = ModLibraryService.Instance;

    public bool IsProbableDuplicate(ModInfo incoming)
    {
        if (string.IsNullOrWhiteSpace(incoming.Name))
            return false;

        var incomingName = incoming.Name.ToLowerInvariant().Trim();
        
        // Exact name match
        if (_library.Mods.Any(mod => 
            mod.Name.Equals(incomingName, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Fuzzy match (e.g., "ELS v1.5" vs "ELS v1.6")
        var existingSameName = _library.Mods
            .Where(mod => mod.Name.StartsWith(incomingName.Split(' ')[0], 
                StringComparison.OrdinalIgnoreCase));
        
        return existingSameName.Any();
    }

    public InstalledMod? FindDuplicate(ModInfo incoming)
    {
        return _library.Mods.FirstOrDefault(mod =>
            mod.Name.Equals(incoming.Name, StringComparison.OrdinalIgnoreCase));
    }
}
```

2. **Update InstallViewModel** (`ViewModels/InstallViewModel.cs`):
```csharp
private readonly ModDuplicateDetector _duplicateDetector = new();

private async Task InstallAsync()
{
    // ... existing detection code ...

    var duplicate = _duplicateDetector.FindDuplicate(_detectedMod!);
    if (duplicate is not null)
    {
        var result = MessageBox.Show(
            $"You already have '{duplicate.Name}' v{duplicate.Version} installed.\n\n" +
            $"Do you want to:\n" +
            $"• Replace (uninstall old, install new)\n" +
            $"• Install as separate\n" +
            $"• Cancel",
            "Duplicate Mod Detected",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Cancel)
            return;

        if (result == MessageBoxResult.Yes)
            _library.Uninstall(duplicate.Id);
    }

    // Continue with install
}
```

**Testing Checklist:**
- [ ] Install "ELS v1.5.0"
- [ ] Try to install "ELS v1.5.0" again
- [ ] Verify dialog appears with duplicate warning
- [ ] Click "Replace"
- [ ] Verify old mod uninstalled and new one installed
- [ ] Try to install "ELS v1.5.1"
- [ ] Verify fuzzy match detected
- [ ] Run: `dotnet test --filter DuplicateDetectorTests`
- [ ] **Regression test**: Install 5 different mods, verify no false positives

---

### Feature 7: Mod Uninstall Confirmation with Details

**User Story:**
Show detailed uninstall confirmation so users know exactly what they're deleting.

**Implementation Steps:**

1. **Update Command** (`ViewModels/LibraryViewModel.cs`):
```csharp
public ICommand UninstallCommand { get; }

public LibraryViewModel()
{
    UninstallCommand = new RelayCommand(obj =>
    {
        var mod = obj as ModItemViewModel ?? SelectedMod;
        if (mod is null)
            return;

        var dialog = new UninstallConfirmationDialog(mod.Model);
        if (dialog.ShowDialog() == true)
        {
            _library.Uninstall(mod.Id);
        }
    });
}
```

2. **Create Dialog** (`Views/Dialogs/UninstallConfirmationDialog.xaml.cs`):
```csharp
public partial class UninstallConfirmationDialog : Window
{
    private readonly InstalledMod _mod;

    public UninstallConfirmationDialog(InstalledMod mod)
    {
        _mod = mod;
        InitializeComponent();
        DataContext = mod;
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
```

3. **XAML Dialog** (`Views/Dialogs/UninstallConfirmationDialog.xaml`):
```xaml
<Window Title="Confirm Uninstall" Width="400" Height="300" WindowStartupLocation="CenterScreen">
    <StackPanel Margin="20">
        <TextBlock Text="Uninstall Mod?" FontSize="16" FontWeight="Bold" Margin="0,0,0,16"/>
        
        <StackPanel Margin="0,0,0,16">
            <TextBlock Text="{Binding Name}" FontSize="14" FontWeight="SemiBold"/>
            <TextBlock Text="{Binding Author, StringFormat='by {0}'}" Foreground="#999"/>
            <TextBlock Text="{Binding InstalledFiles.Count, StringFormat='{0} files'}" Foreground="#666" FontSize="12" Margin="0,4,0,0"/>
        </StackPanel>

        <TextBlock Text="This action cannot be undone." Foreground="#F59E0B" Margin="0,0,0,16" TextWrapping="Wrap"/>

        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
            <Button Content="Cancel" IsCancel="True" Margin="0,0,8,0" Padding="16,8"/>
            <Button Content="Uninstall" Click="ConfirmButton_Click" Padding="16,8" Background="#EF4444" Foreground="White"/>
        </StackPanel>
    </StackPanel>
</Window>
```

**Testing Checklist:**
- [ ] Install 2 mods
- [ ] Right-click mod 1 → Uninstall
- [ ] Verify dialog appears showing mod name, author, file count
- [ ] Click Cancel
- [ ] Verify mod still in library
- [ ] Right-click mod 1 → Uninstall
- [ ] Click Confirm
- [ ] Verify mod removed from library and disk
- [ ] Run: `dotnet test --filter UninstallTests`

---

### Feature 8: Quick Install Buttons for Common Mod Types

**User Story:**
Show buttons for "Install LSPDFR", "Install ELS", "Install Vehicle" so users know which types to look for.

**Implementation Steps:**

1. **Update InstallViewModel** (`ViewModels/InstallViewModel.cs`):
```csharp
public List<ModTypeQuickLink> QuickLinks { get; } =
[
    new("LSPDFR Plugins", ModType.LspdfrPlugin, "https://www.lcpdfr.com/files/category/81-plugins/"),
    new("Vehicles (Add-On)", ModType.VehicleDlc, "https://www.lcpdfr.com/files/category/3-vehicles-addon/"),
    new("Vehicles (Replace)", ModType.VehicleReplace, "https://www.lcpdfr.com/files/category/2-vehicles-replace/"),
    new("ELS Lighting", ModType.Eup, "https://www.lcpdfr.com/files/category/45-els/"),
];

public ICommand BrowseQuickLinkCommand { get; }

public InstallViewModel()
{
    BrowseQuickLinkCommand = new RelayCommand(link =>
    {
        if (link is ModTypeQuickLink quickLink)
            Process.Start(new ProcessStartInfo(quickLink.Url) { UseShellExecute = true });
    });
}

public record ModTypeQuickLink(string Label, ModType Type, string Url);
```

2. **Update XAML** (`Views/InstallView.xaml`):
```xaml
<!-- Add buttons above drop zone -->
<StackPanel Orientation="Horizontal" Margin="0,0,0,12">
    <Button Content="📥 Install LSPDFR"
            Command="{Binding BrowseQuickLinkCommand}"
            CommandParameter="{Binding QuickLinks[0]}"
            Margin="0,0,8,0" />
    <Button Content="🚗 Install Vehicle Add-On"
            Command="{Binding BrowseQuickLinkCommand}"
            CommandParameter="{Binding QuickLinks[1]}"
            Margin="0,0,8,0" />
    <Button Content="💡 Install ELS"
            Command="{Binding BrowseQuickLinkCommand}"
            CommandParameter="{Binding QuickLinks[3]}" />
</StackPanel>
```

**Testing Checklist:**
- [ ] Open Install tab
- [ ] Verify 4 quick link buttons visible
- [ ] Click "Install LSPDFR"
- [ ] Verify browser opens to LSPDFR plugins category
- [ ] Click "Install Vehicle Add-On"
- [ ] Verify browser opens to vehicles category
- [ ] Run: `dotnet test --filter QuickLinksTests`

---

### Feature 9: Mod Size Display

**User Story:**
Show total size of installed mod files so users know storage usage.

**Implementation Steps:**

1. **Update InstalledMod** (`Domain/InstalledMod.cs`):
```csharp
/// <summary>Total size in bytes of all installed files.</summary>
public long TotalSizeBytes { get; set; }

public string TotalSizeDisplay
{
    get
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = TotalSizeBytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
```

2. **Update FileInstaller** (`Services/FileInstaller.cs`):
```csharp
public static async Task<InstallResult> InstallAsync(ModInfo mod, string targetRoot)
{
    // ... existing code ...
    
    // Calculate total size
    long totalSize = 0;
    foreach (var file in writtenFiles)
    {
        if (File.Exists(file))
            totalSize += new FileInfo(file).Length;
    }
    
    installed.TotalSizeBytes = totalSize;
    
    // ... rest of install ...
}
```

3. **Update XAML** (`Views/Components/ModCard.xaml` or detail panel):
```xaml
<TextBlock Text="{Binding TotalSizeDisplay, StringFormat='Size: {0}'}"
           Foreground="#999"
           FontSize="12"
           Margin="0,4,0,0" />
```

**Testing Checklist:**
- [ ] Install small mod (< 1 MB)
- [ ] Verify size displays as "XXX KB"
- [ ] Install large mod (> 100 MB)
- [ ] Verify size displays as "XX.X MB"
- [ ] Install mega mod (> 1 GB)
- [ ] Verify size displays as "X.X GB"
- [ ] Run: `dotnet test --filter FileSizeTests`

---

### Feature 10: Mod Last Played Timestamp

**User Story:**
Track when a mod was last loaded, so users know which mods they actively use.

**Implementation Steps:**

1. **Update InstalledMod** (`Domain/InstalledMod.cs`):
```csharp
/// <summary>Last time the mod was enabled.</summary>
public DateTime? LastEnabledAt { get; set; }

/// <summary>Last time the mod was disabled.</summary>
public DateTime? LastDisabledAt { get; set; }
```

2. **Update ModLibraryService** (`Services/ModLibraryService.cs`):
```csharp
public void SetEnabled(Guid id, bool enabled)
{
    InstalledMod? target = null;

    UiDispatcher.Invoke(() =>
    {
        target = Mods.FirstOrDefault(mod => mod.Id == id);
    });

    if (target is null || target.IsEnabled == enabled)
        return;

    _fileService.SetEnabled(target, enabled);
    
    // Track timestamp
    if (enabled)
        target.LastEnabledAt = DateTime.UtcNow;
    else
        target.LastDisabledAt = DateTime.UtcNow;
    
    ModUpdated?.Invoke(target);
    Save();
}
```

3. **Update ViewModel** (`ViewModels/ModItemViewModel.cs`):
```csharp
public string LastActivityDisplay =>
    Model.LastEnabledAt is DateTime lastEnabled
        ? $"Last enabled: {lastEnabled:MMM d, HH:mm}"
        : "Never enabled";
```

4. **Update XAML** (`Views/Components/ModCard.xaml`):
```xaml
<TextBlock Text="{Binding LastActivityDisplay}"
           Foreground="#666"
           FontSize="11"
           Margin="0,2,0,0" />
```

**Testing Checklist:**
- [ ] Install mod
- [ ] Verify "Never enabled" displayed
- [ ] Enable mod
- [ ] Wait 2 seconds
- [ ] Disable mod
- [ ] Verify "Last enabled: [time]" displayed
- [ ] Enable again
- [ ] Verify timestamp updated
- [ ] Close app, reopen
- [ ] Verify timestamp persisted
- [ ] Run: `dotnet test --filter TimestampTests`

---

## 🔴 Complex Features (1-2 days, high risk)

### Feature 11: Mod Load Order Management

**User Story:**
Let users drag-to-reorder mods to control LSPDFR plugin load order.

**Implementation Steps:**

1. **Add to InstalledMod** (`Domain/InstalledMod.cs`):
```csharp
/// <summary>Load order priority (lower = loads first).</summary>
public int LoadOrderPriority { get; set; } = 0;
```

2. **Create LoadOrderService** (`Services/LoadOrderService.cs`):
```csharp
public class LoadOrderService
{
    private readonly ModLibraryService _library = ModLibraryService.Instance;

    public void ReorderMods(List<Guid> orderedIds)
    {
        for (int i = 0; i < orderedIds.Count; i++)
        {
            var mod = _library.Mods.FirstOrDefault(m => m.Id == orderedIds[i]);
            if (mod is not null)
                mod.LoadOrderPriority = i;
        }
        _library.SaveProxy();
    }

    public List<InstalledMod> GetOrderedMods()
    {
        return _library.Mods
            .OrderBy(m => m.LoadOrderPriority)
            .ThenByDescending(m => m.InstalledAt)
            .ToList();
    }
}
```

3. **Update LibraryViewModel** (`ViewModels/LibraryViewModel.cs`):
```csharp
private readonly LoadOrderService _loadOrder = new();

public ICommand ReorderModsCommand { get; }

public LibraryViewModel()
{
    ReorderModsCommand = new RelayCommand(obj =>
    {
        if (obj is List<Guid> orderedIds)
            _loadOrder.ReorderMods(orderedIds);
    });
}

// Override sort to include load order
private IEnumerable<InstalledMod> ApplyFilters(IEnumerable<InstalledMod> mods)
{
    // ... existing filters ...
    
    return SelectedSort switch
    {
        "Load Order" => _loadOrder.GetOrderedMods().Where(mods.Contains),
        // ... existing sorts ...
    };
}
```

4. **Add Drag-Drop to XAML** (`Views/LibraryView.xaml`):
```xaml
<ListBox ItemsSource="{Binding FilteredMods}"
         AllowDrop="True"
         Drop="ModList_Drop"
         PreviewMouseMove="ModList_PreviewMouseMove"
         VirtualizingPanel.IsVirtualizing="True" />
```

5. **Codebehind for Drag-Drop** (`Views/LibraryView.xaml.cs`):
```csharp
private Point _startPoint;

private void ModList_PreviewMouseMove(object sender, MouseEventArgs e)
{
    if (e.LeftButton == MouseButtonState.Pressed)
    {
        _startPoint = e.GetPosition(null);
    }
}

private void ModList_Drop(object sender, DragEventArgs e)
{
    var listBox = sender as ListBox;
    var orderedMods = listBox?.Items.Cast<ModItemViewModel>()
        .Select(vm => vm.Id)
        .ToList();

    if (orderedMods is not null && DataContext is LibraryViewModel vm)
    {
        vm.ReorderModsCommand.Execute(orderedMods);
    }
}
```

**Testing Checklist:**
- [ ] Install 5 LSPDFR plugins
- [ ] Sort by "Load Order"
- [ ] Drag plugin #3 to position #1
- [ ] Verify UI updates immediately
- [ ] Drag plugin #1 to position #5
- [ ] Close app, reopen
- [ ] Verify load order persisted
- [ ] Export mod list, verify load order order shown
- [ ] Run: `dotnet test --filter LoadOrderTests`
- [ ] **Regression test**: Verify other sorts (Name A-Z, Date, etc.) still work

---

### Feature 12: Mod Dependency Tracking

**User Story:**
Let users see which plugins depend on other plugins (e.g., Police Mod requires ELS).

**Implementation Steps:**

1. **Update InstalledMod** (`Domain/InstalledMod.cs`):
```csharp
/// <summary>IDs of other mods this one requires to function.</summary>
public List<Guid> DependsOnModIds { get; set; } = [];

/// <summary>IDs of mods that depend on this one.</summary>
[JsonIgnore]
public List<Guid> DependentModIds
{
    get
    {
        var lib = ModLibraryService.Instance;
        return lib.Mods
            .Where(m => m.DependsOnModIds.Contains(Id))
            .Select(m => m.Id)
            .ToList();
    }
}
```

2. **Create DependencyValidator** (`Services/DependencyValidator.cs`):
```csharp
public class DependencyValidator
{
    private readonly ModLibraryService _library = ModLibraryService.Instance;

    public List<DependencyIssue> ValidateAll()
    {
        var issues = new List<DependencyIssue>();

        foreach (var mod in _library.Mods.Where(m => m.IsEnabled))
        {
            foreach (var depId in mod.DependsOnModIds)
            {
                var dep = _library.Mods.FirstOrDefault(m => m.Id == depId);
                if (dep is null)
                {
                    issues.Add(new DependencyIssue(
                        mod.Name,
                        "Missing Dependency",
                        $"Requires {dep?.Name ?? "unknown"} but it's not installed"));
                }
                else if (!dep.IsEnabled)
                {
                    issues.Add(new DependencyIssue(
                        mod.Name,
                        "Disabled Dependency",
                        $"Requires {dep.Name} but it's disabled"));
                }
            }
        }

        return issues;
    }
}

public record DependencyIssue(string ModName, string IssueType, string Description);
```

3. **Update LibraryViewModel** (`ViewModels/LibraryViewModel.cs`):
```csharp
public ICommand CheckDependenciesCommand { get; }
public ObservableCollection<DependencyIssue> DependencyIssues { get; } = [];

public LibraryViewModel()
{
    CheckDependenciesCommand = new RelayCommand(CheckDependencies);
}

private void CheckDependencies()
{
    var validator = new DependencyValidator();
    var issues = validator.ValidateAll();

    DependencyIssues.Clear();
    foreach (var issue in issues)
        DependencyIssues.Add(issue);

    if (issues.Count == 0)
        MessageBox.Show("All dependencies satisfied!", "OK");
}
```

4. **Add UI** (`Views/LibraryView.xaml`):
```xaml
<Button Content="Check Dependencies"
        Command="{Binding CheckDependenciesCommand}"
        Margin="0,0,8,0" />

<ItemsControl ItemsSource="{Binding DependencyIssues}"
              Margin="0,16,0,0">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Border Background="#2D1B1B" CornerRadius="6" Padding="12" Margin="0,0,0,8">
                <StackPanel>
                    <TextBlock Text="{Binding ModName}" FontWeight="Bold" Foreground="#F59E0B"/>
                    <TextBlock Text="{Binding IssueType}" Foreground="#F59E0B" FontSize="12"/>
                    <TextBlock Text="{Binding Description}" Foreground="#E5A56E" Margin="0,4,0,0" TextWrapping="Wrap"/>
                </StackPanel>
            </Border>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

**Testing Checklist:**
- [ ] Manually set mod 1 `DependsOnModIds = [mod2.Id, mod3.Id]` in JSON
- [ ] Restart app
- [ ] Click "Check Dependencies"
- [ ] Verify dialog shows 0 issues
- [ ] Disable mod 2
- [ ] Click "Check Dependencies"
- [ ] Verify issue appears: "Mod1 requires Mod2 but it's disabled"
- [ ] Uninstall mod 3
- [ ] Click "Check Dependencies"
- [ ] Verify issue appears: "Mod1 requires Mod3 but it's not installed"
- [ ] Run: `dotnet test --filter DependencyTests`

---

### Feature 13: Mod Backup Before Install

**User Story:**
Automatically backup enabled mods before installing a new one (optional).

**Implementation Steps:**

1. **Update AppConfig** (`Domain/AppConfig.cs`):
```csharp
public bool BackupBeforeInstall { get; set; } = true;
public int MaxPreInstallBackups { get; set; } = 5;
```

2. **Create PreInstallBackupService** (`Services/PreInstallBackupService.cs`):
```csharp
public class PreInstallBackupService
{
    private readonly BackupService _backup = new();

    public async Task<BackupInfo?> CreatePreInstallBackupAsync(ModInfo incomingMod)
    {
        if (!AppConfig.Instance.BackupBeforeInstall)
            return null;

        AppLogger.Info($"[PRE_INSTALL_BACKUP] Creating backup before installing {incomingMod.Name}");

        var backup = await _backup.CreateBackupAsync(
            $"before_install_{incomingMod.Name}_{DateTime.UtcNow:yyyyMMdd_HHmmss}");

        CleanOldBackups();
        return backup;
    }

    private void CleanOldBackups()
    {
        var backups = Directory.GetFiles(
            AppDataPaths.BackupsFolder,
            "before_install_*.zip")
            .OrderByDescending(f => File.GetCreationTime(f))
            .Skip(AppConfig.Instance.MaxPreInstallBackups);

        foreach (var old in backups)
            File.Delete(old);
    }
}
```

3. **Update InstallQueue** (`Core/InstallQueue.cs`):
```csharp
private readonly PreInstallBackupService _preBackup = new();

private async Task ProcessInstallAsync(QueuedInstall queued)
{
    try
    {
        var backup = await _preBackup.CreatePreInstallBackupAsync(queued.Mod);
        if (backup is not null)
            AppLogger.Info($"[BACKUP_OK] Pre-install backup: {backup.Path}");

        var result = await FileInstaller.InstallAsync(queued.Mod, gtaPath);
        // ... rest of install ...
    }
    catch (Exception ex)
    {
        // ... error handling ...
    }
}
```

4. **Update Settings UI** (`Views/SettingsView.xaml`):
```xaml
<CheckBox Content="Backup Enabled Mods Before Installing"
          IsChecked="{Binding BackupBeforeInstall}"
          Margin="0,8,0,0" />

<TextBlock Text="Maximum pre-install backups to keep:"
           Margin="0,8,0,4" />
<Spinner Value="{Binding MaxPreInstallBackups}"
         Minimum="1"
         Maximum="20"
         Margin="0,0,0,8" />
```

**Testing Checklist:**
- [ ] Enable "Backup Before Install"
- [ ] Install 3 mods and enable all
- [ ] Install new mod (should create backup automatically)
- [ ] Verify backup file created in `%APPDATA%\LSPDFRManager\Backups\`
- [ ] Install 6 more mods
- [ ] Verify only 5 most recent backups kept (others deleted)
- [ ] Disable "Backup Before Install"
- [ ] Install new mod
- [ ] Verify no backup created
- [ ] Run: `dotnet test --filter PreInstallBackupTests`

---

### Feature 14: Mod Conflict Resolution Suggestions

**User Story:**
When conflicts detected, suggest fixes (disable old, uninstall, etc.).

**Implementation Steps:**

1. **Create ConflictResolver** (`Services/ConflictResolver.cs`):
```csharp
public class ConflictResolver
{
    private readonly ModLibraryService _library = ModLibraryService.Instance;

    public List<ConflictResolution> SuggestResolutions(InstalledMod newMod, List<string> conflicts)
    {
        var suggestions = new List<ConflictResolution>();

        foreach (var conflict in conflicts)
        {
            var conflictingMod = _library.Mods
                .FirstOrDefault(m => m.InstalledFiles
                    .Any(f => f.Equals(conflict, StringComparison.OrdinalIgnoreCase)));

            if (conflictingMod is null)
                continue;

            // Suggestion 1: Disable conflicting mod
            suggestions.Add(new ConflictResolution(
                "Disable conflicting mod",
                () => _library.SetEnabled(conflictingMod.Id, false),
                "Keeps existing mod but disables it so new one can be installed",
                "safe"));

            // Suggestion 2: Uninstall conflicting mod
            suggestions.Add(new ConflictResolution(
                "Uninstall conflicting mod",
                () => _library.Uninstall(conflictingMod.Id),
                "Removes existing mod completely, allowing new installation",
                "warning"));

            // Suggestion 3: Cancel install
            suggestions.Add(new ConflictResolution(
                "Cancel install",
                () => { },
                "Keep existing mod, do not install new one",
                "info"));
        }

        return suggestions;
    }
}

public record ConflictResolution(
    string Action,
    Action Execute,
    string Description,
    string Severity);
```

2. **Update InstallViewModel** (`ViewModels/InstallViewModel.cs`):
```csharp
private readonly ConflictResolver _resolver = new();

private async Task HandleConflicts()
{
    var conflicts = _library.FindConflicts(_detectedMod!);
    
    if (conflicts.Count > 0)
    {
        var suggestions = _resolver.SuggestResolutions(_detectedMod, conflicts);
        
        var dialog = new ConflictResolutionDialog(suggestions);
        if (dialog.ShowDialog() == true)
        {
            dialog.SelectedResolution?.Execute();
        }
        else
        {
            return;  // User cancelled
        }
    }
}
```

3. **Create ConflictResolutionDialog** (`Views/Dialogs/ConflictResolutionDialog.xaml`):
```xaml
<Window Title="Resolve Conflicts" Width="500" Height="400">
    <StackPanel Margin="20">
        <TextBlock Text="File Conflicts Detected" FontSize="14" FontWeight="Bold" Margin="0,0,0,12"/>
        <TextBlock Text="Choose how to proceed:" Margin="0,0,0,16" TextWrapping="Wrap"/>
        
        <ListBox ItemsSource="{Binding Suggestions}" SelectedItem="{Binding SelectedResolution}">
            <ListBox.ItemTemplate>
                <DataTemplate>
                    <Border Padding="12" Margin="0,0,0,8" Background="#1A1A1A" CornerRadius="6">
                        <StackPanel>
                            <TextBlock Text="{Binding Action}" FontWeight="SemiBold"/>
                            <TextBlock Text="{Binding Description}" Foreground="#999" FontSize="12" Margin="0,4,0,0" TextWrapping="Wrap"/>
                        </StackPanel>
                    </Border>
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>

        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,16,0,0">
            <Button Content="Cancel" IsCancel="True" Margin="0,0,8,0" Padding="16,8"/>
            <Button Content="Apply" Click="Apply_Click" Padding="16,8" Background="#3B82F6" Foreground="White"/>
        </StackPanel>
    </StackPanel>
</Window>
```

**Testing Checklist:**
- [ ] Install mod A (Vehicle ABC)
- [ ] Try to install mod B (also uses Vehicle ABC, conflict)
- [ ] Verify conflict dialog appears with 3 suggestions
- [ ] Choose "Disable conflicting mod"
- [ ] Verify mod A disabled and mod B installed
- [ ] Try to install mod C (conflicts with both)
- [ ] Choose "Uninstall conflicting mod"
- [ ] Verify mod A uninstalled
- [ ] Try to install mod D (conflict)
- [ ] Choose "Cancel install"
- [ ] Verify install cancelled
- [ ] Run: `dotnet test --filter ConflictResolutionTests`

---

### Feature 15: Mod Comment/Annotation System

**User Story:**
Let users add timestamped comments to mods for tracking issues, testing notes.

**Implementation Steps:**

1. **Update InstalledMod** (`Domain/InstalledMod.cs`):
```csharp
public List<ModComment> Comments { get; set; } = [];

public record ModComment(
    Guid Id,
    string Author,
    string Text,
    DateTime CreatedAt,
    DateTime? UpdatedAt = null);
```

2. **Create CommentsService** (`Services/ModCommentsService.cs`):
```csharp
public class ModCommentsService
{
    private readonly ModLibraryService _library = ModLibraryService.Instance;

    public void AddComment(Guid modId, string text)
    {
        var mod = _library.Mods.FirstOrDefault(m => m.Id == modId);
        if (mod is null)
            return;

        var comment = new ModComment(
            Guid.NewGuid(),
            Environment.UserName,
            text,
            DateTime.UtcNow);

        mod.Comments.Add(comment);
        _library.SaveProxy();
        AppLogger.Info($"[COMMENT_ADDED] Mod: {mod.Name}, Author: {comment.Author}");
    }

    public void DeleteComment(Guid modId, Guid commentId)
    {
        var mod = _library.Mods.FirstOrDefault(m => m.Id == modId);
        if (mod is null)
            return;

        mod.Comments.RemoveAll(c => c.Id == commentId);
        _library.SaveProxy();
    }

    public void UpdateComment(Guid modId, Guid commentId, string newText)
    {
        var mod = _library.Mods.FirstOrDefault(m => m.Id == modId);
        if (mod is null)
            return;

        var comment = mod.Comments.FirstOrDefault(c => c.Id == commentId);
        if (comment is not null)
        {
            mod.Comments[mod.Comments.IndexOf(comment)] = comment with
            {
                Text = newText,
                UpdatedAt = DateTime.UtcNow
            };
            _library.SaveProxy();
        }
    }
}
```

3. **Update ModItemViewModel** (`ViewModels/ModItemViewModel.cs`):
```csharp
public ObservableCollection<ModComment> Comments { get; } = [];
public string NewComment { get; set; } = "";

public ICommand AddCommentCommand { get; }
public ICommand DeleteCommentCommand { get; }

public ModItemViewModel(InstalledMod mod)
{
    Model = mod;
    
    AddCommentCommand = new RelayCommand(
        () => AddComment(),
        () => !string.IsNullOrWhiteSpace(NewComment));

    DeleteCommentCommand = new RelayCommand(comment =>
    {
        if (comment is ModComment c)
            DeleteComment(c.Id);
    });

    RefreshComments();
}

private void AddComment()
{
    var service = new ModCommentsService();
    service.AddComment(Model.Id, NewComment);
    NewComment = "";
    RefreshComments();
}

private void DeleteComment(Guid commentId)
{
    var service = new ModCommentsService();
    service.DeleteComment(Model.Id, commentId);
    RefreshComments();
}

private void RefreshComments()
{
    Comments.Clear();
    foreach (var comment in Model.Comments.OrderByDescending(c => c.CreatedAt))
        Comments.Add(comment);
}
```

4. **Update XAML** (`Views/Components/ModDetailsPanel.xaml`):
```xaml
<StackPanel Margin="0,16,0,0">
    <TextBlock Text="Comments" Style="{StaticResource DetailLabel}"/>
    
    <ListBox ItemsSource="{Binding Comments}" Margin="0,8,0,12">
        <ListBox.ItemTemplate>
            <DataTemplate>
                <Border Background="#1A1A1A" CornerRadius="6" Padding="12" Margin="0,0,0,8">
                    <StackPanel>
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="{Binding Author}" FontWeight="Bold" Foreground="#3B82F6"/>
                            <TextBlock Text="{Binding CreatedAt, StringFormat=' — {0:MMM d, HH:mm}'}" 
                                       Foreground="#666" Margin="8,0,0,0"/>
                            <Button Content="×" Command="{Binding DeleteCommentCommand}"
                                    CommandParameter="{Binding}"
                                    HorizontalAlignment="Right"
                                    Background="Transparent" Foreground="#999"/>
                        </StackPanel>
                        <TextBlock Text="{Binding Text}" Foreground="#CCC" Margin="0,4,0,0" TextWrapping="Wrap"/>
                        <TextBlock Text="{Binding UpdatedAt, StringFormat='(edited {0:MMM d})'}"
                                   Foreground="#555" FontSize="11" Margin="0,4,0,0"
                                   Visibility="{Binding UpdatedAt, Converter={StaticResource NullToVisibility}}"/>
                    </StackPanel>
                </Border>
            </DataTemplate>
        </ListBox.ItemTemplate>
    </ListBox>

    <TextBox x:Name="CommentInput"
             Text="{Binding NewComment, UpdateSourceTrigger=PropertyChanged}"
             Placeholder="Add a comment..."
             AcceptsReturn="True"
             MinHeight="60"
             Margin="0,0,0,8" />
    
    <Button Content="Post Comment"
            Command="{Binding AddCommentCommand}"
            HorizontalAlignment="Right"
            Padding="12,6" />
</StackPanel>
```

**Testing Checklist:**
- [ ] Install a mod
- [ ] Click on mod, navigate to Comments section
- [ ] Type "Testing this mod - works great!"
- [ ] Click "Post Comment"
- [ ] Verify comment appears with author, timestamp
- [ ] Type another comment
- [ ] Verify both comments visible, newest first
- [ ] Click delete (×) on first comment
- [ ] Verify comment removed
- [ ] Close app, reopen
- [ ] Verify comments persisted
- [ ] Run: `dotnet test --filter CommentsTests`

---

## 📋 Implementation Workflow

### For Each Feature:

1. **Create Branch:**
   ```bash
   git checkout -b feature/[feature-name]
   ```

2. **Implement Code:**
   - Add domain models
   - Add services
   - Add ViewModel logic
   - Add XAML UI
   - Add unit tests

3. **Run Tests:**
   ```bash
   dotnet test --filter [FeatureName]Tests
   dotnet test  # Run all tests
   ```

4. **Manual Testing:**
   Follow the "Testing Checklist" for each feature
   Test edge cases and error paths

5. **Commit:**
   ```bash
   git add .
   git commit -m "feat: add [Feature Name]

   - [Implementation detail 1]
   - [Implementation detail 2]
   
   Fixes: #[issue number]"
   ```

6. **Create Pull Request:**
   - Link to issue
   - Reference testing checklist
   - Include before/after screenshots if UI change

7. **Merge:**
   - Ensure all tests pass
   - Get code review
   - Squash commits if needed
   - Merge to master

---

## 🧪 Testing Framework Setup

### Create Test File Template:

```csharp
// LSPDFRManager.Tests/[FeatureName]Tests.cs

using LSPDFRManager.Domain;
using LSPDFRManager.Services;
using LSPDFRManager.ViewModels;
using Xunit;

namespace LSPDFRManager.Tests;

public class [FeatureName]Tests : IDisposable
{
    private readonly ModLibraryService _library = ModLibraryService.Instance;
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}");

    public [FeatureName]Tests()
    {
        _library.Mods.Clear();
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch { }
    }

    [Fact]
    public void [TestName]_[Condition]_[ExpectedResult]()
    {
        // Arrange
        var mod = new InstalledMod { Name = "Test Mod" };
        _library.Mods.Add(mod);

        // Act
        // ... perform action ...

        // Assert
        Assert.True(/* condition */);
    }
}
```

### Run Specific Feature Tests:

```bash
# Run all tests for Feature 1
dotnet test --filter FavoriteTests

# Run test by name
dotnet test --filter FavoriteTests.ToggleFavorite_EnabledMod_MarksAsFavorite

# Run with verbose output
dotnet test --filter FavoriteTests -v detailed

# Run all tests
dotnet test

# Run tests and show code coverage
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover
```

---

## 🚀 Release Checklist

Before releasing a version with new features:

```
Pre-Release:
☐ All features implemented
☐ All unit tests passing (dotnet test)
☐ All manual testing checklists completed
☐ No regression in existing features
☐ Code reviewed
☐ Documentation updated (README, CLAUDE.md)

Build:
☐ dotnet restore LSPDFRManager.sln
☐ dotnet build LSPDFRManager.sln --configuration Release
☐ dotnet test --configuration Release
☐ dotnet publish LSPDFRManager.csproj -c Release -r win-x64 --self-contained false -o publish/vX.Y.Z

Release:
☐ Update version in .csproj
☐ Create RELEASE_vX.Y.Z.md
☐ Tag release: git tag vX.Y.Z
☐ Push: git push origin master --tags
☐ GitHub Actions builds release
☐ Verify asset downloads work
☐ Post release notes
```

---

## 📊 Feature Priority Matrix

| Feature | Complexity | Impact | Priority |
|---------|-----------|--------|----------|
| 1. Favorite Tagging | Low | Medium | High (Quick Win) |
| 2. Search History | Low | Low | Medium |
| 3. Copy Mod Name | Very Low | Low | Low |
| 4. Disabled Counter | Low | Medium | High |
| 5. Export Mods List | Low | Medium | High |
| 6. Duplicate Detector | Medium | High | High |
| 7. Uninstall Confirm | Medium | High | High |
| 8. Quick Install Buttons | Low | Medium | Medium |
| 9. Mod Size Display | Medium | Medium | Medium |
| 10. Last Played | Medium | Low | Low |
| 11. Load Order | High | High | High |
| 12. Dependencies | High | High | Medium |
| 13. Pre-Install Backup | High | Medium | Medium |
| 14. Conflict Resolution | High | High | High |
| 15. Comments | Medium | Medium | Low |

---

## 🎯 Next Steps

1. **Start with Feature 1 (Favorites)** — Lowest risk, high value
2. **Add Feature 6 (Duplicate Detector)** — Prevents user errors
3. **Add Feature 7 (Uninstall Confirm)** — Safety critical
4. **Continue down the list** based on user feedback

For each feature, follow the implementation steps, run the test checklist, commit to a branch, and create a PR. All tests must pass before merging.

Good luck! 🚀

