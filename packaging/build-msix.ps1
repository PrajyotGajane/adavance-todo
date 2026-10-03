param(
  [Parameter(Mandatory)] [string] $PublishDir,
  [Parameter(Mandatory)] [string] $Version,
  [string] $IdentityName = $env:MSIX_IDENTITY_NAME,
  [string] $Publisher = $env:MSIX_PUBLISHER,
  [string] $PublisherDisplayName = $env:MSIX_PUBLISHER_DISPLAY_NAME,
  [string] $Out = 'DesktopCompanion.msix'
)
$ErrorActionPreference = 'Stop'
if (-not $IdentityName) { $IdentityName = 'PLACEHOLDER.TaskAndNotes' }
if (-not $Publisher) { $Publisher = 'CN=PLACEHOLDER' }
if (-not $PublisherDisplayName) { $PublisherDisplayName = 'Publisher' }

# MSIX versions need four numeric parts; the Store reserves a trailing 0 for the revision.
$parts = @($Version.Split('-')[0].Split('.')) + @('0', '0', '0', '0')
$ver = ($parts[0..2] -join '.') + '.0'

$stage = Join-Path ([IO.Path]::GetTempPath()) "msix-stage-$([guid]::NewGuid())"
New-Item -ItemType Directory $stage | Out-Null
Copy-Item (Join-Path $PublishDir 'DesktopCompanion.exe') $stage
Copy-Item (Join-Path $PSScriptRoot 'Assets') (Join-Path $stage 'Assets') -Recurse
Remove-Item (Join-Path $stage 'Assets\StoreIcon1024.png')

(Get-Content (Join-Path $PSScriptRoot 'AppxManifest.xml') -Raw).
  Replace('__IDENTITY_NAME__', $IdentityName).
  Replace('__PUBLISHER__', $Publisher).
  Replace('__PUBLISHER_DISPLAY_NAME__', $PublisherDisplayName).
  Replace('__VERSION__', $ver) |
  Set-Content (Join-Path $stage 'AppxManifest.xml') -Encoding utf8

$mk = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter makeappx.exe |
  Where-Object FullName -like '*\x64\*' | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $mk) { throw 'makeappx.exe not found (install the Windows SDK)' }

if (Test-Path $Out) { Remove-Item $Out }
& $mk.FullName pack /d $stage /p $Out /o
if ($LASTEXITCODE) { throw 'makeappx failed' }
Remove-Item $stage -Recurse -Force
Write-Host "Created $Out (version $ver)"
