---
version: 1
slug: "src-launcher-app-mainwindow-xaml"
primary_target: "src/Launcher.App/MainWindow.xaml"
related_targets: ["src/Launcher.Mac/Views.swift"]
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
