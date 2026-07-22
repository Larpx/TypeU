#!/usr/bin/env bash
# TypeU Native AOT 发布脚本（Linux / macOS 宿主上交叉或本机发布）。
# 最低运行平台：Windows 10 1809+ / Linux x64；不支持 Windows 7。
set -euo pipefail

RUNTIME="${1:-linux-x64}"
CONFIGURATION="${2:-Release}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUTPUT_ROOT="${3:-$REPO_ROOT/artifacts/aot}"

if [[ "$RUNTIME" != "win-x64" && "$RUNTIME" != "linux-x64" ]]; then
  echo "Usage: $0 [win-x64|linux-x64] [Debug|Release] [outputRoot]" >&2
  exit 1
fi

publish_app() {
  local project="$1"
  local out_dir="$2"
  local app_name="$3"

  echo "==> Publish ${app_name} (${RUNTIME}, ${CONFIGURATION})"
  rm -rf "$out_dir"
  mkdir -p "$out_dir"

  dotnet publish "$project" \
    -c "$CONFIGURATION" \
    -r "$RUNTIME" \
    --self-contained true \
    -o "$out_dir" \
    -p:PublishAot=true \
    -p:StripSymbols=true \
    -p:DebugType=none \
    -p:DebuggerSupport=false

  if [[ "$RUNTIME" == win-* ]]; then
    exe_name="${app_name}.exe"
  else
    exe_name="${app_name}"
  fi

  target="$out_dir/$exe_name"
  if [[ ! -f "$target" ]]; then
    # 兼容项目默认输出名
    found="$(find "$out_dir" -maxdepth 1 -type f \( -name '*.exe' -o ! -name '*.*' \) \
      ! -name '*.dll' ! -name '*.json' ! -name '*.xml' ! -name '*.pdb' | head -n 1 || true)"
    if [[ -z "$found" ]]; then
      echo "Cannot locate published binary for ${app_name}" >&2
      exit 1
    fi
    mv -f "$found" "$target"
  fi

  find "$out_dir" -name '*.pdb' -delete 2>/dev/null || true
  echo "    -> $target"
}

publish_app "$REPO_ROOT/src/TypeU.Teacher.GUI/TypeU.Teacher.GUI.csproj" \
  "$OUTPUT_ROOT/$RUNTIME/Teacher" "Teacher"
publish_app "$REPO_ROOT/src/TypeU.Student.GUI/TypeU.Student.GUI.csproj" \
  "$OUTPUT_ROOT/$RUNTIME/Student" "Student"

echo "Done."
echo "  $OUTPUT_ROOT/$RUNTIME/Teacher"
echo "  $OUTPUT_ROOT/$RUNTIME/Student"
