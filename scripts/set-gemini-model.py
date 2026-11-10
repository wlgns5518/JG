"""Update only the non-secret model setting in the initialized app database."""
import json
import sqlite3
import sys
from pathlib import Path

database, model = sys.argv[1:]
if not model or any(char not in 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._' for char in model):
    raise SystemExit('Invalid model identifier')
with sqlite3.connect(database) as connection:
    row = connection.execute('SELECT Json FROM AppSettings WHERE Id = ?', ('settings',)).fetchone()
    settings = json.loads(row[0]) if row else {}
    settings['GeminiModel'] = model
    data_root = Path(database).resolve().parent
    for name, value in {'DatabasePath': str(Path(database).resolve()), 'LogPath': str(data_root / 'logs'), 'ProfileRoot': str(data_root / 'profiles'), 'DailyLimit': 10, 'ChromePath': ''}.items():
        if not settings.get(name):
            settings[name] = value
    connection.execute('INSERT INTO AppSettings (Id, Json) VALUES (?, ?) ON CONFLICT(Id) DO UPDATE SET Json=excluded.Json', ('settings', json.dumps(settings, ensure_ascii=False)))
print('Gemini model setting saved: ' + model)
