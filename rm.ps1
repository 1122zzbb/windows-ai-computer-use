# rm.ps1 —— RealMouse 命令行入口
# 用法示例：
#   powershell -File rm.ps1 screen
#   powershell -File rm.ps1 windows
#   powershell -File rm.ps1 focus -title "记事本"
#   powershell -File rm.ps1 click -x 800 -y 400
#   powershell -File rm.ps1 clickwin -title "记事本" -rx 50% -ry 50%
#   powershell -File rm.ps1 type -text "hello"
#   powershell -File rm.ps1 shot -path C:\tmp\a.png

param(
  [Parameter(Position=0)][string]$Cmd = "help",
  [string]$Title,
  [IntPtr]$Hwnd = [IntPtr]::Zero,
  [int]$X = -1,
  [int]$Y = -1,
  [string]$Rx,
  [string]$Ry,
  [string]$Text,
  [string]$Path,
  [int]$Amount = 3,
  [int]$Duration = 220,
  [switch]$NoCheck,
  [switch]$Grid,
  [int]$Cell = 200,
  [int]$CropW = 0,
  [int]$CropH = 0,
  [double]$Scale = 1.0
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\lib\RealMouse.ps1"

function Get-TargetHwnd {
  if ($Hwnd -ne [IntPtr]::Zero) { return $Hwnd }
  if ($Title) {
    $h = [RM2]::FindWindowByTitle($Title)
    if ($h -eq [IntPtr]::Zero) { throw "找不到标题包含 '$Title' 的可见窗口" }
    return $h
  }
  throw "需要 -title 或 -hwnd"
}

function Resolve-Rel([string]$expr, [int]$size, [int]$origin) {
  if (-not $expr) { return $origin }
  if ($expr.EndsWith("%")) {
    $pct = [double]$expr.TrimEnd("%")
    return [int]($origin + $size * $pct / 100.0)
  }
  return [int]([double]$expr)
}

function Invoke-HumanMove([int]$tx, [int]$ty, [int]$ms) {
  $p = [RM2]::Where()
  $sx = $p.X; $sy = $p.Y
  if ($sx -eq $tx -and $sy -eq $ty) { return }
  $steps = [Math]::Max(6, [int]($ms / 16))
  for ($i = 1; $i -le $steps; $i++) {
    $t = $i / $steps
    # ease-out cubic：起手快、收尾慢，接近真人
    $e = 1 - [Math]::Pow(1 - $t, 3)
    $x = [int]($sx + ($tx - $sx) * $e)
    $y = [int]($sy + ($ty - $sy) * $e)
    [RM2]::MoveTo($x, $y) | Out-Null
    Start-Sleep -Milliseconds 12
  }
  [RM2]::MoveTo($tx, $ty) | Out-Null
}

switch ($Cmd.ToLower()) {
  "help" {
    Write-Output @"
rm.ps1 <command> [options]

  screen                              屏幕与 DPI 信息
  where                               当前光标位置及其下窗口
  windows                             列出所有可见顶层窗口
  find     -title <子串>              查找窗口
  focus    -title <子串> | -hwnd <n>  激活窗口到前台（可靠版）
  move     -x <n> -y <n>              真实轨迹移动鼠标
  click    -x <n> -y <n>              点击绝对坐标（默认校验目标）
  clickwin -title <子串> -rx <n|n%> -ry <n|n%>   点击窗口内相对坐标
  rclick   -x <n> -y <n>              右键点击
  dclick   -x <n> -y <n>              双击
  wheel    -amount <n>                滚轮（正上负下）
  type     -text <内容> [-title <子串>]  输入文本（中文走剪贴板）
  key      -text <^z|{ENTER}|{TAB}> [-]    发送按键/组合键
  shot     -path <文件> [-grid] [-scale 0.5]   截图（物理像素，坐标 1:1）
  crop     -x <n> -y <n> -w <n> -h <n> -scale <倍> -path <文件>   局部放大
  desk                                列出桌面图标级窗口（诊断用）
"@
  }
  "screen" {
    Write-Output ("系统度量   : " + [RM2]::SysW() + " x " + [RM2]::SysH())
    Write-Output ("物理像素   : " + [RM2]::PhysW() + " x " + [RM2]::PhysH())
    Write-Output ("缩放比例   : " + ([Math]::Round([RM2]::PhysW() / [Math]::Max(1,[RM2]::SysW()), 2)))
    Write-Output ("焦点窗口   : " + [RM2]::Describe([RM2]::GetForegroundWindow()))
  }
  "where" {
    Write-Output ("光标   : " + [RM2]::CursorText())
    $p = [RM2]::Where()
    Write-Output ("所在窗口: " + [RM2]::AtPoint($p.X, $p.Y))
  }
  "windows" {
    $ws = [RM2]::Windows()
    foreach ($w in $ws) {
      if (-not $w.foreground) { continue }
      Write-Output ("<< hwnd={0} pid={1} rect=({2},{3})-({4},{5}) {6}x{7} topmost={8} min={9} class={10} title={11}" -f `
        $w.hwnd, $w.pid, $w.left, $w.top, $w.right, $w.bottom, $w.Width, $w.Height, $w.topmost, $w.minimized, $w.cls, $w.title)
    }
    foreach ($w in $ws) {
      if ($w.foreground) { continue }
      Write-Output ("   hwnd={0} pid={1} rect=({2},{3})-({4},{5}) {6}x{7} topmost={8} min={9} class={10} title={11}" -f `
        $w.hwnd, $w.pid, $w.left, $w.top, $w.right, $w.bottom, $w.Width, $w.Height, $w.topmost, $w.minimized, $w.cls, $w.title)
    }
  }
  "windows_old" {
    $ws = [RM2]::Windows()
    $list = New-Object System.Collections.ArrayList
    foreach ($w in $ws) { [void]$list.Add($w) }
    $sorted = $list | Sort-Object -Property @{Expression={ if ($_.foreground) { 0 } else { 1 } }}
    foreach ($w in $sorted) {
      $mark = if ($w.foreground) { "<<" } else { "  " }
      Write-Output ("{0} hwnd={1,-10} pid={2,-7} rect=({3},{4})-({5},{6}) {7}x{8} topmost={9} min={10} class={11} title={12}" -f `
        $mark, $w.hwnd, $w.pid, $w.left, $w.top, $w.right, $w.bottom, $w.Width, $w.Height, $w.topmost, $w.minimized, $w.cls, $w.title)
    }
  }
  "find" {
    $h = Get-TargetHwnd
    Write-Output ([RM2]::Describe($h))
  }
  "focus" {
    $h = Get-TargetHwnd
    $ok = [RM2]::ForceForeground($h)
    Start-Sleep -Milliseconds 350
    Write-Output ("激活结果: " + $ok + "  当前焦点: " + [RM2]::Describe([RM2]::GetForegroundWindow()))
  }
  "move" {
    if ($X -lt 0 -or $Y -lt 0) { throw "需要 -x 和 -y" }
    Invoke-HumanMove $X $Y $Duration
    Write-Output ("移动到: " + [RM2]::CursorText())
  }
  "click" {
    if ($X -lt 0 -or $Y -lt 0) { throw "需要 -x 和 -y" }
    if ($Title -or $Hwnd -ne [IntPtr]::Zero) {
      $h = Get-TargetHwnd
      [RM2]::ForceForeground($h) | Out-Null
      Start-Sleep -Milliseconds 300
    }
    # 姿态检查：先悬停，再确认目标，再按下
    Invoke-HumanMove $X $Y $Duration
    Start-Sleep -Milliseconds 120
    $target = [RM2]::AtPoint($X, $Y)
    Write-Output ("目标窗口: " + $target)
    if (-not $NoCheck) {
      $fg = [RM2]::GetForegroundWindow()
      $root = [RM2]::RootAt($X, $Y)
      if ($root -ne $fg -and $fg -ne [IntPtr]::Zero) {
        Write-Output ("WARN: 点击位置不在焦点窗口内，正在尝试激活该窗口")
        [RM2]::ForceForeground($root) | Out-Null
        Start-Sleep -Milliseconds 250
      }
    }
    [RM2]::LeftDown(); Start-Sleep -Milliseconds 70; [RM2]::LeftUp()
    Start-Sleep -Milliseconds 150
    Write-Output "click done"
  }
  "clickwin" {
    $h = Get-TargetHwnd
    [RM2]::ForceForeground($h) | Out-Null
    Start-Sleep -Milliseconds 400
    $r = New-Object RMRECT
    [RM2]::GetWindowRect($h, [ref]$r) | Out-Null
    $tx = Resolve-Rel $Rx ($r.Right - $r.Left) $r.Left
    $ty = Resolve-Rel $Ry ($r.Bottom - $r.Top) $r.Top
    Write-Output ("窗口 rect=($($r.Left),$($r.Top))-($($r.Right),$($r.Bottom))  目标=($tx,$ty)")
    Invoke-HumanMove $tx $ty $Duration
    Start-Sleep -Milliseconds 150
    Write-Output ("实际命中: " + [RM2]::AtPoint($tx, $ty))
    [RM2]::LeftDown(); Start-Sleep -Milliseconds 70; [RM2]::LeftUp()
    Start-Sleep -Milliseconds 200
    Write-Output "clickwin done"
  }
  "rclick" {
    Invoke-HumanMove $X $Y $Duration
    Start-Sleep -Milliseconds 120
    [RM2]::RightDown(); Start-Sleep -Milliseconds 70; [RM2]::RightUp()
    Start-Sleep -Milliseconds 200
    Write-Output "rclick done"
  }
  "dclick" {
    Invoke-HumanMove $X $Y $Duration
    Start-Sleep -Milliseconds 120
    [RM2]::LeftDown(); Start-Sleep -Milliseconds 50; [RM2]::LeftUp()
    Start-Sleep -Milliseconds 90
    [RM2]::LeftDown(); Start-Sleep -Milliseconds 50; [RM2]::LeftUp()
    Start-Sleep -Milliseconds 200
    Write-Output "dclick done"
  }
  "wheel" {
    $p = [RM2]::Where()
    [RM2]::Wheel($Amount)
    Start-Sleep -Milliseconds 350
    Write-Output ("滚轮 " + $Amount + " 于 " + $p.X + "," + $p.Y)
  }
  "type" {
    if (-not $Text) { throw "需要 -text" }
    # 有 -title 时先把目标窗口提到前台，确保按键落在正确的窗口
    if ($Title -or $Hwnd -ne [IntPtr]::Zero) {
      $h = Get-TargetHwnd
      if (-not [RM2]::ForceForeground($h)) { throw "无法激活目标窗口" }
      Start-Sleep -Milliseconds 350
    }
    if ($Text -match '[^\x00-\x7F]') {
      # 非 ASCII 走剪贴板，等价于真人 Ctrl+V
      Set-Clipboard -Value $Text
      Start-Sleep -Milliseconds 150
      [System.Windows.Forms.SendKeys]::SendWait("^v")
    } else {
      foreach ($ch in $Text.ToCharArray()) {
        [System.Windows.Forms.SendKeys]::SendWait($ch)
        Start-Sleep -Milliseconds 25
      }
    }
    Start-Sleep -Milliseconds 250
    Write-Output ("type done -> 焦点: " + [RM2]::Describe([RM2]::GetForegroundWindow()))
  }
  "key" {
    if (-not $Text) { throw "需要 -text，例如 ^z / ^v / {ENTER} / {TAB} / %{F4}" }
    if ($Title -or $Hwnd -ne [IntPtr]::Zero) {
      $h = Get-TargetHwnd
      [RM2]::ForceForeground($h) | Out-Null
      Start-Sleep -Milliseconds 300
    }
    [System.Windows.Forms.SendKeys]::SendWait($Text)
    Start-Sleep -Milliseconds 300
    Write-Output ("key sent: " + $Text)
  }
  "shot" {
    $w = [RM2]::PhysW(); $h = [RM2]::PhysH()
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen(0, 0, 0, 0, (New-Object System.Drawing.Size $w, $h))
    if ($Grid) {
      $font = New-Object System.Drawing.Font("Consolas", 20)
      $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(200, 255, 0, 0))
      $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(90, 255, 0, 0)), 1
      for ($gx = 0; $gx -lt $w; $gx += $Cell) {
        $g.DrawLine($pen, $gx, 0, $gx, $h)
        $g.DrawString("$gx", $font, $brush, ($gx + 4), 4)
      }
      for ($gy = 0; $gy -lt $h; $gy += $Cell) {
        $g.DrawLine($pen, 0, $gy, $w, $gy)
        $g.DrawString("$gy", $font, $brush, 4, ($gy + 2))
      }
      $pen.Dispose(); $brush.Dispose(); $font.Dispose()
    }
    $g.Dispose()
    # 可选缩放输出（便于人/模型看图，坐标需按 Scale 换算回去）
    if ($Scale -lt 1.0) {
      $nw = [int]([Math]::Max(1, $w * $Scale)); $nh = [int]([Math]::Max(1, $h * $Scale))
      $small = New-Object System.Drawing.Bitmap $nw, $nh
      $gs = [System.Drawing.Graphics]::FromImage($small)
      $gs.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
      $gs.DrawImage($bmp, 0, 0, $nw, $nh)
      $gs.Dispose(); $bmp.Dispose(); $bmp = $small
      Write-Output "# 已缩放到 ${nw}x${nh}，图上坐标 × $([Math]::Round(1/$Scale,3)) = 物理坐标"
    }
    if (-not $Path) { $Path = Join-Path $env:TEMP ("rm_" + (Get-Date -Format "HHmmss") + ".png") }
    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output $Path
  }
  "crop" {
    # 裁剪一块区域并放大，方便看细节：-x -y -w -h -scale -path
    if ($X -lt 0 -or $Y -lt 0) { throw "需要 -x -y（左上角）" }
    if ($CropW -le 0) { $CropW = 600 }
    if ($CropH -le 0) { $CropH = 400 }
    if ($Scale -le 0) { $Scale = 1.5 }
    $pw = [RM2]::PhysW(); $ph = [RM2]::PhysH()
    $full = New-Object System.Drawing.Bitmap $pw, $ph
    $gf = [System.Drawing.Graphics]::FromImage($full)
    $gf.CopyFromScreen(0, 0, 0, 0, (New-Object System.Drawing.Size $pw, $ph))
    $gf.Dispose()
    $nw = [int]($CropW * $Scale); $nh = [int]($CropH * $Scale)
    $crop = New-Object System.Drawing.Bitmap $nw, $nh
    $gc = [System.Drawing.Graphics]::FromImage($crop)
    $gc.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gc.DrawImage($full, (New-Object System.Drawing.Rectangle 0, 0, $nw, $nh), (New-Object System.Drawing.Rectangle $X, $Y, $CropW, $CropH), [System.Drawing.GraphicsUnit]::Pixel)
    $gc.Dispose(); $full.Dispose()
    if (-not $Path) { $Path = Join-Path $env:TEMP ("rm_crop_" + (Get-Date -Format "HHmmss") + ".png") }
    $crop.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $crop.Dispose()
    Write-Output "# 区域 ($X,$Y) ${W}x${H} 放大 $Scale 倍"
    Write-Output $Path
  }
  "desk" {
    foreach ($w in [RM2]::Windows()) {
      if ($w.Width -gt 200 -and $w.Height -gt 100) {
        Write-Output ("hwnd={0} pid={1} {2}x{3} fg={4} class={5} title={6}" -f $w.hwnd, $w.pid, $w.Width, $w.Height, $w.foreground, $w.cls, $w.title)
      }
    }
  }
  default { Write-Output "未知命令: $Cmd（用 help 查看）" }
}

