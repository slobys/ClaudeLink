"""Compatibility entry point; the native renderer reads design/brand.svg."""
from pathlib import Path
import subprocess

script = Path(__file__).with_suffix('.ps1')
subprocess.run(['pwsh', '-NoProfile', '-File', str(script)], check=True)
