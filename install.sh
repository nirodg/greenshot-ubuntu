#!/usr/bin/env bash
# install.sh - Build and install Greenshot Linux port
set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

info()    { echo -e "${CYAN}[INFO]${NC}  $*"; }
success() { echo -e "${GREEN}[OK]${NC}    $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC}  $*"; }
die()     { echo -e "${RED}[ERROR]${NC} $*" >&2; exit 1; }

SUDO=""
if [[ ${EUID} -ne 0 ]]; then
    command -v sudo >/dev/null 2>&1 || die "This installer needs root privileges (sudo not found)."
    SUDO="sudo"
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC_DIR="${SCRIPT_DIR}/src"
OUT_DIR="${SCRIPT_DIR}/.build_output"

INSTALL_PREFIX="/usr/local"
INSTALL_BIN="${INSTALL_PREFIX}/bin/greenshot"
INSTALL_DESKTOP="/usr/share/applications/greenshot.desktop"
INSTALL_ICON="/usr/share/pixmaps/greenshot.ico"

SKIP_DOTNET=0
SKIP_DEPS=0
SKIP_BUILD=0
NO_AUTOSTART=0

usage() {
    cat <<'EOF'
Usage: ./install.sh [options]

Options:
  --skip-dotnet      Skip .NET SDK installation/check
  --skip-deps        Skip runtime dependency installation
  --skip-build       Skip build step and use existing .build_output/Greenshot
  --no-autostart     Do not create ~/.config/autostart/greenshot.desktop
  --prefix <path>    Install binary under prefix (default: /usr/local)
  -h, --help         Show help
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --skip-dotnet)
            SKIP_DOTNET=1
            shift
            ;;
        --skip-deps)
            SKIP_DEPS=1
            shift
            ;;
        --skip-build)
            SKIP_BUILD=1
            shift
            ;;
        --no-autostart)
            NO_AUTOSTART=1
            shift
            ;;
        --prefix)
            [[ $# -ge 2 ]] || die "--prefix requires a value"
            INSTALL_PREFIX="$2"
            INSTALL_BIN="${INSTALL_PREFIX}/bin/greenshot"
            shift 2
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            die "Unknown option: $1"
            ;;
    esac
done

TARGET_USER="${SUDO_USER:-$(id -un)}"
TARGET_HOME="$(getent passwd "${TARGET_USER}" | cut -d: -f6)"
[[ -n "${TARGET_HOME}" ]] || die "Could not determine home directory for user ${TARGET_USER}."

run_as_target_user() {
    if [[ ${EUID} -eq 0 && -n "${SUDO_USER:-}" ]]; then
        sudo -u "${TARGET_USER}" "$@"
    else
        "$@"
    fi
}

check_ubuntu() {
    [[ -f /etc/os-release ]] || die "Cannot detect OS (missing /etc/os-release)."
    # shellcheck source=/dev/null
    source /etc/os-release
    [[ "${ID:-}" == "ubuntu" ]] || die "This installer currently supports Ubuntu only."
    info "Detected Ubuntu ${VERSION_ID:-unknown}."
}

ensure_dotnet() {
    if command -v dotnet >/dev/null 2>&1; then
        local major
        major="$(dotnet --version | cut -d. -f1)"
        if [[ "${major}" == "8" ]]; then
            success "dotnet $(dotnet --version) already installed."
            return
        fi
        warn "Detected dotnet ${major}.x, installing dotnet 8 SDK as required."
    fi

    info "Installing .NET 8 SDK..."
    ${SUDO} apt-get update -qq

    if ${SUDO} apt-get install -y dotnet-sdk-8.0 >/dev/null 2>&1; then
        success "Installed dotnet-sdk-8.0 from Ubuntu repositories."
    else
        warn "dotnet-sdk-8.0 not available in current apt sources. Adding Microsoft feed."
        local deb="/tmp/packages-microsoft-prod.deb"
        if command -v curl >/dev/null 2>&1; then
            curl -fsSL "https://packages.microsoft.com/config/ubuntu/${VERSION_ID}/packages-microsoft-prod.deb" -o "${deb}"
        elif command -v wget >/dev/null 2>&1; then
            wget -q "https://packages.microsoft.com/config/ubuntu/${VERSION_ID}/packages-microsoft-prod.deb" -O "${deb}"
        else
            die "Need curl or wget to download Microsoft package feed setup."
        fi
        ${SUDO} dpkg -i "${deb}" >/dev/null
        rm -f "${deb}"
        ${SUDO} apt-get update -qq
        ${SUDO} apt-get install -y dotnet-sdk-8.0
        success "Installed dotnet-sdk-8.0 from Microsoft feed."
    fi

    command -v dotnet >/dev/null 2>&1 || die "dotnet command not found after installation."
}

install_runtime_deps() {
    info "Installing runtime dependencies..."
    local pkgs=(
        libx11-6
        libgtk-3-0
        libnotify-bin
        xclip
        xsel
    )

    ${SUDO} apt-get update -qq
    if apt-cache show wl-clipboard >/dev/null 2>&1; then
        pkgs+=(wl-clipboard)
    fi

    ${SUDO} apt-get install -y "${pkgs[@]}"
    success "Runtime dependencies installed."
}

build_app() {
    [[ -f "${SRC_DIR}/Greenshot/Greenshot.csproj" ]] || die "Project file not found at ${SRC_DIR}/Greenshot/Greenshot.csproj"

    local arch runtime
    arch="$(uname -m)"
    case "${arch}" in
        x86_64) runtime="linux-x64" ;;
        aarch64) runtime="linux-arm64" ;;
        armv7l) runtime="linux-arm" ;;
        *) die "Unsupported architecture: ${arch}" ;;
    esac

    info "Building Greenshot (${runtime})..."
    rm -rf "${OUT_DIR}"
    mkdir -p "${OUT_DIR}"

    dotnet publish "${SRC_DIR}/Greenshot/Greenshot.csproj" \
        --configuration Release \
        --runtime "${runtime}" \
        --self-contained true \
        --output "${OUT_DIR}" \
        -p:PublishSingleFile=true \
        -p:PublishReadyToRun=true

    [[ -x "${OUT_DIR}/Greenshot" ]] || die "Build did not produce ${OUT_DIR}/Greenshot"
    success "Build complete: ${OUT_DIR}/Greenshot"
}

