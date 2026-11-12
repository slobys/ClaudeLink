#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
ARCH="${1:-arm64}"
[[ "$ARCH" == arm64 || "$ARCH" == x64 ]] || exit 2
VERSION="$(python3 -c 'import xml.etree.ElementTree as E; print(E.parse("src/Launcher.App/Launcher.App.csproj").findtext(".//Version"))')"
ROOT="$PWD"; TOOLS="$ROOT/.tools/mac-$ARCH"; OUT="$ROOT/artifacts/macos-$ARCH"; APP="$OUT/Claude Link.app"
mkdir -p "$TOOLS" "$OUT"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
python3 - "$ARCH" "$TOOLS" <<'PY'
import sys,json,urllib.request,hashlib,tarfile,pathlib
arch,folder=sys.argv[1:]; folder=pathlib.Path(folder)
def fetch(url,file,algorithm,expected):
    if not file.exists(): urllib.request.urlretrieve(url,file)
    assert hashlib.new(algorithm,file.read_bytes()).hexdigest().lower()==expected.lower(),f'Checksum mismatch: {file.name}'
sdkroot=folder/'dotnet'
if not (sdkroot/'dotnet').exists():
    metadata=json.load(urllib.request.urlopen('https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'))
    sdk=next(s for r in metadata['releases'] for s in r.get('sdks',[]) if s['version']=='10.0.404')
    asset=next(f for f in sdk['files'] if f.get('rid')=='osx-'+arch and f['name'].endswith('.tar.gz'))
    archive=folder/'sdk.tar.gz'; fetch(asset['url'],archive,'sha512',asset['hash']); sdkroot.mkdir(exist_ok=True)
    with tarfile.open(archive) as package: package.extractall(sdkroot,filter='data')
cpu='arm64' if arch=='arm64' else 'amd64'
name=f'sing-box-1.14.4-darwin-{cpu}.tar.gz'
expected={'arm64':'0d8c7f5ed5d587b0f1a5e9772a708ec3bfa4ac4c9329604b01d8d9c2ba406838','x64':'eae86b9b033bba50b7c44ab719e4bfac0aca05efd28483390ed3aa510f32db25'}[arch]
archive=folder/name; fetch('https://github.com/SagerNet/sing-box/releases/download/v1.14.4/'+name,archive,'sha256',expected)
with tarfile.open(archive) as package:
    member=next(m for m in package.getmembers() if m.name.endswith('/sing-box') and m.isfile())
    (folder/'sing-box').write_bytes(package.extractfile(member).read())
