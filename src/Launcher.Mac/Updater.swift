import AppKit
import CryptoKit
import Foundation

struct ReleaseManifest: Codable { var Schema: Int; var Repository: String; var Version: String; var AssetName: String; var Size: Int; var Sha256: String }
struct UpdateOffer { var manifest: ReleaseManifest; var url: String; var notes: String }
enum Updater {
    static let repository = "slobys/ClaudeLink"
    static let publicKey = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEgsfjXnQIGksYc3n9Vgdpk7vQFFU66Tj5ggxikImNkvlwyGhQVnD9Isg0hTYRtks0kedbezwfOfZUBg7zo9X2ug=="
    static func verified(_ bytes: Data,_ signature: Data) throws -> ReleaseManifest {
        let key = try P256.Signing.PublicKey(derRepresentation:Data(base64Encoded:publicKey)!)
        guard let raw=Data(base64Encoded:String(decoding:signature,as:UTF8.self).trimmingCharacters(in:.whitespacesAndNewlines)), key.isValidSignature(try P256.Signing.ECDSASignature(rawRepresentation:raw),for:bytes) else { throw ClientError.message("更新签名校验失败，已停止更新。") }
        let manifest = try JSONDecoder().decode(ReleaseManifest.self,from:bytes)
        guard manifest.Schema == 1, manifest.Repository == repository, manifest.Version.range(of:#"^\d+\.\d+\.\d+$"#,options:.regularExpression) != nil, manifest.AssetName == "ClaudeLink-\(manifest.Version)-macos-\(BuildInfo.arch).zip", manifest.Size > 0, manifest.Size < 512*1024*1024, manifest.Sha256.range(of:"^[a-f0-9]{64}$",options:.regularExpression) != nil else { throw ClientError.message("更新清单不适用于当前系统。") }; return manifest
    }
    static func check() async throws -> UpdateOffer? {
        let response = try await Web.fetch("https://api.github.com/repos/"+repository+"/releases/latest",limit:1024*1024,timeout:25)
        guard response.code == 200 else { throw ClientError.message("暂时无法检查更新（HTTP \(response.code)）。") }
        let release=Web.object(response); guard release["draft"] as? Bool == false, release["prerelease"] as? Bool == false, let assets=release["assets"] as? [[String:Any]], let tag=release["tag_name"] as? String else { throw ClientError.message("发布信息无效。") }
        func asset(_ name: String) throws -> String {
            guard let url=assets.first(where:{$0["name"] as? String == name})?["browser_download_url"] as? String, url.hasPrefix("https://github.com/"+repository+"/releases/download/"+tag+"/") else { throw ClientError.message("此版本尚未提供当前 Mac 的更新附件。") }; return url
        }
        let prefix="update-macos-"+BuildInfo.arch+".json"
        let bytes=try await Web.fetch(asset(prefix),limit:65536,timeout:25), signature=try await Web.fetch(asset(prefix+".sig"),limit:1024,timeout:25)
        let manifest=try verified(bytes.data,signature.data); guard tag == "v"+manifest.Version else { throw ClientError.message("更新版本与签名不一致。") }
        guard manifest.Version.compare(BuildInfo.version,options:.numeric) == .orderedDescending else { return nil }
        return UpdateOffer(manifest:manifest,url:try asset(manifest.AssetName),notes:release["body"] as? String ?? "")
    }
    static func download(_ offer: UpdateOffer) async throws -> URL {
        let bytes=try await Web.fetch(offer.url,limit:offer.manifest.Size,timeout:300)
        guard bytes.code == 200, bytes.data.count == offer.manifest.Size, sha256(bytes.data) == offer.manifest.Sha256 else { throw ClientError.message("更新包大小或 SHA-256 校验失败。") }
        let folder=AppPaths.root.appendingPathComponent("updates/"+UUID().uuidString); let archive=folder.appendingPathComponent("update.zip"); try AppPaths.write(bytes.data,to:archive)
        let extraction=try await Commands.run("/usr/bin/ditto",["-xk",archive.path,folder.path],timeout:60); let app=folder.appendingPathComponent("Claude Link.app")
        guard extraction.code == 0, let bundle=Bundle(url:app), bundle.bundleIdentifier == "com.slobys.ClaudeLink", bundle.infoDictionary?["CFBundleShortVersionString"] as? String == offer.manifest.Version else { throw ClientError.message("更新包内容验证失败。") }
        let codesign=try await Commands.run("/usr/bin/codesign",["--verify","--deep","--strict",app.path]); guard codesign.code == 0 else { throw ClientError.message("更新应用的代码签名不完整。") }; return app
    }
    @MainActor static func install(_ app: URL,session: Session) async throws {
        try await session.stop()
        let destination=URL(fileURLWithPath:Bundle.main.bundlePath)
        guard destination.path.hasSuffix("/Claude Link.app"), !destination.path.contains("/Volumes/") else { throw ClientError.message("请先把 Claude Link 拖入“应用程序”或个人 Applications 文件夹，再安装更新。") }
        let parent=destination.deletingLastPathComponent(), staged=parent.appendingPathComponent(".ClaudeLink-update-"+UUID().uuidString+".app"), backup=parent.appendingPathComponent(".ClaudeLink-previous-"+UUID().uuidString+".app")
        let stamp=try await Commands.run("/bin/ps",["-p",String(getpid()),"-o","lstart="]); let started=String(decoding:stamp.output,as:UTF8.self).trimmingCharacters(in:.whitespacesAndNewlines)
        // The script is authored locally; release content never supplies shell commands.
        let script="set -eu\n/usr/bin/ditto "+shellQuote(app.path)+" "+shellQuote(staged.path)+"\n/usr/bin/codesign --verify --deep --strict "+shellQuote(staged.path)+"\nwhile [ \"$(/bin/ps -p \(getpid()) -o lstart= | /usr/bin/sed 's/^ *//;s/ *$//')\" = "+shellQuote(started)+" ]; do /bin/sleep 0.2; done\n/bin/mv "+shellQuote(destination.path)+" "+shellQuote(backup.path)+"\nif /bin/mv "+shellQuote(staged.path)+" "+shellQuote(destination.path)+"; then /usr/bin/open "+shellQuote(destination.path)+"; else /bin/mv "+shellQuote(backup.path)+" "+shellQuote(destination.path)+"; exit 1; fi\n"
        let scriptPath=AppPaths.root.appendingPathComponent("updates/install-"+UUID().uuidString+".sh"); try AppPaths.write(Data(script.utf8),to:scriptPath,mode:0o700)
        if FileManager.default.isWritableFile(atPath:parent.path) { let p=Process(); p.executableURL=URL(fileURLWithPath:"/bin/sh"); p.arguments=[scriptPath.path]; p.standardOutput=FileHandle.nullDevice; p.standardError=FileHandle.nullDevice; try p.run() }
        else { throw ClientError.message("当前应用目录不可写。已下载并校验更新，请打开更新目录，将新版拖入“应用程序”覆盖安装。") }
        NSApplication.shared.terminate(nil)
    }
}
