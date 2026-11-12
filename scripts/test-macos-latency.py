"""Disposable loopback TLS/proxy fixtures; no account, TUN or system environment changes."""
import concurrent.futures
import contextlib
import http.server
import json
import os
from pathlib import Path
import shutil
import socket
import socketserver
import ssl
import subprocess
import sys
import tempfile
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

app, output = map(Path, sys.argv[1:3])
checks = []
secret = uuid.uuid4().hex + uuid.uuid4().hex
temp = Path(tempfile.mkdtemp(prefix="ClaudeLink-LatencyFixture-"))
roots = []
servers = []
helpers = []
owners = []
opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))

def check(condition, description):
    if not condition:
        raise AssertionError(description)
    checks.append(description)
    print("PASS " + description, flush=True)

class TLSHandler(http.server.BaseHTTPRequestHandler):
    requests = 0
    def do_HEAD(self):
        type(self).requests += 1
        time.sleep(.03)
        self.send_response(204)
        self.end_headers()
    def log_message(self, *args):
        pass

class Server(socketserver.ThreadingTCPServer):
    daemon_threads = True
    allow_reuse_address = True

def start(server):
    threading.Thread(target=server.serve_forever, daemon=True).start()
    servers.append(server)
    return server.server_address[1]

def relay(target, delay):
    class Relay(socketserver.BaseRequestHandler):
        requests = 0
        def handle(self):
            try:
                self.request.settimeout(5)
                header = b""
                while b"\r\n\r\n" not in header and len(header) < 8192:
                    part = self.request.recv(1)
                    if not part:
                        return
                    header += part
                if b"dXNlcjpzZWNyZXQ=" not in header:
                    self.request.sendall(b"HTTP/1.1 407 Proxy Authentication Required\r\n\r\n")
                    return
                type(self).requests += 1
                time.sleep(delay)
                with socket.create_connection(("127.0.0.1", target), 3) as upstream:
                    self.request.sendall(b"HTTP/1.1 200 Connection established\r\n\r\n")
                    import select
                    while True:
                        readable, _, _ = select.select([self.request, upstream], [], [], 5)
                        if not readable:
                            return
                        for source in readable:
                            data = source.recv(65536)
                            if not data:
                                return
                            (upstream if source is self.request else self.request).sendall(data)
            except OSError:
                pass
    return start(Server(("127.0.0.1", 0), Relay)), Relay

def api(port, path, auth=True):
    request = urllib.request.Request(f"http://127.0.0.1:{port}/{path}")
    if auth:
        request.add_header("Authorization", "Bearer " + secret)
    try:
        with opener.open(request, timeout=4) as response:
            return response.status, json.load(response)
    except urllib.error.HTTPError as error:
        return error.code, {}

def open_port(port):
    with socket.socket() as connection:
        connection.settimeout(.2)
        return connection.connect_ex(("127.0.0.1", port)) == 0

def launch(nodes, all_nodes, owner):
    with socket.socket() as reserve:
        reserve.bind(("127.0.0.1", 0))
        port = reserve.getsockname()[1]
    request = dict(operation="latency-config", selected=nodes, nodes=all_nodes, port=port, secret=secret)
    bridge = app / "Contents/Resources/bridge/ClaudeLink.Bridge"
    plan = json.loads(subprocess.check_output([str(bridge)], input=json.dumps(request).encode()))
    config = plan["Config"]
    config["certificate"] = {"store": "none", "certificate": (temp / "cert.pem").read_text()}
    root = Path(tempfile.gettempdir()).resolve() / ("ClaudeLink-Latency-" + str(uuid.uuid4()))
    root.mkdir(mode=0o700)
    roots.append(root)
    (root / "config.json").write_text(json.dumps(config))
    stamp = subprocess.check_output(["/bin/ps", "-p", str(owner), "-o", "lstart="]).decode().strip()
    (root / "control.json").write_text(json.dumps(dict(parent=owner, stamp=stamp)))
    child = subprocess.Popen([str(app / "Contents/Helpers/ClaudeLinkHost"), "--latency", str(root)])
    helpers.append((child, root))
    for _ in range(70):
        try:
            if api(port, "version")[0] == 200:
                return child, root, port, plan
        except OSError:
            pass
        if child.poll() is not None:
            raise AssertionError("Latency guardian exited before readiness")
        time.sleep(.1)
    raise AssertionError("Latency guardian readiness timed out")

