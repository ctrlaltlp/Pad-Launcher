$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (!(Test-Path $compiler)) { throw 'The Windows .NET Framework 4 compiler is required.' }
$out = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force $out | Out-Null
$refs = @('System.dll','System.Core.dll','System.Security.dll','System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','System.Xaml.dll','System.Xml.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$librarySources = @((Join-Path $PSScriptRoot 'src\Library.cs'),(Join-Path $PSScriptRoot 'src\DynamicRules.cs'),(Join-Path $PSScriptRoot 'src\ExternalLibraries.cs'),(Join-Path $PSScriptRoot 'src\ArtworkService.cs'),(Join-Path $PSScriptRoot 'src\StartupSetting.cs'),(Join-Path $PSScriptRoot 'src\GameImport.cs'),(Join-Path $PSScriptRoot 'src\GogLibrary.cs'),(Join-Path $PSScriptRoot 'src\EaLibrary.cs'),(Join-Path $PSScriptRoot 'src\SteamShortcutImport.cs'),(Join-Path $PSScriptRoot 'src\UbisoftLibrary.cs'),(Join-Path $PSScriptRoot 'src\RockstarLibrary.cs'),(Join-Path $PSScriptRoot 'src\EmulatorLibrary.cs'),(Join-Path $PSScriptRoot 'src\EmulatorImport.cs'),(Join-Path $PSScriptRoot 'src\EmulatorSqlite.cs'))
$librarySources += (Join-Path $PSScriptRoot 'src\BattleNetLibrary.cs')
$librarySources += (Join-Path $PSScriptRoot 'src\XboxLibrary.cs')
$librarySources += (Join-Path $PSScriptRoot 'src\SteamShortcutIdentity.cs')
$librarySources += (Join-Path $PSScriptRoot 'src\LibretroArtwork.cs')
$librarySources += (Join-Path $PSScriptRoot 'src\LibretroPlaceholderSizing.cs')
$librarySources += (Join-Path $PSScriptRoot 'src\ListArtwork.cs')
$librarySources += (Join-Path $PSScriptRoot 'src\ArtworkCacheCleanup.cs')
$librarySources += (Join-Path $PSScriptRoot 'src\EmulatorTitleTags.cs')
$librarySources += (Join-Path $PSScriptRoot 'src\RetroArchListDefaults.cs')
$librarySources += (Join-Path $PSScriptRoot 'src\RetroArchCoreResolver.cs')
$librarySources += (Join-Path $PSScriptRoot 'src\PlayTime.cs')
$librarySources += (Join-Path $PSScriptRoot 'src\ControlBindings.cs')
$appSources = @((Join-Path $PSScriptRoot 'src\App.cs'),(Join-Path $PSScriptRoot 'src\PlayStationInput.cs'),(Join-Path $PSScriptRoot 'src\EmulatorSettings.cs'),(Join-Path $PSScriptRoot 'src\ListTransitions.cs'),(Join-Path $PSScriptRoot 'src\EmulatorImportSettings.cs'))
$appSources += (Join-Path $PSScriptRoot 'src\ListToolsSettings.cs')
$appSources += (Join-Path $PSScriptRoot 'src\UpdateSettings.cs')
$appSources += (Join-Path $PSScriptRoot 'src\UpdateSession.cs')
$librarySources += (Join-Path $PSScriptRoot 'src\AppUpdates.cs')
$librarySources += (Join-Path $PSScriptRoot 'src\EmulatorDetection.cs')
$appSources += (Join-Path $PSScriptRoot 'src\EmulatorDetectionSettings.cs')
$appSources += (Join-Path $PSScriptRoot 'src\SettingsAppearance.cs')
$appSources += (Join-Path $PSScriptRoot 'src\SetupWizard.cs')
$appSources += (Join-Path $PSScriptRoot 'src\LayoutPreviews.cs')
$appSources += (Join-Path $PSScriptRoot 'src\SwitchProInput.cs')
$appSources += (Join-Path $PSScriptRoot 'src\LibretroArtworkPicker.cs')
$appSources += (Join-Path $PSScriptRoot 'src\LibretroPanelLayout.cs')
$appSources += (Join-Path $PSScriptRoot 'src\ListArtworkSettings.cs')
$appSources += (Join-Path $PSScriptRoot 'src\PanelSizingSettings.cs')
$appSources += (Join-Path $PSScriptRoot 'src\GameSearch.cs')
$appSources += (Join-Path $PSScriptRoot 'src\ArcMenu.cs')
$appSources += (Join-Path $PSScriptRoot 'src\ControllerBindings.cs')
$appSources += (Join-Path $PSScriptRoot 'src\ControllerNavigationTrace.cs')
$appSources += (Join-Path $PSScriptRoot 'src\ControlSettings.cs')
$appSources += (Join-Path $PSScriptRoot 'src\PlayActivity.cs')
$appSources += (Join-Path $PSScriptRoot 'src\SettingsWindowSizing.cs')
$appSources += (Join-Path $PSScriptRoot 'src\ControlDiagram.cs')
$refs += '/resource:' + (Join-Path $PSScriptRoot 'assets\PlayStationControls.png') + ',PlayStationControls.png'
$refs += '/resource:' + (Join-Path $PSScriptRoot 'assets\XboxControls.png') + ',XboxControls.png'
$refs += '/resource:' + (Join-Path $PSScriptRoot 'assets\NintendoControls.png') + ',NintendoControls.png'
$appSources += (Join-Path $PSScriptRoot 'src\EmulatorTitleTagSettings.cs')
$refs += '/resource:' + (Join-Path $PSScriptRoot 'third_party\SDL3\SDL3.dll.gz') + ',SDL3.dll.gz'
$refs += '/resource:' + (Join-Path $PSScriptRoot 'third_party\SDL3\LICENSE.txt') + ',SDL3-LICENSE.txt'
$refs += '/resource:' + (Join-Path $PSScriptRoot 'src\SettingsTheme.xaml') + ',SettingsTheme.xaml'
$artworkTest = $args -contains '-ArtworkTest'
$firstStartupTest = $args -contains '-FirstStartupTest'
$emulatorDetectionTest = $args -contains '-EmulatorDetectionTest'
$executableName = if($emulatorDetectionTest) { 'PadLauncher.EmulatorDetectionTest' } elseif($firstStartupTest) { 'PadLauncher.FirstStartupTest' } elseif($artworkTest) { 'PadLauncher.ArtworkTest' } else { 'PadLauncher' }
$defines = if($emulatorDetectionTest) { @('/define:EMULATOR_DETECTION_TEST') } elseif($firstStartupTest) { @('/define:FIRST_START_TEST') } elseif($artworkTest) { @('/define:ARTWORK_TEST') } else { @() }
if ($args -contains '-NavigationDiagnostics') { $defines += '/define:NAVIGATION_DIAGNOSTICS' }
if ($args -contains '-FocusDiagnostics') {
    $executableName = 'PadLauncher.FocusDiagnostics'
    $defines += '/define:FOCUS_DIAGNOSTICS'
    $appSources += (Join-Path $PSScriptRoot 'src\FocusDiagnostics.cs')
}
if ($args -contains '-XboxGuideTest') {
    $executableName = 'PadLauncher.XboxGuideTest'
    $defines += '/define:XBOX_GUIDE_TEST'
}
$appSources += (Join-Path $PSScriptRoot 'src\XboxGuideInput.cs')
$stagedExecutable = Join-Path $out ($executableName + '.build.exe')
$iconPath = Join-Path $PSScriptRoot 'assets\PadLauncher.ico'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ $defines ('/win32manifest:' + (Join-Path $PSScriptRoot 'src\App.manifest')) ('/win32icon:' + $iconPath) ('/resource:' + $iconPath + ',PadLauncher.ico') ('/out:' + $stagedExecutable) $refs $librarySources $appSources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$builtExecutable = Get-Item $stagedExecutable
$newestSource = @($librarySources + $appSources + (Join-Path $PSScriptRoot 'src\App.manifest')) | Get-Item | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
if ($builtExecutable.LastWriteTimeUtc -lt $newestSource.LastWriteTimeUtc) { throw 'The executable is older than the source. Close any running launcher and rebuild.' }
if ($args -contains '-Test') {
    & $compiler /nologo /target:exe /platform:x64 /main:SetupWizardTests ('/out:' + (Join-Path $out 'SetupWizardTests.exe')) $refs $librarySources $appSources (Join-Path $PSScriptRoot 'tests\SetupWizardTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Setup wizard test build failed.' }
    & (Join-Path $out 'SetupWizardTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Setup wizard tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $out 'EmulatorDetectionTests.exe')) $refs $librarySources (Join-Path $PSScriptRoot 'tests\EmulatorDetectionTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Emulator detection test build failed.' }
    & (Join-Path $out 'EmulatorDetectionTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Emulator detection tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 /main:EmulatorDetectionUiTests ('/out:' + (Join-Path $out 'EmulatorDetectionUiTests.exe')) $refs $librarySources $appSources (Join-Path $PSScriptRoot 'tests\EmulatorDetectionUiTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Emulator detection UI test build failed.' }
    & (Join-Path $out 'EmulatorDetectionUiTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Emulator detection UI tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 /main:UpdateSessionTests ('/out:' + (Join-Path $out 'UpdateSessionTests.exe')) $refs $librarySources $appSources (Join-Path $PSScriptRoot 'tests\UpdateSessionTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Update session test build failed.' }
    & (Join-Path $out 'UpdateSessionTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Update session tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 /main:HiddenGamesTests ('/out:' + (Join-Path $out 'HiddenGamesTests.exe')) $refs $librarySources $appSources (Join-Path $PSScriptRoot 'tests\HiddenGamesTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Hidden game test build failed.' }
    & (Join-Path $out 'HiddenGamesTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Hidden game tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $out 'UpdateTests.exe')) $refs $librarySources (Join-Path $PSScriptRoot 'tests\UpdateTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Update test build failed.' }
    & (Join-Path $out 'UpdateTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Update tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $out 'EmulatorImportTests.exe')) $refs $librarySources (Join-Path $PSScriptRoot 'tests\EmulatorImportTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Emulator import test build failed.' }
    & (Join-Path $out 'EmulatorImportTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Emulator import tests failed.' }

    & $compiler /nologo /target:exe /platform:x64 /main:ListTransitionTests ('/out:' + (Join-Path $out 'ListTransitionTests.exe')) $refs $librarySources $appSources (Join-Path $PSScriptRoot 'tests\ListTransitionTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'List transition test build failed.' }
    & (Join-Path $out 'ListTransitionTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'List transition tests failed.' }

    & $compiler /nologo /target:exe /platform:x64 /main:EmulatorUiTests ('/out:' + (Join-Path $out 'EmulatorUiTests.exe')) $refs $librarySources $appSources (Join-Path $PSScriptRoot 'tests\EmulatorUiTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Emulator UI test build failed.' }
    & (Join-Path $out 'EmulatorUiTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Emulator UI tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 /main:ListButtonLayoutTests ('/out:' + (Join-Path $out 'ListButtonLayoutTests.exe')) $refs $librarySources $appSources (Join-Path $PSScriptRoot 'tests\ListButtonLayoutTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'List button layout test build failed.' }
    & (Join-Path $out 'ListButtonLayoutTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'List button layout tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 /main:ArtworkRefreshUiTests ('/out:' + (Join-Path $out 'ArtworkRefreshUiTests.exe')) $refs $librarySources $appSources (Join-Path $PSScriptRoot 'tests\ArtworkRefreshUiTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Artwork refresh UI test build failed.' }
    & (Join-Path $out 'ArtworkRefreshUiTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Artwork refresh UI tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 /main:StartupArtworkTests ('/out:' + (Join-Path $out 'StartupArtworkTests.exe')) $refs $librarySources $appSources (Join-Path $PSScriptRoot 'tests\StartupArtworkTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Startup artwork tests failed to compile.' }
    & (Join-Path $out 'StartupArtworkTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Startup artwork tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 /main:RetroArchListDefaultsTests ('/out:' + (Join-Path $out 'RetroArchListDefaultsTests.exe')) $refs $librarySources $appSources (Join-Path $PSScriptRoot 'tests\RetroArchListDefaultsTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'RetroArch list defaults test build failed.' }
    & (Join-Path $out 'RetroArchListDefaultsTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'RetroArch list defaults tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 /main:RetroArchCoreResolverTests ('/out:' + (Join-Path $out 'RetroArchCoreResolverTests.exe')) $refs $librarySources (Join-Path $PSScriptRoot 'tests\RetroArchCoreResolverTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'RetroArch core resolution test build failed.' }
    & (Join-Path $out 'RetroArchCoreResolverTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'RetroArch core resolution tests failed.' }

    & $compiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $out 'EmulatorTests.exe')) $refs $librarySources (Join-Path $PSScriptRoot 'tests\EmulatorTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Emulator test build failed.' }
    & (Join-Path $out 'EmulatorTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Emulator tests failed.' }

    & $compiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $out 'CustomListTests.exe')) $refs $librarySources (Join-Path $PSScriptRoot 'tests\CustomListTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Custom list test build failed.' }
    & (Join-Path $out 'CustomListTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Custom list tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $out 'RockstarTests.exe')) $refs $librarySources (Join-Path $PSScriptRoot 'tests\RockstarTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Rockstar test build failed.' }
    & (Join-Path $out 'RockstarTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Rockstar tests failed.' }

    & $compiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $out 'XboxTests.exe')) $refs $librarySources (Join-Path $PSScriptRoot 'tests\XboxTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Xbox test build failed.' }
    & (Join-Path $out 'XboxTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Xbox integration tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $out 'UbisoftTests.exe')) $refs $librarySources (Join-Path $PSScriptRoot 'tests\UbisoftTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Ubisoft test build failed.' }
    & (Join-Path $out 'UbisoftTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Ubisoft integration tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $out 'StorageTests.exe')) $refs $librarySources (Join-Path $PSScriptRoot 'tests\StorageTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Storage test build failed.' }
    & (Join-Path $out 'StorageTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Storage tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $out 'LibretroArtworkTests.exe')) $refs $librarySources (Join-Path $PSScriptRoot 'tests\LibretroArtworkTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Libretro artwork test build failed.' }
    & (Join-Path $out 'LibretroArtworkTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Libretro artwork tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 /main:LibretroPickerTests ('/out:' + (Join-Path $out 'LibretroPickerTests.exe')) $refs $librarySources $appSources (Join-Path $PSScriptRoot 'tests\LibretroPickerTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Libretro artwork picker test build failed.' }
    & (Join-Path $out 'LibretroPickerTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Libretro artwork picker tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 /main:LibretroPanelTests ('/out:' + (Join-Path $out 'LibretroPanelTests.exe')) $refs $librarySources $appSources (Join-Path $PSScriptRoot 'tests\LibretroPanelTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Libretro panel test build failed.' }
    & (Join-Path $out 'LibretroPanelTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Libretro panel tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $out 'ArtworkCacheCleanupTests.exe')) $refs $librarySources (Join-Path $PSScriptRoot 'tests\ArtworkCacheCleanupTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Artwork cleanup test build failed.' }
    & (Join-Path $out 'ArtworkCacheCleanupTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Artwork cleanup tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $out 'ListArtworkTests.exe')) $refs $librarySources (Join-Path $PSScriptRoot 'tests\ListArtworkTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Per-list artwork test build failed.' }
    & (Join-Path $out 'ListArtworkTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Per-list artwork tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 /main:GameSearchTests ('/out:' + (Join-Path $out 'GameSearchTests.exe')) $refs $librarySources $appSources (Join-Path $PSScriptRoot 'tests\GameSearchTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Game search test build failed.' }
    & (Join-Path $out 'GameSearchTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Game search tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 /main:ControlBindingTests ('/out:' + (Join-Path $out 'ControlBindingTests.exe')) $refs $librarySources $appSources (Join-Path $PSScriptRoot 'tests\ControlBindingTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Control binding tests failed to compile.' }
    & (Join-Path $out 'ControlBindingTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Control binding tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $out 'SteamImportTests.exe')) $refs $librarySources (Join-Path $PSScriptRoot 'tests\SteamImportTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Steam import test build failed.' }
    & (Join-Path $out 'SteamImportTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Steam shortcut import tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $out 'EaTests.exe')) $refs $librarySources (Join-Path $PSScriptRoot 'tests\EaTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'EA test build failed.' }
    & (Join-Path $out 'EaTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'EA integration tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $out 'GogTests.exe')) $refs $librarySources (Join-Path $PSScriptRoot 'tests\GogTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'GOG test build failed.' }
    & (Join-Path $out 'GogTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'GOG adapter tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $out 'ExternalTests.exe')) $refs $librarySources (Join-Path $PSScriptRoot 'tests\ExternalTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'External test build failed.' }
    & (Join-Path $out 'ExternalTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'External integration tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $out 'ArtworkBatchTests.exe')) $refs $librarySources (Join-Path $PSScriptRoot 'tests\ArtworkBatchTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Artwork test build failed.' }
    & (Join-Path $out 'ArtworkBatchTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Artwork batch tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $out 'LibraryTests.exe')) $refs $librarySources (Join-Path $PSScriptRoot 'tests\LibraryTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    & (Join-Path $out 'LibraryTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $out 'DynamicTests.exe')) $refs $librarySources (Join-Path $PSScriptRoot 'tests\DynamicTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Dynamic test build failed.' }
    & (Join-Path $out 'DynamicTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Dynamic tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 /main:OverlayTests ('/out:' + (Join-Path $out 'OverlayTests.exe')) $refs $librarySources $appSources (Join-Path $PSScriptRoot 'tests\OverlayTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Overlay test build failed.' }
    & (Join-Path $out 'OverlayTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Overlay tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 /main:MonitorTests ('/win32manifest:' + (Join-Path $PSScriptRoot 'src\App.manifest')) ('/out:' + (Join-Path $out 'MonitorTests.exe')) $refs $librarySources $appSources (Join-Path $PSScriptRoot 'tests\MonitorTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Monitor test build failed.' }
    & (Join-Path $out 'MonitorTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Monitor tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 /main:ControllerTests ('/out:' + (Join-Path $out 'ControllerTests.exe')) $refs $librarySources $appSources (Join-Path $PSScriptRoot 'tests\ControllerTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Controller test build failed.' }
    & (Join-Path $out 'ControllerTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Controller tests failed.' }
    & $compiler /nologo /target:exe /platform:x64 /main:SwitchProTests ('/out:' + (Join-Path $out 'SwitchProTests.exe')) $refs $librarySources $appSources (Join-Path $PSScriptRoot 'tests\SwitchProTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Switch Pro test build failed.' }
    & (Join-Path $out 'SwitchProTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Switch Pro tests failed.' }
}
$installedExecutable = Join-Path $out ($executableName + '.exe')
try { Copy-Item -LiteralPath $stagedExecutable -Destination $installedExecutable -Force }
catch { throw "Build completed, but Windows could not replace $executableName.exe. Exit that version completely and run build.ps1 again. $($_.Exception.Message)" }
if ((Get-FileHash -LiteralPath $stagedExecutable).Hash -ne (Get-FileHash -LiteralPath $installedExecutable).Hash) { throw 'The installed executable does not match the tested build.' }
Write-Output "Installed and verified $installedExecutable"
