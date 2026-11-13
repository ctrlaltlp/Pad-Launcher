$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (!(Test-Path $compiler)) { throw 'The Windows .NET Framework 4 compiler is required.' }
$out = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force $out | Out-Null
$refs = @('System.dll','System.Core.dll','System.Security.dll','System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','System.Xaml.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$librarySources = @((Join-Path $PSScriptRoot 'src\Library.cs'),(Join-Path $PSScriptRoot 'src\DynamicRules.cs'),(Join-Path $PSScriptRoot 'src\ExternalLibraries.cs'),(Join-Path $PSScriptRoot 'src\ArtworkService.cs'),(Join-Path $PSScriptRoot 'src\StartupSetting.cs'),(Join-Path $PSScriptRoot 'src\PlayniteImport.cs'),(Join-Path $PSScriptRoot 'src\GogLibrary.cs'),(Join-Path $PSScriptRoot 'src\EaLibrary.cs'),(Join-Path $PSScriptRoot 'src\SteamShortcutImport.cs'),(Join-Path $PSScriptRoot 'src\UbisoftLibrary.cs'),(Join-Path $PSScriptRoot 'src\RockstarLibrary.cs'),(Join-Path $PSScriptRoot 'src\EmulatorLibrary.cs'),(Join-Path $PSScriptRoot 'src\EmulatorImport.cs'),(Join-Path $PSScriptRoot 'src\EmulatorSqlite.cs'))
$librarySources += (Join-Path $PSScriptRoot 'src\BattleNetLibrary.cs')
$appSources = @((Join-Path $PSScriptRoot 'src\App.cs'),(Join-Path $PSScriptRoot 'src\PlayStationInput.cs'),(Join-Path $PSScriptRoot 'src\EmulatorSettings.cs'),(Join-Path $PSScriptRoot 'src\ListTransitions.cs'),(Join-Path $PSScriptRoot 'src\EmulatorImportSettings.cs'))
$appSources += (Join-Path $PSScriptRoot 'src\ListToolsSettings.cs')
$appSources += (Join-Path $PSScriptRoot 'src\UpdateSettings.cs')
$librarySources += (Join-Path $PSScriptRoot 'src\AppUpdates.cs')
$refs += '/resource:' + (Join-Path $PSScriptRoot 'src\SettingsTheme.xaml') + ',SettingsTheme.xaml'
$artworkTest = $args -contains '-ArtworkTest'
$firstStartupTest = $args -contains '-FirstStartupTest'
$executableName = if($firstStartupTest) { 'PadLauncher.FirstStartupTest' } elseif($artworkTest) { 'PadLauncher.ArtworkTest' } else { 'PadLauncher' }
$defines = if($firstStartupTest) { @('/define:FIRST_START_TEST') } elseif($artworkTest) { @('/define:ARTWORK_TEST') } else { @() }
$stagedExecutable = Join-Path $out ($executableName + '.build.exe')
$iconPath = Join-Path $PSScriptRoot 'assets\PadLauncher.ico'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ $defines ('/win32manifest:' + (Join-Path $PSScriptRoot 'src\App.manifest')) ('/win32icon:' + $iconPath) ('/resource:' + $iconPath + ',PadLauncher.ico') ('/out:' + $stagedExecutable) $refs $librarySources $appSources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$builtExecutable = Get-Item $stagedExecutable
$newestSource = @($librarySources + $appSources + (Join-Path $PSScriptRoot 'src\App.manifest')) | Get-Item | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
if ($builtExecutable.LastWriteTimeUtc -lt $newestSource.LastWriteTimeUtc) { throw 'The executable is older than the source. Close any running launcher and rebuild.' }
if ($args -contains '-Test') {
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

    & $compiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $out 'UbisoftTests.exe')) $refs $librarySources (Join-Path $PSScriptRoot 'tests\UbisoftTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Ubisoft test build failed.' }
    & (Join-Path $out 'UbisoftTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Ubisoft integration tests failed.' }
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
    & $compiler /nologo /target:exe /platform:x64 ('/out:' + (Join-Path $out 'PlayniteTests.exe')) $refs $librarySources (Join-Path $PSScriptRoot 'tests\PlayniteTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Playnite test build failed.' }
    & (Join-Path $out 'PlayniteTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Playnite import tests failed.' }
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
}
$installedExecutable = Join-Path $out ($executableName + '.exe')
try { Copy-Item -LiteralPath $stagedExecutable -Destination $installedExecutable -Force }
catch { throw "Build completed, but Windows could not replace $executableName.exe. Exit that version completely and run build.ps1 again. $($_.Exception.Message)" }
if ((Get-FileHash -LiteralPath $stagedExecutable).Hash -ne (Get-FileHash -LiteralPath $installedExecutable).Hash) { throw 'The installed executable does not match the tested build.' }
Write-Output "Installed and verified $installedExecutable"
