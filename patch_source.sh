#!/usr/bin/env bash
set -euo pipefail
python3 - <<'PY'
from pathlib import Path

splash = Path('work/InstaSaver_Android_Project/app/src/main/java/com/cardinalich/instasaver/SplashActivity.java')
text = splash.read_text(encoding='utf-8')
old = 'Эксклюзивно для ООО «Золотой Фонд»'
new = 'Эксклюзивно для себя и друзей'
if old not in text and new not in text:
    raise SystemExit('Splash text marker not found')
splash.write_text(text.replace(old, new), encoding='utf-8')

gradle = Path('work/InstaSaver_Android_Project/app/build.gradle')
build = gradle.read_text(encoding='utf-8')
build = build.replace('versionCode 3', 'versionCode 4')
build = build.replace("versionName '1.0.2'", "versionName '1.0.3'")
gradle.write_text(build, encoding='utf-8')

print('Applied splash text: Эксклюзивно для себя и друзей')
PY
