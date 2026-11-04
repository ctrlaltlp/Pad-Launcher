$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (!(Test-Path $compiler)) { throw 'The Windows .NET Framework 4 compiler is required.' }
$out = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force $out | Out-Null
$refs = @('System.dll','System.Core.dll','System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','System.Xaml.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$librarySources = @((Join-Path $PSScriptRoot 'src\Library.cs'),(Join-Path $PSScriptRoot 'src\DynamicRules.cs'))
$appSources = @((Join-Path $PSScriptRoot 'src\App.cs'),(Join-Path $PSScriptRoot 'src\PlayStationInput.cs'))
$executableName = 'SteamPadLauncher'
$stagedExecutable = Join-Path $out ($executableName + '.build.exe')
$iconPath = Join-Path $PSScriptRoot 'assets\PadLauncher.ico'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ ('/win32manifest:' + (Join-Path $PSScriptRoot 'src\App.manifest')) ('/win32icon:' + $iconPath) ('/resource:' + $iconPath + ',PadLauncher.ico') ('/out:' + $stagedExecutable) $refs $librarySources $appSources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$builtExecutable = Get-Item $stagedExecutable
$newestSource = @($librarySources + $appSources + (Join-Path $PSScriptRoot 'src\App.manifest')) | Get-Item | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
if ($builtExecutable.LastWriteTimeUtc -lt $newestSource.LastWriteTimeUtc) { throw 'The executable is older than the source. Close any running launcher and rebuild.' }
$helperSources=@((Join-Path $PSScriptRoot 'companion\TapLauncher\TapLauncher.cs'),(Join-Path $PSScriptRoot 'companion\TapLauncher\Settings.cs'))
& $compiler /nologo /target:winexe /platform:anycpu /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.Linq.dll ('/out:' + (Join-Path $out 'TapLauncher.exe')) $helperSources
if ($LASTEXITCODE -ne 0) { throw 'TapLauncher build failed.' }
if ($args -contains '-Test') {
    & (Join-Path $out 'TapLauncher.exe') --self-test | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'TapLauncher tests failed. See bin\TapLauncher.log.' }
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
