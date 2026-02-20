# Task: Restore "Backend ready" indicator next to Generate button

## 1. Context & Objective
The previous relocation task moved the backend dot indicator out of `MainWindow.xaml`.
However the implementation that landed in `GeneratePage.xaml` never added the dot/label
in the button row — it only shows a generic `StatusText = "Ready"` label, which gives
no clear signal that the backend is online.

**Goal:** Make it unambiguous when the backend is ready by:
1. Setting `StatusText = "Backend ready"` in `GenerateViewModel` when the backend
   comes online, and resetting it after each generation cycle.
2. Adding a small green dot + "Backend ready" label **inside the button row** in
   `GeneratePage.xaml` that is only visible when `IsBackendRunning = True`.

## 2. Files to Modify

### `src/IndexTTSStudio/ViewModels/GenerateViewModel.cs`

**Change A — Update `StatusText` when backend comes online.**
Find the `PropertyChanged` subscription (around line 61) and extend the
`IsBackendRunning` branch:

```csharp
_mainVm.PropertyChanged += (_, e) =>
{
    if (e.PropertyName == nameof(MainWindowViewModel.IsBackendRunning))
    {
        OnPropertyChanged(nameof(IsBackendRunning));
        if (_mainVm.IsBackendRunning)
            StatusText = "Backend ready";
    }
    else if (e.PropertyName == nameof(MainWindowViewModel.BackendStatus))
        OnPropertyChanged(nameof(BackendStatus));
};
```

**Change B — Reset `StatusText` after each generation.**
In `GenerateAsync()`, modify the `finally` block to reset the label when idle:

```csharp
finally
{
    IsGenerating = false;
    if (!HasError)
        StatusText = "Backend ready";
}
```

### `src/IndexTTSStudio/Views/Pages/GeneratePage.xaml`

**Change C — Add a dedicated backend dot + label in the button row.**
Inside the `<StackPanel Orientation="Horizontal">` that contains the Generate and
Play buttons (around line 151), append after the existing `StatusText` `<TextBlock>`:

```xml
<!-- Backend ready dot -->
<StackPanel Orientation="Horizontal" VerticalAlignment="Center" Margin="6,0,0,0"
            Visibility="{Binding IsBackendRunning, Converter={StaticResource BoolToVisConverter}}">
    <Ellipse Width="8" Height="8" Fill="#69FF47" VerticalAlignment="Center" Margin="0,0,5,0" />
    <TextBlock Text="Backend ready" FontSize="11" Foreground="#69FF47" VerticalAlignment="Center" />
</StackPanel>
```

> The `BoolToVisConverter` key is already registered in `App.xaml`.

## 3. Implementation Steps
1. Apply Change A and Change B to `GenerateViewModel.cs`.
2. Apply Change C to `GeneratePage.xaml` (insert after the StatusText TextBlock,
   still inside the same horizontal StackPanel).
3. Build and confirm:
   - While backend is loading → dot hidden, progress bar shown below.
   - Once backend is ready → green dot + "Backend ready" appears next to Generate button.
   - After generation completes → StatusText resets to "Backend ready".

## 4. Completion Instructions
Update this file with a "Changelog" section detailing every file changed.

---

## Changelog

### 2026-02-20: README.md Update
- Added `yakkinator-in-action.gif` preview image
- Added Quick Start section with one-click setup instructions
- Added storage requirement notice (~16GB in %LOCALAPPDATA%)
- Made README more fun and engaging with emojis and casual language
- Improved Running section with clearer, friendlier steps
- Added friendly closing line to Troubleshooting section

### `src/IndexTTSStudio/ViewModels/GenerateViewModel.cs`
- **Change A**: Extended `IsBackendRunning` property change handler to set `StatusText = "Backend ready"` when backend comes online.
- **Change B**: Modified `GenerateAsync()` `finally` block to reset `StatusText = "Backend ready"` after successful generation.

### `src/IndexTTSStudio/Views/Pages/GeneratePage.xaml`
- **Change C**: Added green dot + "Backend ready" label in the button row (inside the horizontal StackPanel with Generate/Play buttons). The indicator uses `BoolToVisConverter` to show only when `IsBackendRunning = True`.
