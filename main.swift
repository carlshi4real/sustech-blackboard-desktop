import AppKit
import SwiftUI
import WebKit

let origin = "https://bb.sustech.edu.cn"
let overview = origin + "/webapps/bb-social-learning-BBLEARN/execute/mybbOverview?cmd=display&globalNavigation=false"
let home = origin + "/webapps/bb-social-learning-BBLEARN/execute/mybb?cmd=display&toolId=BB-CORE_____overview-tool"
struct Assignment: Codable, Identifiable {
    var id: String
    var title: String
    var course: String
    var due: String
    var group: String
    var note: String?
    var dueTime: String?
    var submitted: Bool?
    var url: URL { URL(string: origin + "/webapps/calendar/launch/attempt/" + id)! }
}
struct Snapshot: Codable { var items: [Assignment]; var updated: Date }
@MainActor final class Store: ObservableObject {
    @Published var items: [Assignment] = []
    @Published var updated: Date = .distantPast
    @Published var status = "登录 Blackboard 后自动同步"
    @Published var syncing = false
    @Published var showHandled = false
    @Published var handled: Set<String> = []
    var sync: (() -> Void)?
    var connect: (() -> Void)?
    var raise: (() -> Void)?
    var hide: (() -> Void)?
    let folder = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0].appendingPathComponent("BlackboardDesktop")
    init() {
        handled = Set(UserDefaults.standard.stringArray(forKey: "handled") ?? [])
        let url = folder.appendingPathComponent("snapshot.json")
        if let data = try? Data(contentsOf: url), let s = try? JSONDecoder().decode(Snapshot.self, from: data) { items = s.items; updated = s.updated; status = "显示上次同步的记录" }
    }
    func save(_ rows: [Assignment]) {
        let old = Dictionary(uniqueKeysWithValues: items.map { ($0.id, $0) })
        items = rows.map { row in
            var result = row
            result.dueTime = old[row.id]?.dueTime
            result.submitted = old[row.id]?.submitted
            return result
        }
        updated = Date(); syncing = false; status = "日程已同步 · 正在核对截止时间"
        persist()
    }
    func updateDetail(_ id: String, time: String?, submitted: Bool?) {
        guard let index = items.firstIndex(where: { $0.id == id }) else { return }
        if let time { items[index].dueTime = time }
        if let submitted { items[index].submitted = submitted }
        persist()
    }
    func persist() {
        do {
            try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
            try JSONEncoder().encode(Snapshot(items: items, updated: updated)).write(to: folder.appendingPathComponent("snapshot.json"), options: .atomic)
        } catch { status = "已同步，但本地缓存保存失败" }
    }
    func toggle(_ a: Assignment) {
        if handled.contains(a.id) { handled.remove(a.id) } else { handled.insert(a.id) }
        UserDefaults.standard.set(Array(handled), forKey: "handled")
    }
    var visible: [Assignment] { items.filter { showHandled || !handled.contains($0.id) } }
    func count(_ group: String) -> Int { visible.filter { $0.group == group && !handled.contains($0.id) && $0.submitted != true }.count }
}
struct WidgetView: View {
    @ObservedObject var store: Store
    let coral = Color(red: 0.98, green: 0.43, blue: 0.36)
    let amber = Color(red: 1, green: 0.76, blue: 0.39)
    let mint = Color(red: 0.49, green: 0.84, blue: 0.72)
    var body: some View {
        VStack(alignment: .leading, spacing: 11) {
            HStack(spacing: 7) {
                Image(systemName: "square.stack.3d.up.fill").font(.system(size: 17)).foregroundStyle(mint)
                VStack(alignment: .leading, spacing: 1) {
                    Text("作业提醒").font(.system(size: 17, weight: .bold))
                    Text("BLACKBOARD · 南方科技大学").font(.system(size: 8, weight: .semibold)).tracking(0.8).foregroundStyle(.white.opacity(0.48))
                }
                Spacer()
                Button { store.sync?() } label: { Image(systemName: "arrow.clockwise").font(.system(size: 13, weight: .medium)) }.disabled(store.syncing).help("刷新作业")
                Menu {
                    Button("连接 / 登录 Blackboard") { store.connect?() }
                    Button("打开 Blackboard") { NSWorkspace.shared.open(URL(string: home)!) }
                    Toggle("显示已处理", isOn: $store.showHandled)
                    Button("移动到屏幕右侧") { store.raise?() }
                    Divider()
                    Button("退出作业提醒") { NSApp.terminate(nil) }
                } label: { Image(systemName: "ellipsis").font(.system(size: 16)) }.menuStyle(.borderlessButton).frame(width: 18)
                Button { store.hide?() } label: {
                    Image(systemName: "xmark").font(.system(size: 11, weight: .semibold))
                        .frame(width: 20, height: 20)
                }.help("隐藏桌面卡片，保留菜单栏图标").accessibilityLabel("关闭桌面卡片")
            }
            HStack(spacing: 6) {
                metric("逾期", store.count("过期"), coral)
                metric("今天待交", store.count("今天截止"), amber)
                metric("即将截止", store.visible.filter { $0.group != "过期" && $0.group != "今天截止" && !store.handled.contains($0.id) && $0.submitted != true }.count, mint)
            }
            ScrollView {
                VStack(alignment: .leading, spacing: 10) {
                    ForEach(["过期", "今天截止", "本周截止", "以后截止"], id: \.self) { group in
                        let rows = store.visible.filter { $0.group == group }
                        if !rows.isEmpty {
                            VStack(alignment: .leading, spacing: 5) {
                                HStack(spacing: 6) {
                                    Circle().fill(color(group)).frame(width: 5, height: 5)
                                    Text(group == "过期" ? "逾期 · 请核对提交状态" : group).font(.system(size: 10, weight: .semibold)).foregroundStyle(.white.opacity(0.58))
                                }
                                ForEach(rows) { row in
                                    HStack(alignment: .top, spacing: 7) {
                                        Button { store.toggle(row) } label: {
                                            Image(systemName: store.handled.contains(row.id) || row.submitted == true ? "checkmark.circle.fill" : "circle").font(.system(size: 16)).foregroundStyle(store.handled.contains(row.id) || row.submitted == true ? mint : .white.opacity(0.25))
                                        }.help("标记已处理（仅在小组件中隐藏，不提交作业）")
                                        VStack(alignment: .leading, spacing: 3) {
                                            Button { NSWorkspace.shared.open(row.url) } label: {
                                                Text(row.title).font(.system(size: 12, weight: .semibold)).multilineTextAlignment(.leading).fixedSize(horizontal: false, vertical: true).strikethrough(store.handled.contains(row.id))
                                            }.help("在浏览器中打开作业")
                                            Text(row.course).font(.system(size: 9)).foregroundStyle(.white.opacity(0.48)).lineLimit(1)
                                            HStack {
                                                Text(row.due + (row.dueTime.map { " · \($0)" } ?? "")).font(.system(size: 10, weight: .medium)).foregroundStyle(color(group))
                                                if row.submitted == true { Text("已提交").font(.system(size: 9)).foregroundStyle(mint) }
                                                else if row.note != nil { Text("已有成绩 · 请核对").font(.system(size: 9)).foregroundStyle(mint) }
                                            }
                                        }
                                        Spacer(minLength: 0)
                                    }.padding(8).frame(maxWidth: .infinity, alignment: .leading).background(.white.opacity(0.045), in: RoundedRectangle(cornerRadius: 10))
                                }
                            }
                        }
                    }
                    if store.visible.isEmpty { Text("当前列表没有待办作业").foregroundStyle(.secondary).padding(.vertical, 35) }
                }
            }.scrollIndicators(.hidden)
            VStack(alignment: .leading, spacing: 3) {
                HStack {
                    Circle().fill(store.syncing ? amber : mint).frame(width: 5, height: 5)
                    Text(store.status).font(.system(size: 9)).foregroundStyle(.white.opacity(0.6)).lineLimit(2)
                    Spacer(minLength: 0)
                }
                HStack {
                    Text("更新于 \(store.updated.formatted(.dateTime.month().day().hour().minute()))").font(.system(size: 9)).foregroundStyle(.white.opacity(0.35))
                    Spacer()
                    Button("连接账号") { store.connect?() }.font(.system(size: 10)).foregroundStyle(mint)
                }
                Text("以最后同步的日程为准").font(.system(size: 9)).foregroundStyle(.white.opacity(0.35))
            }
        }
        .padding(15).frame(width: 330, height: 480)
        .foregroundStyle(.white).buttonStyle(.plain)
        .background(LinearGradient(colors: [Color(red: 0.12, green: 0.18, blue: 0.19), Color(red: 0.075, green: 0.10, blue: 0.13)], startPoint: .topLeading, endPoint: .bottomTrailing))
        .clipShape(RoundedRectangle(cornerRadius: 19))
        .overlay(RoundedRectangle(cornerRadius: 19).stroke(.white.opacity(0.12), lineWidth: 1))
        .environment(\.colorScheme, .dark)
    }
    func color(_ group: String) -> Color { group == "过期" ? coral : group == "今天截止" ? amber : mint }
    func metric(_ label: String, _ count: Int, _ color: Color) -> some View {
        HStack(spacing: 5) {
            Text("\(count)").font(.system(size: 20, weight: .semibold, design: .rounded)).foregroundStyle(color)
            Text(label).font(.system(size: 9, weight: .medium)).foregroundStyle(.white.opacity(0.68)).lineLimit(1)
        }.frame(maxWidth: .infinity, alignment: .leading).padding(.horizontal, 8).padding(.vertical, 9).background(color.opacity(0.08), in: RoundedRectangle(cornerRadius: 10))
    }
}
final class DesktopPanel: NSPanel {
    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { false }
}
@MainActor final class AppDelegate: NSObject, NSApplicationDelegate, WKNavigationDelegate, WKUIDelegate {
    let store = Store()
    var panel: DesktopPanel!
    var web: WKWebView!
    var detailWeb: WKWebView!
    var detailQueue: [Assignment] = []
    var detailIndex = 0
    var loginWindow: NSWindow!
    var statusItem: NSStatusItem!
    var timer: Timer?
    var watchdog: Timer?
    var polling: Timer?
    var remainingPolls = 0
    var loginVisible = false
    var isAtOverview = false
    func applicationDidFinishLaunching(_ notification: Notification) {
        panel = DesktopPanel(contentRect: NSRect(x: 0, y: 0, width: 330, height: 480), styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        panel.title = "Blackboard 作业提醒"
        panel.isFloatingPanel = false; panel.hidesOnDeactivate = false
        panel.isOpaque = false; panel.backgroundColor = .clear; panel.hasShadow = true
        panel.isMovableByWindowBackground = true
        panel.collectionBehavior = [.canJoinAllSpaces, .stationary, .ignoresCycle]
        panel.level = NSWindow.Level(rawValue: Int(CGWindowLevelForKey(.desktopIconWindow)) + 1)
        panel.contentView = NSHostingView(rootView: WidgetView(store: store))
        if panel.setFrameUsingName("desktop-position") {
            let old = panel.frame
            panel.setFrame(NSRect(x: old.maxX - 330, y: old.maxY - 480, width: 330, height: 480), display: false)
        } else { position() }
        panel.setFrameAutosaveName("desktop-position")
        panel.orderFrontRegardless()
        let config = WKWebViewConfiguration(); config.websiteDataStore = .default()
        web = WKWebView(frame: NSRect(x: 0, y: 0, width: 1050, height: 760), configuration: config)
        web.navigationDelegate = self; web.uiDelegate = self
        let detailConfig = WKWebViewConfiguration(); detailConfig.websiteDataStore = .default()
        detailWeb = WKWebView(frame: .zero, configuration: detailConfig)
        detailWeb.navigationDelegate = self
        loginWindow = NSWindow(contentRect: web.frame, styleMask: [.titled, .closable, .resizable, .miniaturizable], backing: .buffered, defer: false)
        loginWindow.title = "连接 Blackboard — 登录后自动同步"
        loginWindow.contentView = web; loginWindow.isReleasedWhenClosed = false; loginWindow.center()
        store.sync = { [weak self] in self?.refresh() }
        store.connect = { [weak self] in self?.showLogin() }
        store.raise = { [weak self] in self?.position(); self?.panel.orderFrontRegardless() }
        store.hide = { [weak self] in self?.panel.orderOut(nil) }
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        statusItem.button?.image = NSImage(systemSymbolName: "checklist", accessibilityDescription: "作业提醒")
        let menu = NSMenu()
        menu.addItem(withTitle: "显示作业提醒", action: #selector(showWidget), keyEquivalent: "")
        menu.addItem(withTitle: "刷新作业", action: #selector(refresh), keyEquivalent: "r")
        menu.addItem(withTitle: "连接 Blackboard", action: #selector(showLogin), keyEquivalent: "")
        menu.addItem(.separator())
        menu.addItem(withTitle: "退出", action: #selector(quit), keyEquivalent: "q")
        for item in menu.items { item.target = self }
        statusItem.menu = menu
        timer = Timer.scheduledTimer(withTimeInterval: 900, repeats: true) { [weak self] _ in Task { @MainActor in self?.refresh() } }
        NSWorkspace.shared.notificationCenter.addObserver(self, selector: #selector(refresh), name: NSWorkspace.didWakeNotification, object: nil)
        refresh()
        showLogin()
    }
    func position() {
        guard let screen = NSScreen.main else { return }
        let f = screen.visibleFrame
        panel.setFrameOrigin(NSPoint(x: f.maxX - 350, y: f.maxY - 500))
    }
    @objc func showWidget() {
        panel.level = .floating; panel.orderFrontRegardless()
        DispatchQueue.main.asyncAfter(deadline: .now() + 12) { [weak self] in self?.panel.level = NSWindow.Level(rawValue: Int(CGWindowLevelForKey(.desktopIconWindow)) + 1) }
    }
    @objc func quit() { NSApp.terminate(nil) }
    @objc func showLogin() {
        loginVisible = true
        loginWindow.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true)
        if web.url == nil || (!store.syncing && isAtOverview) { refresh() }
    }
    @objc func refresh() {
        if store.syncing || (loginWindow.isVisible && web.url?.host == "cas.sustech.edu.cn") { return }
        polling?.invalidate(); store.syncing = true; store.status = "正在同步 Blackboard…"
        isAtOverview = false
        web.load(URLRequest(url: URL(string: home)!, cachePolicy: .reloadIgnoringLocalCacheData))
        watchdog?.invalidate()
        watchdog = Timer.scheduledTimer(withTimeInterval: 75, repeats: false) { [weak self] _ in Task { @MainActor in
            guard let self, self.store.syncing else { return }
            self.store.syncing = false; self.store.status = "连接超时 · 保留上次数据，点击刷新重试"
        } }
    }
    func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
        if webView === detailWeb { readDetail(); return }
        guard let url = webView.url else { return }
        if url.host == "cas.sustech.edu.cn" || url.path.contains("login") {
            store.syncing = false; store.status = "需要登录 · 点击“连接账号”开启自动同步"; watchdog?.invalidate(); return
        }
        guard url.host == "bb.sustech.edu.cn" else { return }
        if !url.path.contains("/execute/mybb") { web.load(URLRequest(url: URL(string: home)!)); return }
        isAtOverview = true
        remainingPolls = 45; extract()
        polling?.invalidate()
        polling = Timer.scheduledTimer(withTimeInterval: 2, repeats: true) { [weak self] _ in Task { @MainActor in
            guard let self else { return }
            self.remainingPolls -= 1
            if self.remainingPolls <= 0 { self.polling?.invalidate(); self.store.syncing = false; self.store.status = "页面结构暂无法读取 · 保留上次数据"; return }
            self.extract()
        } }
    }
    func extract() {
        guard let url = Bundle.main.url(forResource: "extract", withExtension: "js"), let js = try? String(contentsOf: url, encoding: .utf8) else { return }
        web.evaluateJavaScript(js) { [weak self] value, error in
            guard let self, let string = value as? String, let data = string.data(using: .utf8) else { return }
            struct Result: Decodable { let ready: Bool; let items: [Assignment] }
            guard let result = try? JSONDecoder().decode(Result.self, from: data), result.ready else { return }
            self.polling?.invalidate(); self.watchdog?.invalidate(); self.store.save(result.items)
            self.statusItem.button?.toolTip = "Blackboard：\(result.items.count) 项日程"
            self.detailQueue = result.items.filter { $0.group == "今天截止" || $0.group == "本周截止" || $0.group == "以后截止" }
            self.detailIndex = 0
            self.loadNextDetail()
            if self.loginVisible { self.loginWindow.orderOut(nil); self.loginVisible = false; self.showWidget() }
        }
    }
    func loadNextDetail() {
        guard detailIndex < detailQueue.count else { store.status = "已同步 · 每 15 分钟自动刷新"; return }
        detailWeb.load(URLRequest(url: detailQueue[detailIndex].url))
    }
    func readDetail() {
        guard detailIndex < detailQueue.count else { return }
        let current = detailQueue[detailIndex]
        guard detailWeb.url?.host == "bb.sustech.edu.cn", detailWeb.url?.path.contains("uploadAssignment") == true else {
            detailIndex += 1; loadNextDetail(); return
        }
        let script = """
        (() => {
          let text = document.body.innerText;
          if (!/到期日期[\\s\\S]{0,120}\\d{4}年/.test(text)) {
            const button = [...document.querySelectorAll('button')].find(b => b.innerText.includes('作业详细信息'));
            if (button) button.click();
            text = document.body.innerText;
          }
          const at = text.indexOf('到期日期');
          const part = at < 0 ? '' : text.slice(at, at + 160);
          const found = part.match(/\\d{4}年\\s*\\d{1,2}月\\s*\\d{1,2}日[\\s\\S]{0,30}?(上午|下午|AM|PM)\\s*(\\d{1,2}):(\\d{2})/);
          let time = null;
          if (found) {
            let hour = Number(found[2]);
            if (found[1] === '下午' || found[1] === 'PM') hour = hour % 12 + 12;
            else hour = hour % 12;
            time = String(hour).padStart(2, '0') + ':' + found[3];
          }
          const submitted = text.includes('复查提交历史记录') && text.includes('尝试') ? true : null;
          return JSON.stringify({time, submitted});
        })();
        """
        detailWeb.evaluateJavaScript(script) { [weak self] value, _ in
            guard let self else { return }
            if let json = value as? String, let data = json.data(using: .utf8) {
                struct Detail: Decodable { let time: String?; let submitted: Bool? }
                if let detail = try? JSONDecoder().decode(Detail.self, from: data) {
                    self.store.updateDetail(current.id, time: detail.time, submitted: detail.submitted)
                }
            }
            self.detailIndex += 1; self.loadNextDetail()
        }
    }
    func webView(_ webView: WKWebView, didFailProvisionalNavigation navigation: WKNavigation!, withError error: Error) {
        if webView === detailWeb { detailIndex += 1; loadNextDetail() } else { fail(error) }
    }
    func webView(_ webView: WKWebView, didFail navigation: WKNavigation!, withError error: Error) {
        if webView === detailWeb { detailIndex += 1; loadNextDetail() } else { fail(error) }
    }
    func fail(_ error: Error) {
        if (error as NSError).code == NSURLErrorCancelled { return }
        store.syncing = false; store.status = "网络连接失败 · 保留上次数据"; watchdog?.invalidate()
    }
    func webView(_ webView: WKWebView, decidePolicyFor navigationAction: WKNavigationAction, decisionHandler: @escaping (WKNavigationActionPolicy) -> Void) {
        guard let url = navigationAction.request.url else { decisionHandler(.cancel); return }
        if url.scheme == "about" || (url.scheme == "https" && ["bb.sustech.edu.cn", "cas.sustech.edu.cn"].contains(url.host ?? "")) { decisionHandler(.allow) }
        else {
            decisionHandler(.cancel)
            if webView === web && navigationAction.targetFrame?.isMainFrame == true {
                store.status = "登录跳转到其他站点，请在浏览器中检查"
            }
        }
    }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { false }
}
MainActor.assumeIsolated {
    let app = NSApplication.shared
    app.setActivationPolicy(.accessory)
    let delegate = AppDelegate()
    app.delegate = delegate
    app.run()
}
