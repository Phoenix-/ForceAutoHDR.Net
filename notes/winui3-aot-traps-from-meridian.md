# NativeAOT + WinUI 3 traps already paid for in Meridian (D:\Work\!PetProjects\Calendar)

Carried over so they are not rediscovered here. Details and repro notes live in
`Calendar/notes/` and Meridian's memory; only the rule is repeated.

- **`ItemsControl.ItemsSource = ObservableCollection<T>` crashes under AOT** with
  `E_INVALIDARG` (0x80070057) / fail-fast inside `set_ItemsSource`: CsWinRT trims the
  `IObservableVector<IInspectable>` adapter. Meridian's fix: don't use `ItemsSource`,
  sync `Items` manually from `CollectionChanged`. Before copying that, try the AOT-era
  alternatives first: `[GeneratedBindableCustomProperty]` on the item type and
  `x:Bind` to a strongly-typed collection; verify on the published exe, not in Debug.
  ✅ **Does not reproduce here (2026-09-07, WinAppSDK 2.4 / .NET 10).**
  `ItemsSource="{x:Bind ViewModel.Profiles}"` over an `ObservableCollection<ProfileViewModel>`
  works in the published AOT exe — 29 rows, live add/remove, two-way `ToggleSwitch` bindings.
  No `[GeneratedBindableCustomProperty]` was needed. What *is* needed is the next bullet.
- **Every class implementing a WinRT interface must be `partial`** — including a view model that
  only implements `INotifyPropertyChanged`. Otherwise `CsWinRT1028` fails the build ("implements
  WinRT interfaces but it or a parent type isn't marked partial"), because the source generator
  emits the vtable that used to be produced by reflection. This is the mechanism that makes the
  bullet above obsolete, and `CsWinRTAotWarningLevel=2` turns it into a build error rather than a
  runtime surprise.
- **`{Binding}` inside `DataTemplate` fails under AOT** (reflection). Use `x:DataType`
  + `{x:Bind}` everywhere; `CsWinRTAotWarningLevel=2` and treating WMC/IL warnings as
  errors catches most of it at build time.
- **`VisualTreeHelper` walks return null before first layout in AOT** (works in Debug).
  Name elements in XAML and reference them directly.
- **`Flyout/MenuFlyout.ShowAt` throws `ArgumentException`** for targets inside a `Canvas`;
  set `XamlRoot` from the target first.
- **Custom `Main` (`DISABLE_XAML_GENERATED_MAIN`) needs `PerMonitorV2` in app.manifest**,
  otherwise the bottom-right of the window is dead to wheel/drag at >100 % scale and text
  is blurry. The template's `app.manifest` already has it; keep it if `Main` is replaced.
- **Diagnose startup crashes via Event Log + WER**, not the app log: faulting module
  `Microsoft.ui.xaml.dll`, exception `0x802b000a` (packaged, missing activatable
  classes) or `0xC000027B` (unpackaged, missing PRI / XAML resource).
