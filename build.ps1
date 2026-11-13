# 编译「世界时钟.exe」—— 只需要 Windows 自带的 .NET Framework 4.x，无需安装任何东西
# 用法：在本文件夹右键 → 用 PowerShell 运行，或执行  powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$fw   = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
if (-not (Test-Path "$fw\csc.exe")) { $fw = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319" }
$csc  = "$fw\csc.exe"
$src  = (Get-ChildItem (Join-Path $here 'src') -Filter *.cs | ForEach-Object { $_.FullName })
$ico  = Join-Path $here 'src\app.ico'
$out  = Join-Path $here '世界时钟.exe'
$refs = @(
  "/r:$fw\WPF\PresentationFramework.dll",
  "/r:$fw\WPF\PresentationCore.dll",
  "/r:$fw\WPF\WindowsBase.dll",
  "/r:$fw\System.Xaml.dll",
  "/r:System.Windows.Forms.dll",
  "/r:System.Drawing.dll",
  "/r:System.Xml.dll"
)
$common = @('/nologo', '/target:winexe', '/optimize+', '/codepage:65001', '/platform:anycpu') + $refs

# 第一步：先编译一个临时版本，用它画出程序图标 app.ico
if (-not (Test-Path $ico)) {
  $tmp = Join-Path $env:TEMP 'worldclock_icon_tmp.exe'
  & $csc @common "/out:$tmp" @src
  if ($LASTEXITCODE -ne 0) { throw '编译失败' }
  & $tmp --make-icon $ico | Out-Null
  Remove-Item $tmp -ErrorAction SilentlyContinue
}

# 第二步：带图标正式编译（如果时钟正在运行，先关掉它）
Get-Process | Where-Object { $_.Path -eq $out } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500
& $csc @common "/win32icon:$ico" "/out:$out" @src
if ($LASTEXITCODE -ne 0) { throw '编译失败' }
Write-Host "完成：$out"
