#!/usr/bin/env bash
# TypeU 跨平台 AOT 发布脚本（Linux/macOS Bash 版）。
# 用法：./publish.sh [runtime-identifier] [configuration]
# 默认：linux-x64 Release
# 可选 RID：linux-x64、linux-arm64、win-x64、win-arm64

set -euo pipefail

RID="${1:-linux-x64}"
CONFIG="${2:-Release}"

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PUBLISH_ROOT="$ROOT/publish/$RID"

echo "=== TypeU AOT 发布 ==="
echo "RuntimeIdentifier : $RID"
echo "Configuration     : $CONFIG"
echo "Output            : $PUBLISH_ROOT"
echo ""

PROJECTS=(
    "Teacher:src/TypeU.Teacher.GUI/TypeU.Teacher.GUI.csproj"
    "Student:src/TypeU.Student.GUI/TypeU.Student.GUI.csproj"
)

for entry in "${PROJECTS[@]}"; do
    NAME="${entry%%:*}"
    PROJ_PATH="${entry##*:}"
    OUT_DIR="$PUBLISH_ROOT/$NAME"

    echo "[$NAME] 发布中..."
    dotnet publish "$ROOT/$PROJ_PATH" \
        -c "$CONFIG" \
        -r "$RID" \
        -o "$OUT_DIR" \
        /p:PublishAot=true \
        /p:StripSymbols=true \
        /p:DebugType=none \
        /p:PublishSingleFile=true

    if [ $? -ne 0 ]; then
        echo "[$NAME] 发布失败"
        exit 1
    fi

    # 产物校验：确认无项目自身的 PDB 文件（第三方原生库如 SkiaSharp/HarfBuzzSharp 的 pdb 可接受）。
    if find "$OUT_DIR" -name 'TypeU.*.pdb' | grep -q .; then
        echo "[$NAME] 警告：发现项目 PDB 文件"
        find "$OUT_DIR" -name 'TypeU.*.pdb'
        exit 1
    fi

    # 列出主可执行文件。
    if [[ "$RID" == win-* ]]; then
        MAIN_EXE=$(find "$OUT_DIR" -maxdepth 1 -name 'TypeU.*.exe' | head -n 1)
    else
        MAIN_EXE=$(find "$OUT_DIR" -maxdepth 1 -type f -name 'TypeU.*' -executable | head -n 1)
    fi

    if [ -n "$MAIN_EXE" ]; then
        SIZE_MB=$(du -m "$MAIN_EXE" | cut -f1)
        echo "[$NAME] 产物：$(basename "$MAIN_EXE") (${SIZE_MB} MB)"
    fi
    echo ""
done

echo "=== 发布完成 ==="
echo "产物目录：$PUBLISH_ROOT"
