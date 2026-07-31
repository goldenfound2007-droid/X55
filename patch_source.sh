#!/usr/bin/env bash
set -euo pipefail

cat overlay.part00 overlay.part01 overlay.part02 overlay.part03 overlay.part04 overlay.part05 overlay.part06 > overlay_v104.tar.gz.b64
base64 -d overlay_v104.tar.gz.b64 > overlay_v104.tar.gz
tar -xzf overlay_v104.tar.gz -C work/InstaSaver_Android_Project

echo "Applied InstaSaver 1.0.4 source overlay"
grep -R "versionName" work/InstaSaver_Android_Project/app/build.gradle
find work/InstaSaver_Android_Project/app/src/main/java/com/cardinalich/instasaver -maxdepth 1 -type f -printf '%f\n' | sort
