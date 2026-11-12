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

## 1.3.0 scoped extension

THESIS: Make node choice easier through the user's NaiLink-style country cards and an explicit two-hop chain for a user-supplied final exit. This remains an Operate extension of the existing desktop surface, not a replacement visual world.

OWN-WORLD: Preserve white/slate/blue controls and the charcoal/coral/cream mark. Use bundled country flags; native WPF geometry and macOS SF Symbols carry selection and unknown-region icons. Status includes text as well as color.

STORY: Find a node through search, region or group; select its card. For a chain, add both nodes, choose entry and final exit, review local → entry → final exit → destination, save and use it from the workbench. Missing hops remain invalid; never silently substitute another exit or bypass a failed entry.

FIRST VIEWPORT: Actions, search and region/group filters lead into a responsive card grid. Cards show a flag, name, protocol and source or path. A visible note says that regions come from names, not measured exit locations. One click selects; the Windows double-click also opens the workbench.

FORM: Repeated information cards are explicitly pinned by the user's NaiLink reference. The chain editor protects route selection with two named pickers, a neutral initial name, visible direction, inline errors and explicit save/cancel. Two hops only; HTTP exits carry TCP traffic. No independent QUALITY BAR card or approved comp was created for this precisely specified local extension.

FINISH: Review wide/small node views, no-match state, valid and invalid chain editor and workbench on both native platforms. Render native view content without screen capture. Record test boundaries separately from real user TUN, residential-IP and account validation; finish with independent review and scoped documentation.

## 1.3.2 density refinement

The user rejected the oversized three-column node cards in their Windows screenshot and explicitly requested four columns and smaller cards. Preserve the existing identity and interactions. At the user's window width and wider, cap the grid at four columns; use fewer columns only when a smaller viewport needs room for readable labels. Cards use 92 units of inner height, 12 units of padding, 8-unit gaps and a 14-unit semibold name. Keep country flags, region/source, protocol, state text and native selection graphics. Long names and paths truncate with full content available in help/tooltips. Verify wide, user-width, narrow and no-match states with synthetic data and native self-rendering, without screen capture.

## 1.3.3 card-height refinement

The user requests another reduction in node-card height. Keep the four-column grid and existing font sizes, colors, flags and selection behavior. Use three information rows: name; region plus source/path on one line; protocol plus state. The middle line joins region and source with a centered dot, truncates when needed and exposes its complete text in a native tooltip/help. Inner height is 70, padding 10 and grid gap remains 8; the outer card is about 20% shorter than 1.3.2. Windows and macOS share this hierarchy. Verify wide/user-width, narrow, no-match and long-content states through native view rendering, never screen capture. This is a local refinement, with no new visual-world or concept selection.