try:
    subprocess.run(["openssl", "req", "-x509", "-newkey", "rsa:2048", "-nodes", "-keyout", str(temp / "key.pem"), "-out", str(temp / "cert.pem"), "-days", "1", "-subj", "/CN=localhost", "-addext", "subjectAltName=IP:127.0.0.1"], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    origin = Server(("127.0.0.1", 0), TLSHandler)
    tls = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
    tls.load_cert_chain(str(temp / "cert.pem"), str(temp / "key.pem"))
    origin.socket = tls.wrap_socket(origin.socket, server_side=True)
    target_port = start(origin)
    fast, fast_class = relay(target_port, .04)
    slow, _ = relay(target_port, .28)
    timeout, _ = relay(target_port, 2.2)
    entry, entry_class = relay(fast, .02)
    def node(identity, port, auth=True):
        return dict(Id=identity, Name="Fixture", GroupId="manual", OutboundJson=json.dumps(dict(type="http", server="127.0.0.1", server_port=port, username="user" if auth else "bad", password="secret" if auth else "bad")), ChainNodeIds=[])
    nodes = [node("fast", fast), node("slow", slow), node("timeout", timeout), node("bad", fast, False)]
    entry_node = node("entry", entry)
    nodes.append(dict(Id="chain", Name="Fixture chain", OutboundJson="{}", ChainNodeIds=["entry", "fast"]))
    child, root, port, plan = launch(nodes, nodes + [entry_node], os.getpid())
    check(plan["Config"]["inbounds"] == [] and api(port, "version", False)[0] == 401, "Latency controller is authenticated, loopback only, and has no traffic or TUN inbound")
    target = f"https://127.0.0.1:{target_port}/latency"
    def measure(identity):
        query = urllib.parse.urlencode(dict(timeout=900, url=target))
        return identity, api(port, f"proxies/{plan['Tags'][identity]}/delay?{query}")
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        results = dict(pool.map(measure, [n["Id"] for n in nodes]))
    check(results["fast"][0] == results["slow"][0] == 200 and results["fast"][1]["delay"] < results["slow"][1]["delay"], "Real proxied TLS HEAD delays preserve fast/slow order")
    check(results["timeout"][0] == 504 and results["bad"][0] == 503, "Core distinguishes timeout and rejected proxy authentication")
    check(results["chain"][0] == 200 and entry_class.requests > 0 and fast_class.requests >= 2 and TLSHandler.requests == 3, "Chain measurements traverse both hops and failed nodes cannot bypass to the target")
    (root / "cancel").touch()
    child.wait(timeout=8)
    check(not open_port(port) and not (root / "config.json").exists(), "Cancel stops the owned core and removes credential-bearing configuration")
    owner = subprocess.Popen([sys.executable, "-c", "import time;time.sleep(60)"])
    owners.append(owner)
    child, root, port, _ = launch(nodes[:1], nodes, owner.pid)
    owner.terminate()
    owner.wait(timeout=5)
    child.wait(timeout=8)
    check(not open_port(port) and not (root / "config.json").exists(), "Parent death also stops the unprivileged latency core without environment changes")
    output.write_text(json.dumps(dict(Passed=len(checks), Checks=checks, LoopbackOnly=True, LiveTunTested=False), indent=2))
finally:
    for child, root in helpers:
        if child.poll() is None:
            (root / "cancel").touch()
            with contextlib.suppress(subprocess.TimeoutExpired):
                child.wait(timeout=8)
    for owner in owners:
        if owner.poll() is None:
            owner.terminate()
            owner.wait(timeout=5)
    for server in servers:
        server.shutdown()
        server.server_close()
    for root in roots:
        if not any(c.poll() is None and r == root for c, r in helpers):
            shutil.rmtree(root)
    shutil.rmtree(temp)
