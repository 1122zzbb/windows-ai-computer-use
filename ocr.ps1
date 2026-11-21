param(
  [Parameter(Mandatory=$true, Position=0)][string]$Text,
  [switch]$Click,
  [int]$Index = 1,
  [string]$ShotPath = "",
  [int]$MinLen = 1,
  [switch]$ToRight,
  [switch]$ToLeft,
  [switch]$Below,
  [int]$Offset = 0,
  [int]$OffsetX = 0,
  [int]$OffsetY = 0,
  [string]$Type = "",
  [switch]$Tab,
  [switch]$Enter,
  [switch]$DClick,
  [switch]$TClick,
  [string]$Window = "",
  [switch]$FullScreen,
  [switch]$Parallel
)

$ErrorActionPreference = "Stop"
$RM = Join-Path $PSScriptRoot "bin\rm.exe"
if (-not (Test-Path $RM)) {
    $cmd = Get-Command rm.exe -ErrorAction SilentlyContinue
    if ($cmd) { $RM = $cmd.Source }
}
if (-not (Test-Path $RM)) {
    throw "rm.exe not found! Please place rm.exe in the bin folder or add it to PATH."
}

# 模式选择：默认走超高速窗口 ROI 模式（零磁盘 I/O），指定 -FullScreen 时走全屏内存 OCR
$cmd = if ($FullScreen) { "ocr" } else { "ocrw" }

$argsList = [System.Collections.Generic.List[string]]::new()
$argsList.Add($cmd)
$argsList.Add($Text)

if ($Click) { $argsList.Add("--click") }
if ($DClick) { $argsList.Add("--dclick") }
if ($TClick) { $argsList.Add("--tclick") }
if ($ToRight) { 
    $argsList.Add("--toright")
    if ($Offset -gt 0) { $argsList.Add("$Offset") }
}
if ($ToLeft) { 
    $argsList.Add("--toleft")
    if ($Offset -gt 0) { $argsList.Add("$Offset") }
}
if ($Below) { 
    $argsList.Add("--below")
    if ($Offset -gt 0) { $argsList.Add("$Offset") }
}
if ($OffsetX -ne 0) { $argsList.Add("--offsetx"); $argsList.Add("$OffsetX") }
if ($OffsetY -ne 0) { $argsList.Add("--offsety"); $argsList.Add("$OffsetY") }
if ($Type) { $argsList.Add("--type"); $argsList.Add($Type) }
if ($Tab) { $argsList.Add("--tab") }
if ($Enter) { $argsList.Add("--enter") }
if ($Window) { $argsList.Add("--window"); $argsList.Add($Window) }
if ($Index -gt 1) { $argsList.Add("--index"); $argsList.Add("$Index") }
if ($Parallel) { $argsList.Add("--parallel") }

& $RM @argsList
