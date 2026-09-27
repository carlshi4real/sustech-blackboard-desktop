# SUSTech Blackboard Desktop

南方科技大学 Blackboard 的 macOS 桌面作业提醒。卡片按逾期、今天待交和即将截止分组，显示作业详情中的截止时刻和提交状态。可手动刷新、标记已处理、点击打开原作业；关闭卡片后仍可从菜单栏恢复。运行时每 15 分钟同步一次。

## 下载与使用

从仓库下载 `BlackboardDesktop-v1.0.0-macos-arm64.zip`，解压得到 `Blackboard作业提醒.app`，放进“应用程序”文件夹并打开。适用 Apple Silicon Mac，macOS 14 或更新版本。首次启动会弹出**南方科技大学官方统一认证页面**，每位用户自行输入自己的学号和密码。学校登录会话失效后需要再次登录。应用没有设置开机自启。

此版本使用临时签名，未经过 Apple 公证；macOS 可能提示无法验证开发者。

## 自行编译

在装有 Apple Command Line Tools 的 Mac 上运行 `./build.sh`。源码为 `main.swift`、`extract.js` 和 `Info.plist`。

## 数据与隐私

本仓库和下载包**不含任何账号、密码、个人作业、Cookie 或登录缓存**。应用只访问学校 Blackboard 与统一认证站点。每位用户的作业快照只保存在自己 Mac 的 `~/Library/Application Support/BlackboardDesktop/snapshot.json`；登录会话由系统 WebKit 管理。标记“已处理”只影响本机显示，不会提交或修改 Blackboard 中的作业。

该应用读取“右上角姓名 → Bb 主页 → 日程表”的内容。如果 Blackboard 页面结构发生变化，同步可能需要更新；同步失败时保留最后一次成功读取的记录，并显示更新时间。
