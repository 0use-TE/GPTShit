"""Build the ESP32 LittleFS image. The checked-in image makes normal app runs tool-free."""
from pathlib import Path
import subprocess
import sys

root = Path(__file__).resolve().parents[1]
libraries = root / 'work' / 'python-libs'
sys.path.insert(0, str(libraries))
try:
    from littlefs import LittleFS
except ImportError:
    subprocess.run([sys.executable, '-m', 'pip', 'install', '--target', str(libraries),
                    'littlefs-python==0.19.0'], check=True)
    from littlefs import LittleFS

filesystem = LittleFS(block_size=4096, block_count=512, read_size=32, prog_size=32)
for name in ('main.py', 'pid.py'):
    with filesystem.open(name, 'w') as file:
        file.write((root / 'firmware' / name).read_text(encoding='utf-8'))
output = root / 'firmware' / 'runtime' / 'filesystem.bin'
output.parent.mkdir(parents=True, exist_ok=True)
output.write_bytes(filesystem.context.buffer)
print('ESP32 firmware filesystem built.')
