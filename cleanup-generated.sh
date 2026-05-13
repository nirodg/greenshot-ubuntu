#!/usr/bin/env bash
# cleanup-generated.sh - Remove generated build artifacts for the Linux port
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC_DIR="${SCRIPT_DIR}/src"

remove_dir_if_exists() {
    local dir="$1"
    if [[ -d "${dir}" ]]; then
        rm -rf "${dir}"
        echo "Removed ${dir}"
    fi
}

remove_file_if_exists() {
    local file="$1"
    if [[ -f "${file}" ]]; then
        rm -f "${file}"
        echo "Removed ${file}"
    fi
}

echo "Cleaning generated artifacts..."

# Installer/build output directories
remove_dir_if_exists "${SCRIPT_DIR}/.build_output"
remove_dir_if_exists "${SCRIPT_DIR}/publish"

# Remove per-project build artifacts under src
if [[ -d "${SRC_DIR}" ]]; then
    while IFS= read -r -d '' dir; do
        rm -rf "${dir}"
        echo "Removed ${dir}"
    done < <(find "${SRC_DIR}" -type d \( -name bin -o -name obj \) -print0)
fi

# Remove package artifacts created during local packaging
while IFS= read -r -d '' zip_file; do
    rm -f "${zip_file}"
    echo "Removed ${zip_file}"
done < <(find "${SCRIPT_DIR}" -maxdepth 1 -type f -name '*.zip' -print0)

# Remove common crash/core dump artifacts if present
remove_file_if_exists "${SCRIPT_DIR}/core"
while IFS= read -r -d '' core_file; do
    rm -f "${core_file}"
    echo "Removed ${core_file}"
done < <(find "${SCRIPT_DIR}" -maxdepth 1 -type f -name 'core.*' -print0)

# Also clean ignored/untracked generated files via git when available.
# -X limits cleanup to ignored files only, so tracked edits are preserved.
if command -v git >/dev/null 2>&1 && git -C "${SCRIPT_DIR}" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    echo "Running git clean for ignored generated files..."
    git -C "${SCRIPT_DIR}" clean -fdX
fi

echo "Cleanup complete."
