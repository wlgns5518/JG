"""Check deliverables for exact provided key values without printing those values."""
import json
from pathlib import Path

root = Path(__file__).resolve().parent.parent
try:
    config = json.loads((root / 'api-keys.txt').read_text(encoding='utf-8-sig'))
    needles = [config['YouTube']['ApiKey'].encode(), config['Gemini']['ApiKey'].encode()]
except Exception:
    raise SystemExit('Could not read the local key configuration. Contents were not logged.') from None
count = 0
leaks = []
for folder in ('src', 'tests', 'scripts', 'docs', 'artifacts'):
    for path in (root / folder).rglob('*'):
        if not path.is_file() or any(part in ('obj', 'bin', 'install-smoke') for part in path.relative_to(root).parts):
            continue
        with path.open('rb') as stream:
            previous = b''
            while block := stream.read(1024 * 1024):
                data = previous + block
                if any(needle and needle in data for needle in needles):
                    leaks.append(str(path.relative_to(root)))
                    break
                previous = data[-256:]
        count += 1
for name in ('README.md', '.gitignore'):
    if any(needle in (root / name).read_bytes() for needle in needles):
        leaks.append(name)
if leaks:
    raise SystemExit('Secret leak detected in: ' + ', '.join(leaks))
print(f'SECRET SCAN PASS: {count} source, documentation, log and delivery files checked. No provided plaintext key values found.')
