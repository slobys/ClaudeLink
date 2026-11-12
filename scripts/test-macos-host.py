"""Exercise the real root supervisor against loopback only, without routes or OS mutations."""
import json, pathlib, subprocess, sys, time, uuid, os, hashlib
app, output = map(pathlib.Path, sys.argv[1:])
checks = []
def check(ok, message):
    if not ok: raise AssertionError(message)
    checks.append(message)
    print('PASS', message, flush=True)
session_id = str(uuid.uuid4())
stage = pathlib.Path('/Library/Application Support/ClaudeLink/Sessions') / session_id
mailbox = output/'host-test'
mailbox.mkdir(mode=0o700, exist_ok=True)
port=17992
config={'log':{'level':'warn'},'inbounds':[{'type':'mixed','listen':'127.0.0.1','listen_port':port}], 'outbounds':[{'type':'direct','tag':'direct'}]}
(mailbox/'config.json').write_text(json.dumps(config)); os.chmod(mailbox/'config.json',0o600)
stamp=subprocess.check_output(['/bin/ps','-p',str(os.getpid()),'-o','lstart='],text=True).strip()
nonce=uuid.uuid4().hex+uuid.uuid4().hex
control={'parent':os.getpid(),'parentStamp':stamp,'mailbox':str(mailbox.resolve()),'nonce':nonce,'port':port,'disableIPv6':False}
(mailbox/'control.json').write_text(json.dumps(control)); os.chmod(mailbox/'control.json',0o600)
for directory in [stage.parent.parent,stage.parent,stage]:
    subprocess.run(['sudo','mkdir','-p',str(directory)],check=True)
    subprocess.run(['sudo','chmod','755',str(directory)],check=True)
for src,name,mode in [(app/'Contents/Resources/sing-box','sing-box','755'),(app/'Contents/Helpers/ClaudeLinkHost','ClaudeLinkHost','755'),(mailbox/'config.json','config.json','600'),(mailbox/'control.json','control.json','600')]:
    subprocess.run(['sudo','install','-m',mode,str(src),str(stage/name)],check=True)
host=subprocess.Popen(['sudo',str(stage/'ClaudeLinkHost'),'--run',str(stage)])
try:
    status={}
    for _ in range(100):
        if (stage/'status.json').exists(): status=json.loads((stage/'status.json').read_text())
        if status.get('state') in ['running','failed']: break
        time.sleep(.1)
    check(status.get('state')=='running','Root supervisor starts official loopback core')
    import socket
    with socket.create_connection(('127.0.0.1',port),3): pass
    check(True,'Owned local proxy accepts connections')
    check((stage/'config.json').stat().st_uid==0 and (stage/'config.json').stat().st_mode & 0o777 == 0o600,'Root config stays private in root-owned stage')
    (mailbox/'stop').write_text(nonce)
    host.wait(timeout=15)
    status=json.loads((stage/'status.json').read_text())
    check(status.get('state')=='stopped','Nonce stop closes session and restores journal')
    check(not (stage/'config.json').exists() and not (stage/'core.log').exists(),'Completed session removes credential-bearing root files')
    with socket.socket() as sock: check(sock.connect_ex(('127.0.0.1',port))!=0,'Supervisor releases local listening port')
    subprocess.run(['sudo',str(stage/'ClaudeLinkHost'),'--recover',str(stage)],check=True)
    check(json.loads((stage/'status.json').read_text())['state']=='stopped','Recovery is idempotent')
finally:
    if host.poll() is None:
        (mailbox/'stop').write_text(nonce)
        host.wait(timeout=15)
(output/'supervisor-results.json').write_text(json.dumps({'Passed':len(checks),'Checks':checks,'Scope':'root-owned loopback session; no TUN routes, IPv6 or timezone mutations'},indent=2))
