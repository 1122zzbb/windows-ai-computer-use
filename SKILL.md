---
name: real-mouse-windows
description: High-speed native Windows mouse & keyboard automation engine for AI Agents. Features zero-disk WinRT RAM-stream OCR, relative semantic offsets (--toright, --toleft), anti-deadlock mouse wheeling, Chinese radical fault-tolerance, and .rmc macro pipeline batching.
---

# RealMouse 3.0 —— 极速原生键鼠自动化引擎

专为 AI Agent（WorkBuddy、Claude Code、Cursor、Windsurf、Codex、Cline 等）打造的 Windows 原生桌面键鼠与视觉定位引擎。

## 一、 核心架构与速度突破

### 传统截图 OCR vs RealMouse 3.0 内存流流程
```text
【传统流程（耗时 ~3500ms）】：
截全屏 ➔ PNG 编码写入磁盘 (250ms) ➔ 读磁盘文件解码 (200ms) ➔ 全屏识别 (350ms) ➔ 多轮往返调度

【RealMouse 3.0 内存流流程（耗时 ~300-600ms，提速 5-7 倍）】：
视窗 ROI 裁剪 (20ms) ➔ 内存流直传 WinRT (30ms) ➔ 原生识别 (200-300ms) ➔ 相对偏移瞬时操作 (0ms)
```

实测基准对比（物理 2880×1800 分辨率）：
| 模式 | 运行方式 | 磁盘 I/O | 实测总耗时 | 适用场景 |
|:---|:---|:---:|:---:|:---|
| **全屏常规 OCR** | `rm.exe ocr <text>` | 0 KB (纯内存) | 712 ms | 跨多窗口全局查找 |
| **全屏切片并发 OCR** | `rm.exe ocr <text> --parallel` | 0 KB (纯内存) | **489 ms** | 大屏复杂界面高速并发 |
| **活动视窗 ROI 极速 OCR** | `rm.exe ocrw <text>` | 0 KB (纯内存) | **335–598 ms** | 浏览器/应用内表单、弹窗交互 |
| **.rmc 宏脚本批处理** | `rm.exe script <file>` | 0 KB (单进程) | **每步 80–250 ms** | 连续自动化录入表单、流水线答题 |

---

## 二、 常用指令极速对照表

| 分类 | 指令 | 说明 |
|:---|:---|:---|
| **视窗感知** | `rm.exe screen` | 物理分辨率、DPI、当前焦点句柄 |
| | `rm.exe windows` | 列出桌面全部可见物理窗口与 PID |
| | `rm.exe where` | 鼠标实时物理坐标及下方控件 |
| **精准定位** | `rm.exe ocrw <文本> [--click]` | 焦点窗口 ROI 极速 OCR 定位/点击（~300ms） |
| | `rm.exe ocr <文本> [--parallel]` | 全屏多线程并行切片 OCR |
| | `rm.exe locate <窗口> [关键词]` | UIA 纯控件树免截图定位 |
| **动作操作** | `rm.exe focus <标题>` | 激活并前置窗口（含最小化自动恢复） |
| | `rm.exe click <x> <y>` | 贝塞尔平滑轨迹左键点击 |
| | `rm.exe wheel <n> [x y]` | 智能滚轮（负数向下，正数向上） |
| | `rm.exe type <文本>` | Unicode 原生键盘注入（长文本走剪贴板） |
| | `rm.exe key <组合键>` | `ctrl+v`、`enter`、`tab`、`ctrl+w` |
| **流水线批处理** | `rm.exe script <file.rmc>` | 单进程执行宏流水线，毫秒级连招 |
| **自适应等待** | `rm.exe waitocr <文本> [ms]` | 等待文字出现 |
| | `rm.exe waitpix <x> <y> [ms]` | 等待像素变化 |

---

## 三、 本地极速 OCR 指令集 (`ocrw` / `ocr`)

### 1. 指令语法
```bash
# 焦点视窗 ROI 模式（首选，最快）：
rm.exe ocrw <目标文本> [选项]

# 全屏内存模式：
rm.exe ocr <目标文本> [选项]
```

### 2. 常用选项
| 参数 | 说明 | 示例 |
|:---|:---|:---|
| `--click` | 找到后左键单击目标中心 | `rm.exe ocrw "登录" --click` |
| `--dclick` / `--rclick` / `--tclick` | 双击 / 右键 / 三击（选中文本行） | `rm.exe ocrw "项目名称" --tclick` |
| `--toright [dist=55]` | **智能右移锚定**：点击文字右侧的输入框 | `rm.exe ocrw "主要研究内容" --toright 60 --click` |
| `--toleft [dist=30]` | **智能左移锚定**：点击文字左侧的单选框/复选框 | `rm.exe ocrw "A. 正确答案" --toleft 35 --click` |
| `--below [dist=35]` | **智能下移锚定**：点击文字下方的区域 | `rm.exe ocrw "搜索" --below 40 --click` |
| `--offsetx <px>` / `--offsety <px>` | 精细像素级微调偏移量 | `rm.exe ocrw "提交" --offsetx 10 --click` |
| `--type <文本>` | 点击后直接输入文字（自动剪贴板/Unicode注入） | `rm.exe ocrw "姓名" --toright 50 --type "张三"` |
| `--tab` / `--enter` | 点击/打字完成后自动按 Tab 或 Enter | `rm.exe ocrw "搜索" --type "毕业设计" --enter` |
| `--window <标题>` | 执行前自动激活并置顶指定窗口 | `rm.exe ocrw "开始答题" --window "Microsoft Edge"` |
| `--index <N>` | 当界面有多处相同时，指定第 N 个命中目标（从 1 开始） | `rm.exe ocrw "编辑" --index 2 --click` |
| `--parallel` | 启用上下半屏多线程并行识别（仅全屏模式） | `rm.exe ocr "确定" --parallel --click` |

---

## 四、 核心鲁棒性增强

1. **汉字偏旁拆分容错**：内置反向规范化，自动兼容合体字在特殊字体下被拆成偏旁（如“作”拆为“亻乍”）的情况。
2. **智能滚轮防卡死**：指针在标签栏等非内容区时自动将鼠标滑入内容区后再滚轮，彻底告别盲滚失效。
3. **最大化隐形边框安全裁剪**：自动修正 Windows 阴影外延与屏幕边界 Clamp，杜绝 GDI BitBlt 报错。
4. **自适应动态等待感知**：以 100ms 间隔自适应感知文字渲染完成，文字一出立即返回，拒绝盲目静态 sleep。
