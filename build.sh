#!/bin/zsh
set -eu
cd "$(dirname "$0")"
APP='Blackboard作业提醒.app'
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp Info.plist "$APP/Contents/Info.plist"
cp extract.js "$APP/Contents/Resources/extract.js"
swiftc main.swift -o "$APP/Contents/MacOS/BlackboardDesktop" -framework AppKit -framework SwiftUI -framework WebKit -module-cache-path "${TMPDIR:-/tmp}/blackboard-swift-module-cache"
codesign --force --deep --sign - "$APP"
