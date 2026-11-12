# Product
<!-- impeccable:product-schema 1 -->

## Platform
adaptive

Desktop scope: Windows WPF and native macOS SwiftUI. This is not a mobile or web interface.

## Users
Chinese-speaking Claude Code users who want to automate recurring network and session-environment setup through a desktop interface.

## Product Purpose
Automate Claude Code network-environment setup: apply saved proxy or TUN settings, handle optional temporary IPv6 and timezone changes, set language for the CLI and dedicated check browser, check connectivity, then open the official desktop Code page or CLI. On session end, attempt to restore recorded temporary changes. Node and subscription management supply the connection for this workflow. Keep connection, account authorization and unverified results distinct.

## Operating Context
Windows and native macOS clients are published with signed updates. Lead product explanations with automatic environment preparation, checks, launch and recovery. Present node management as support for that workflow. Provide both Apple Silicon and Intel packages; user Mac hardware is unknown.

## Capabilities and Constraints
Reuse the existing C# node importer and sing-box configuration builder. Preserve Windows encrypted settings, nodes, subscriptions and recovery behavior. Native macOS uses Keychain-backed encryption and an OS-specific session supervisor. Target macOS 15+ to match .NET 10 supported platforms; Windows x64 remains supported. Never claim account authorization based on unauthenticated API reachability. No new large UI framework dependency. Apple Developer ID/notarization credentials have not been supplied.

## Brand Commitments
Keep the name Claude Link and Chinese interface. Independent project, not an Anthropic or SagerNet product. No invented capability or verification claims.

User-pinned application mark uses the legacy charcoal #202C2D, coral #E49B7D and cream #F4EBDF palette. Keep the existing blue action palette; the logo is a separate identity asset rendered from vector geometry.

## Evidence on Hand
Existing source, user screenshots, synthetic WPF renderings, protocol tests, installer tests and GitHub Releases. User screenshots are private reference material and are not published.

## Product Principles
Status must be recognizable by color, text and icon. GUI and CLI are separate choices. Preserve work and user configuration across updates. Privileged operations require OS authorization and recoverable journals. Verify native builds on their actual target OS.

New-install presets select TUN, strict checks, temporary IPv6-off and timezone matching, en-US and Chrome-first auto selection. Explicit saved preferences override defaults; presets contain no personal nodes, subscriptions or executable paths. Apply system changes only after a user-initiated authorized connection. The webpage probe is https://claude.ai/login and cannot establish browser access or account authorization.
