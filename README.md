# 世界时钟

Windows 桌面多时区时钟，支持迷你模式、置顶显示、时间换算、倒计时提醒、主题和系统托盘。置顶模式下，时钟放在任务栏区域时会保持显示。

## 运行

下载仓库中的 `世界时钟.exe`，双击运行。需要 Windows 和 .NET Framework 4.x。

## 从源码编译

在项目目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

脚本会用 Windows 自带的 .NET Framework C# 编译器生成 `世界时钟.exe`。如果时钟正在运行，编译脚本会先关闭它。个人设置保存在 `%APPDATA%\WorldClock`。
