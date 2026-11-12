---
version: 1
slug: "src-launcher-app-mainwindow-xaml"
primary_target: "src/Launcher.App/MainWindow.xaml"
related_targets: ["src/Launcher.Mac/Views.swift", "src/Launcher.App/AppDialog.xaml", "src/Launcher.Mac/LaunchConfirmationView.swift"]
---

# Desktop launcher
Mode: Operate. Scope: Windows WPF and macOS SwiftUI, five pages and their dialogs. The user delegated implementation choices and requested a replacement UI and both Mac architectures. This is a code-led native build.

## Direction contract
THESIS: A readable desktop network workbench. Connection, transport, website and account authorization remain distinct. Replace the mixed dark navigation and warm forms with one light native console.
OWN-WORLD: White sidebar and surfaces, slate text, blue primary action. Green success, amber warning, red error, blue information, gray pending. Rounded controls share borders, spacing and system UI fonts.
STORY: Choose a node and project, connect, read explicit results, open the official client, then disconnect to restore the environment. Logs preserve time order from the top.
FIRST VIEWPORT: Fixed 224px sidebar, page heading and connection badge above the session form, primary launch action inside the form, independent results below. The signature interaction is a colored, labeled check row that separates API reachability from website refusal.
FORM: Native workbench with persistent navigation and scrolling content, chosen for operational clarity under the user's delegated choice. seed:d6d1e37c5d7e8b89.
FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance

## 1.2.1 scoped extension

User-pinned override: restore the original charcoal #202C2D, coral #E49B7D and cream #F4EBDF application mark. The action palette and existing desktop layout stay intact. Use design/brand.svg as the vector source, native WPF DrawingImage in the sidebar and supersampled multi-size PNG/ICO/ICNS for OS surfaces.

Replace application-owned Windows MessageBox prompts and the Mac launch alert with the existing white/slate/blue control language. Launch confirmation separates API transport, unverified checks and the decision. Default to returning to checks, preserve the report on both decisions, and keep action buttons available with long content.

Fresh installations preselect TUN, strict network checks, temporary IPv6-off and timezone matching, en-US and Chrome-first auto selection. Preserve existing explicit preferences and personal data. Only a user-initiated authorized connection applies system changes. The web probe and browser check target https://claude.ai/login; HTTP 403 is an automated-probe result, not proof of browser failure or account suspension.
