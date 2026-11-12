# 1.2.0 implementation plan

1. Replace the Windows visual system with a light, unified desktop console: shared palette, controls, typography, explicit semantic result badges and structured colored logs. Keep operational behavior and existing settings.
2. Add a small cross-platform C# bridge over Launcher.Core; native SwiftUI macOS app uses it for node/subscription parsing and core configuration. Build osx-arm64 and osx-x64, macOS 15+.
3. macOS storage uses an AES-GCM key in Keychain. A separately compiled native supervisor owns TUN sessions, verifies staged executables/configuration, monitors the GUI lifetime and journals system changes. GUI and CLI launch through OS-native paths.
4. Package .app, .dmg and update .zip files on macOS CI. Preserve the Windows schema-1 update.json; publish separate signed Mac manifests so older Windows updaters continue to work.
5. Verify configuration, imports, storage, supervisor lifetime/recovery, updates and native UI rendering. Record limitations separately from tested behavior. Finish UI review and design documentation, then publish installable artifacts.

Visual direction: code-led Operate surface. Light sidebar and white content surfaces; blue action accent; slate text; green success, amber warning, red failure, blue information and gray pending. System UI fonts appropriate to each OS, consistent body sizing and spacing, no decorative motion. Status color never replaces its label or icon. Windows active sessions are not interrupted during development.