install_files() {
    [[ -f "${OUT_DIR}/Greenshot" ]] || die "Missing ${OUT_DIR}/Greenshot. Run build or remove --skip-build."

    info "Installing Greenshot binary to ${INSTALL_BIN}"
    ${SUDO} mkdir -p "$(dirname "${INSTALL_BIN}")"
    ${SUDO} install -m 755 "${OUT_DIR}/Greenshot" "${INSTALL_BIN}"

    if [[ -f "${SRC_DIR}/Greenshot/Assets/greenshot.ico" ]]; then
        ${SUDO} install -m 644 "${SRC_DIR}/Greenshot/Assets/greenshot.ico" "${INSTALL_ICON}"
    else
        warn "Icon not found at ${SRC_DIR}/Greenshot/Assets/greenshot.ico"
    fi

    info "Installing desktop entry to ${INSTALL_DESKTOP}"
    ${SUDO} tee "${INSTALL_DESKTOP}" >/dev/null <<EOF
[Desktop Entry]
Name=Greenshot
Comment=Screenshot tool for Linux
Exec=${INSTALL_BIN}
Icon=${INSTALL_ICON}
Type=Application
Categories=Graphics;Photography;Utility;
Keywords=screenshot;capture;screen;annotation;
StartupNotify=false
X-GNOME-Autostart-enabled=true
EOF
    ${SUDO} chmod 644 "${INSTALL_DESKTOP}"

    if command -v update-desktop-database >/dev/null 2>&1; then
        ${SUDO} update-desktop-database /usr/share/applications >/dev/null 2>&1 || true
    fi
}

install_autostart() {
    [[ ${NO_AUTOSTART} -eq 0 ]] || {
        info "Skipping autostart setup (--no-autostart)."
        return
    }

    info "Creating autostart entry for user ${TARGET_USER}"
    local autostart_dir="${TARGET_HOME}/.config/autostart"
    local autostart_file="${autostart_dir}/greenshot.desktop"

    run_as_target_user mkdir -p "${autostart_dir}"
    run_as_target_user bash -c "cat > '${autostart_file}' <<'EOF'
[Desktop Entry]
Name=Greenshot
Comment=Screenshot tool for Linux
Exec=${INSTALL_BIN}
Icon=${INSTALL_ICON}
Type=Application
X-GNOME-Autostart-enabled=true
EOF"
}

create_user_config_dir() {
    local cfg_dir="${TARGET_HOME}/.config/greenshot"
    run_as_target_user mkdir -p "${cfg_dir}"
    success "Config directory ready: ${cfg_dir}"
}

main() {
    echo
    echo -e "${CYAN}Greenshot Linux Installer${NC}"
    echo

    check_ubuntu
    [[ ${SKIP_DOTNET} -eq 1 ]] || ensure_dotnet
    [[ ${SKIP_DEPS} -eq 1 ]] || install_runtime_deps
    [[ ${SKIP_BUILD} -eq 1 ]] || build_app
    install_files
    install_autostart
    create_user_config_dir

    echo
    success "Installation complete."
    echo "Run: ${INSTALL_BIN}"
    echo
}

main "$@"