(folder/'sing-box').chmod(0o755)
(folder/'core-manifest.json').write_text(json.dumps({'Version':'1.14.4','Archive':name,'Sha256':expected},indent=2))
PY
DOTNET="$TOOLS/dotnet/dotnet"
mkdir -p "$APP/Contents/"{MacOS,Resources,Helpers}
"$DOTNET" publish src/Launcher.Bridge -c Release -r "osx-$ARCH" --self-contained true -o "$APP/Contents/Resources/bridge" --nologo -v minimal
cp "$TOOLS/sing-box" "$APP/Contents/Resources/sing-box"
cp design/tokens.json "$APP/Contents/Resources/tokens.json"
mkdir -p "$APP/Contents/Resources/Licenses"
cp LICENSE vendor/*LICENSE*.txt vendor/dotnet-ThirdPartyNotices.txt docs/THIRD-PARTY-NOTICES.md "$APP/Contents/Resources/Licenses/"
cp "$TOOLS/core-manifest.json" "$APP/Contents/Resources/Licenses/"
# Sign nested native code first. Core hashes bind the actual signed binaries staged by the GUI.
find "$APP/Contents/Resources/bridge" -type f -print0 | while IFS= read -r -d '' file; do
  if /usr/bin/file -b "$file" | /usr/bin/grep -q 'Mach-O'; then /usr/bin/codesign --force --sign - "$file"; fi
done
/usr/bin/codesign --force --sign - "$APP/Contents/Resources/sing-box"
CORE_HASH="$(shasum -a 256 "$APP/Contents/Resources/sing-box" | awk '{print $1}')"
printf 'enum BuildInfo { static let coreHash = "%s" }\n' "$CORE_HASH" > "$OUT/HostBuildInfo.swift"
TARGET_ARCH="$ARCH"; [[ "$ARCH" != x64 ]] || TARGET_ARCH=x86_64
SWIFT=(xcrun swiftc -swift-version 5 -O -target "$TARGET_ARCH-apple-macos15.0" -parse-as-library)
"${SWIFT[@]}" src/Launcher.Mac/Host.swift "$OUT/HostBuildInfo.swift" -o "$APP/Contents/Helpers/ClaudeLinkHost"
/usr/bin/codesign --force --sign - "$APP/Contents/Helpers/ClaudeLinkHost"
HOST_HASH="$(shasum -a 256 "$APP/Contents/Helpers/ClaudeLinkHost" | awk '{print $1}')"
printf 'enum BuildInfo { static let version = "%s"; static let arch = "%s"; static let coreHash = "%s"; static let hostHash = "%s" }\n' "$VERSION" "$ARCH" "$CORE_HASH" "$HOST_HASH" > "$OUT/BuildInfo.swift"
FILES=(src/Launcher.Mac/{Models,Utilities,Networking,Session,Launcher,Updater,SingleInstance,AppState,Views,App}.swift)
"${SWIFT[@]}" "${FILES[@]}" "$OUT/BuildInfo.swift" -o "$APP/Contents/MacOS/ClaudeLink" -framework SwiftUI -framework AppKit -framework Security -framework CryptoKit
python3 - "$APP" "$VERSION" <<'PY'
import sys,plistlib,pathlib
app,version=sys.argv[1:]
plist={'CFBundleIdentifier':'com.slobys.ClaudeLink','CFBundleName':'Claude Link','CFBundleExecutable':'ClaudeLink','CFBundlePackageType':'APPL','CFBundleShortVersionString':version,'CFBundleVersion':version,'LSMinimumSystemVersion':'15.0','NSHighResolutionCapable':True,'CFBundleIconFile':'AppIcon','NSPrincipalClass':'NSApplication'}
pathlib.Path(app,'Contents/Info.plist').write_bytes(plistlib.dumps(plist))
PY
ICONSET="$OUT/AppIcon.iconset"; mkdir -p "$ICONSET"
for size in 16 32 128 256 512; do sips -z "$size" "$size" src/Launcher.App/Assets/App.png --out "$ICONSET/icon_${size}x${size}.png" >/dev/null; double=$((size*2)); sips -z "$double" "$double" src/Launcher.App/Assets/App.png --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null; done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/AppIcon.icns"
/usr/bin/codesign --force --sign - "$APP"
/usr/bin/codesign --verify --deep --strict "$APP"
"${SWIFT[@]}" src/Launcher.Mac/{Models,Utilities,Networking,Session,Launcher,Updater,SingleInstance,AppState,Tests}.swift "$OUT/BuildInfo.swift" -o "$APP/Contents/MacOS/ClaudeLinkTests" -framework SwiftUI -framework AppKit -framework Security -framework CryptoKit
/usr/bin/codesign --force --sign - "$APP/Contents/MacOS/ClaudeLinkTests"
"$APP/Contents/MacOS/ClaudeLinkTests" "$OUT/native-results.json"
# The auxiliary test executable is kept outside the shipping app after the tests.
mv "$APP/Contents/MacOS/ClaudeLinkTests" "$OUT/ClaudeLinkTests"
/usr/bin/codesign --force --sign - "$APP"
python3 scripts/test-macos-host.py "$APP" "$OUT"
"$APP/Contents/MacOS/ClaudeLink" --ui-smoke "$OUT/ui" & UI_PID=$!
for iteration in {1..90}; do kill -0 "$UI_PID" 2>/dev/null || break; sleep 1; done
if kill -0 "$UI_PID" 2>/dev/null; then kill "$UI_PID"; echo 'Native UI checks timed out'; exit 1; fi
wait "$UI_PID"
test -f "$OUT/ui/ui-results.json"
"$APP/Contents/MacOS/ClaudeLink" --shutdown-smoke "$OUT/shutdown-results.json" & QUIT_PID=$!
for iteration in {1..20}; do kill -0 "$QUIT_PID" 2>/dev/null || break; sleep 1; done
if kill -0 "$QUIT_PID" 2>/dev/null; then kill "$QUIT_PID"; echo 'Native graceful shutdown timed out'; exit 1; fi
wait "$QUIT_PID"
test -f "$OUT/shutdown-results.json"
/usr/bin/codesign --verify --deep --strict "$APP"
ditto -c -k --sequesterRsrc --keepParent "$APP" "$ROOT/artifacts/ClaudeLink-$VERSION-macos-$ARCH.zip"
DMG="$OUT/dmg"; mkdir -p "$DMG"; ditto "$APP" "$DMG/Claude Link.app"; ln -s /Applications "$DMG/Applications"
hdiutil create -volname "Claude Link $VERSION" -srcfolder "$DMG" -ov -format UDZO "$ROOT/artifacts/ClaudeLink-$VERSION-macos-$ARCH.dmg"
