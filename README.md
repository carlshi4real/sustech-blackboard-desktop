# SUSTech Blackboard Desktop

南方科技大学 Blackboard 作业桌面提醒，提供 **macOS** 和 **Windows** 两个独立版本。卡片按逾期、今天待交和即将截止分组，显示从作业详情读取的截止日期与具体时刻；支持每 15 分钟自动同步、手动刷新、打开原作业及本机“已处理”标记。关闭卡片后，macOS 菜单栏或 Windows 托盘图标仍在，可随时恢复。

## 下载

| 系统 | 下载 | 使用方式 |
| --- | --- | --- |
| Windows 10/11，64 位 Intel/AMD | [Windows v1.0.0 安装包与 ZIP](https://github.com/carlshi4real/sustech-blackboard-desktop/releases/tag/windows-v1.0.0) | 下载 `BlackboardDesktop-Windows-x64-Setup-v1.0.0.exe` 安装，或完整解压 ZIP 后运行程序。详见 [Windows 使用说明](windows/README.md)。 |
| macOS 14+，Apple Silicon | [macOS v1.0.0 下载包](https://github.com/carlshi4real/sustech-blackboard-desktop/releases/tag/v1.0.0) | 解压得到 `Blackboard作业提醒.app`，移入“应用程序”文件夹。 |

两个版本都可独立运行，不需要 Codex。Windows 包已包含 .NET 运行时，学校登录页还需要 Microsoft Edge WebView2；缺少时应用会提示安装。两个版本目前都没有正式代码签名，操作系统可能显示未知开发者或发布者提示。

## 登录与隐私

首次启动时，使用者在学校官方登录页自行输入自己的账号和密码。仓库及发布包**不含真实学号、密码、个人作业、Cookie 或登录缓存**。应用不保存账号密码；作业快照与登录会话仅保存在用户自己的电脑上。标记“已处理”只改变本机显示，不会提交作业或修改 Blackboard。

学校页面结构若发生变化，作业同步可能需要更新。同步失败时会保留最后一次成功读取的记录，并显示状态与更新时间。Windows 自动构建在 GitHub 的 Windows 环境中检查程序启动、模拟页面提取、截止时刻、缓存保护、托盘恢复与安装程序；学校在线登录仍需使用者在自己的电脑上完成。

源码：macOS 位于仓库根目录；Windows 位于 [`windows/`](windows/)。
