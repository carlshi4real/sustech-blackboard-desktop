# Blackboard 作业提醒 · Windows

适用于 Windows 10 / 11 64 位（Intel / AMD）。这是独立的 Windows 应用，运行不需要 Codex、Python、开发工具或单独安装 .NET。

## 安装

在仓库的 **Windows v1.0.0** 发布页下载 `BlackboardDesktop-Windows-x64-Setup-v1.0.0.exe`，按向导安装到当前用户的应用目录。也可下载 `BlackboardDesktop-Windows-x64-v1.0.0.zip`，完整解压后运行 `BlackboardDesktop.exe`，请勿只复制其中一个 EXE。

学校登录页使用 Microsoft Edge WebView2。若本机缺少它，应用会提示前往微软官网下载；安装后重新打开应用。应用尚未购买 Windows 代码签名证书，Windows 可能显示未知发布者提示。

## 使用

- 启动时打开学校官方登录窗口，使用者自行输入自己的账号密码。有效的学校会话可继续使用；失效后请点击“连接账号”重新登录。
- 卡片显示逾期、今天待交及即将截止作业。截止时刻从详情页读取，以北京时间展示；未核实的时间明确标注“时刻待核实”，不会猜测。
- 每 15 分钟自动同步，也可手动刷新；唤醒电脑后重新同步。读取失败保留最后记录并显示状态与更新时间。
- 点击作业标题在默认浏览器中打开原作业；浏览器可能需要单独登录。
- 勾选作业只是在本机标记已处理，绝不会向学校提交作业。菜单可显示已处理项目。
- 拖动卡片标题移动位置。右上角 × 和 Alt+F4 只隐藏卡片；点击任务栏右下角托盘图标恢复。菜单的“退出作业提醒”才会退出程序。
- 菜单可选择始终置顶。默认是可拖动的桌面悬浮卡片，不是 Windows 系统小组件面板插件，也没有开机自启。

## 隐私

源码、安装程序与压缩包不包含任何真实学号、密码、作业数据、Cookie 或登录缓存。测试使用的是编造的示例课程与作业。

应用不读取账号密码输入框，不保存密码；学校页面的自动保存密码与自动填充已禁用。登录会话仅由本机 WebView2 管理。作业与已处理标记存储在 `%LOCALAPPDATA%\SUSTechBlackboardDesktop\snapshot.json`，学校会话在同目录的 `WebView2` 子目录中。卸载不会自动删除这些本机数据；退出应用后可手动删除此目录。

应用的学校窗口仅允许学校 Blackboard / CAS 的 HTTPS 顶层导航，不加载本地文件，也不向网页暴露本机调用接口。框架的正常网络请求（如 WebView2 更新）由微软组件管理。

## 构建与验证

在安装 .NET 10 SDK 的 Windows 上运行：

```powershell
dotnet publish windows/BlackboardDesktop.csproj -c Release -r win-x64 --self-contained true -o dist/app
$p = Start-Process dist/app/BlackboardDesktop.exe -ArgumentList '--self-test', 'test-results' -Wait -PassThru
if ($p.ExitCode -ne 0) { throw 'Tests failed' }
```

GitHub Actions 在 Windows 环境构建并启动真实 WinForms / WebView2，使用无账号的模拟页面检查提取、截止时刻、缓存保护及关闭/托盘恢复。测试通过后生成安装程序和 ZIP，并发布到 GitHub Releases。在线学校登录仍需使用者在自己的 Windows 电脑上完成；自动化不使用任何真实账号。

第三方组件：Microsoft .NET 与 Microsoft WebView2（依各自许可证分发）。
