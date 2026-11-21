# Windows AI Computer Use (RealMouse) 🖱️⚡

> **让 AI 像真人一样毫秒级操控 Windows 桌面**  
> 专为 **WorkBuddy**、**Claude Code**、**Cursor**、**Windsurf** 等 AI Agent 打造的极速原生键鼠操作与本地视觉定位引擎。

---

## 🎯 它是干什么的？

很多用户在让 AI 操作电脑时，经常遇到以下难题：
1. **慢**：传统方案每走一步都要“截全屏 ➔ 上传大模型 / 磁盘落盘 OCR ➔ 计算坐标”，单步耗时 3~5 秒。
2. **瞎**：网页或软件的输入框内往往没有初始文字，视觉模型很难准确定位输入位置。
3. **重**：需要配置复杂的 Python 环境、Playwright 浏览器或笨重的 RPA 软件。

**本项目的核心目标是让 AI 拥有一双“毫秒级响应的手和眼”：**
- ⚡ **300ms 极速闭环**：原生 WinRT 内存流 OCR，零临时图片文件落盘，单步操作提速 5~8 倍。
- 🎯 **语义相对锚定**：自带 `--toright`（找输入框）、`--toleft`（勾选复选框），一条指令搞定“识字 ➔ 找框 ➔ 点击 ➔ 输入”。
- 📦 **零环境依赖**：预编译独立执行文件 `rm.exe`，免装 Python / Node.js，任何 Windows 电脑解压即用。
- 🛡️ **工业级防卡死**：贝塞尔拟人平滑鼠标轨迹、滚轮落点防卡死、中文偏旁拆分容错。

---

## 🚀 常用 AI 调用指令

```powershell
# 1. 查找界面文字并点击
rm.exe ocrw "登录" --click

# 2. 找到输入框并自动输入（在"用户名"右侧 50 像素处输入）
rm.exe ocrw "用户名" --toright 50 --type "admin"

# 3. 勾选单选框/复选框（在"同意"左侧 30 像素处勾选）
rm.exe ocrw "我已同意" --toleft 30 --click

# 4. 智能向下滚动（防顶部标签栏死锁）
rm.exe wheel -4 500 500
```

---

## 🤖 如何在你的 AI 中使用

1. **WorkBuddy 用户**：
   - 将本工具解压至任意目录，把 `bin` 路径加入系统环境变量 `PATH`。
   - 将项目中的 `SKILL.md` 内容复制到 WorkBuddy 的自定义提示词中，AI 即可学会调用 `rm.exe`。
2. **Claude Code / Codex 用户**：
   - 将本仓库放入技能目录，或者在 Agent 提示词中引入 `SKILL.md`。
3. **Cursor / Windsurf 用户**：
   - 在 `.cursorrules` 中声明可在终端执行 `rm.exe` 进行 Windows 界面交互。

---

## 📂 目录结构

- `bin/`：已编译的 `rm.exe` 与 `RealMouse.dll`（开箱即用）。
- `src/`：完整的开源 C# 源码（支持自行二次开发或编译）。
- `lib/`：PowerShell 辅助集成模块。
- `examples/`：单进程宏流水线脚本 (`.rmc`) 示例。
- `SKILL.md`：标准化 AI Agent 技能定义文件。
