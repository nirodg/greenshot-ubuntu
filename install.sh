#!/usr/bin/env bash
# install.sh — Detect Ubuntu version, install .NET 8 SDK, build and install Greenshot
set -euo pipefail

# ── Colour helpers ──────────────────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; CYAN='\033[0;36m'; NC='\033[0m'
info()    { echo -e "${CYAN}[INFO]${NC}  $*"; }
success() { echo -e "${GREEN}[OK]${NC}    $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC}  $*"; }
die()     { echo -e "${RED}[ERROR]${NC} $*" >&2; exit 1; }

# ── Require root (or sudo available) ───────────────────────────────────────
SUDO=""
if [[ $EUID -ne 0 ]]; then
    command -v sudo &>/dev/null || die "This script needs root or sudo."
    SUDO="sudo"
    info "Running as non-root; will use sudo for system operations."
fi

# ── Detect Ubuntu version ──────────────────────────────────────────────────
[[ -f /etc/os-release ]] || die "/etc/os-release not found — is this Ubuntu?"
# shellcheck source=/dev/null
source /etc/os-release

[[ "${ID:-}" == "ubuntu" ]] || die "This script targets Ubuntu. Detected: ${ID:-unknown}"

VERSION_ID="${VERSION_ID:-}"   # e.g. "22.04"
CODENAME="${UBUNTU_CODENAME:-${VERSION_CODENAME:-}}"  # e.g. "jammy"

# Extract major version number (20, 22, 24 …)
MAJOR_VER="${VERSION_ID%%.*}"
[[ -n "$MAJOR_VER" ]] || die "Could not parse Ubuntu major version from VERSION_ID='$VERSION_ID'."

info "Detected Ubuntu ${VERSION_ID} (${CODENAME}), major=${MAJOR_VER}"

# ── Architecture mapping ───────────────────────────────────────────────────
ARCH="$(uname -m)"
case "$ARCH" in
    x86_64)  DOTNET_ARCH="x64"  ; APT_ARCH="amd64" ;;
    aarch64) DOTNET_ARCH="arm64"; APT_ARCH="arm64"  ;;
    armv7l)  DOTNET_ARCH="arm"  ; APT_ARCH="armhf"  ;;
    *)       die "Unsupported architecture: $ARCH" ;;
esac
info "Architecture: ${ARCH} → dotnet=${DOTNET_ARCH}, apt=${APT_ARCH}"

# ── Paths ──────────────────────────────────────────────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC_DIR="${SCRIPT_DIR}/src"
BUILD_DIR="${SCRIPT_DIR}/.build_output"
INSTALL_BIN="/usr/local/bin/greenshot"
INSTALL_DESKTOP="/usr/share/applications/greenshot.desktop"
INSTALL_ICON_DIR="/usr/share/icons/hicolor/128x128/apps"
DOTNET_SDK_VERSION="8.0"

# ── Step 1: Install .NET 8 SDK ─────────────────────────────────────────────
install_dotnet_via_ubuntu_packages() {
    # Ubuntu 24.04 (Noble) ships dotnet-sdk-8.0 in the universe repo directly.
    info "Installing .NET SDK ${DOTNET_SDK_VERSION} from Ubuntu packages..."
    $SUDO apt-get update -qq
    $SUDO apt-get install -y dotnet-sdk-${DOTNET_SDK_VERSION}
}

install_dotnet_via_microsoft_repo() {
    local feed_url="https://packages.microsoft.com/config/ubuntu/${VERSION_ID}/packages-microsoft-prod.deb"
    local tmp_deb="/tmp/packages-microsoft-prod.deb"

    info "Installing .NET SDK ${DOTNET_SDK_VERSION} via Microsoft package feed (${feed_url})..."

    # Download the Microsoft repo config package
    if command -v curl &>/dev/null; then
        curl -sSL "$feed_url" -o "$tmp_deb"
    elif command -v wget &>/dev/null; then
        wget -q "$feed_url" -O "$tmp_deb"
    else
        die "Neither curl nor wget is available. Install one and retry."
    fi

    $SUDO dpkg -i "$tmp_deb"
    rm -f "$tmp_deb"

    $SUDO apt-get update -qq
    $SUDO apt-get install -y dotnet-sdk-${DOTNET_SDK_VERSION}
}

install_dotnet_via_snap() {
    info "Installing .NET SDK ${DOTNET_SDK_VERSION} via snap..."
    $SUDO snap install dotnet-sdk --classic --channel=8.0/stable
    $SUDO snap alias dotnet-sdk.dotnet dotnet
}

install_dotnet_via_binaries() {
    # Last-resort: download the official .NET install script from Microsoft
    info "Falling back to Microsoft's dotnet-install.sh script..."
    local tmp_script="/tmp/dotnet-install.sh"

    if command -v curl &>/dev/null; then
        curl -sSL "https://dot.net/v1/dotnet-install.sh" -o "$tmp_script"
    elif command -v wget &>/dev/null; then
        wget -q "https://dot.net/v1/dotnet-install.sh" -O "$tmp_script"
    else
        die "Cannot download dotnet-install.sh: no curl or wget."
    fi

    chmod +x "$tmp_script"

    # Install into /usr/local/dotnet so it's system-wide
    local dotnet_root="/usr/local/dotnet"
    $SUDO bash "$tmp_script" \
        --channel "${DOTNET_SDK_VERSION}" \
        --install-dir "$dotnet_root" \
        --no-path
    rm -f "$tmp_script"

    # Symlink so 'dotnet' is on PATH for all users
    $SUDO ln -sf "${dotnet_root}/dotnet" /usr/local/bin/dotnet
    info "dotnet installed at ${dotnet_root}, symlinked to /usr/local/bin/dotnet"
}

ensure_dotnet() {
    # Already installed and correct major version?
    if command -v dotnet &>/dev/null; then
        local installed_ver
        installed_ver="$(dotnet --version 2>/dev/null | cut -d. -f1)"
        if [[ "$installed_ver" == "8" ]]; then
            success "dotnet $(dotnet --version) already installed — skipping SDK installation."
            return 0
        fi
        warn "Found dotnet ${installed_ver}.x — need 8.x; will install SDK ${DOTNET_SDK_VERSION} alongside."
    fi

    # Strategy selection by Ubuntu version:
    #   24.04 (Noble)  → Ubuntu universe packages (dotnet-sdk-8.0 available natively)
    #   22.04 (Jammy)  → Ubuntu universe packages also work (added in 22.04.2)
    #   20.04 (Focal)  → Microsoft APT feed
    #   18.04 (Bionic) → Microsoft APT feed
    #   other          → dotnet-install.sh binary fallback
    case "$MAJOR_VER" in
        24|23)
            if $SUDO apt-get install -y --dry-run dotnet-sdk-${DOTNET_SDK_VERSION} &>/dev/null 2>&1; then
                install_dotnet_via_ubuntu_packages
            else
                warn "Ubuntu package dotnet-sdk-${DOTNET_SDK_VERSION} not found; trying Microsoft feed."
                install_dotnet_via_microsoft_repo || install_dotnet_via_binaries
            fi
            ;;
        22)
            # Try Ubuntu universe first (available since 22.04.2), fall back to MS feed
            $SUDO apt-get update -qq
            if apt-cache show dotnet-sdk-${DOTNET_SDK_VERSION} &>/dev/null 2>&1; then
                install_dotnet_via_ubuntu_packages
            else
                install_dotnet_via_microsoft_repo || install_dotnet_via_binaries
            fi
            ;;
        20|18)
            install_dotnet_via_microsoft_repo || install_dotnet_via_binaries
            ;;
        *)
            warn "Unknown Ubuntu major version ${MAJOR_VER}; using dotnet-install.sh fallback."
            install_dotnet_via_binaries
            ;;
    esac

    # Verify
    command -v dotnet &>/dev/null || die "dotnet not on PATH after installation. Check the output above."
    local ver
    ver="$(dotnet --version)"
    success ".NET SDK ${ver} installed."
}

# ── Step 2: Install runtime dependencies ──────────────────────────────────
install_runtime_deps() {
    info "Installing runtime dependencies..."

    # Base X11 / GTK libs (Avalonia needs these)
    local pkgs=(
        libx11-6          # X11 client library (screen capture, hotkeys)
        libfontconfig1    # Font rendering
        libice6 libsm6    # X11 session management
        libxt6            # X Toolkit intrinsics
        # GTK3 (Avalonia GTK backend)
        libgtk-3-0
        libglib2.0-0
        libgdk-pixbuf-2.0-0
        libpango-1.0-0
        libcairo2
        # Notifications
        libnotify-bin     # provides notify-send
        # Clipboard tools (install both; destination picks whichever is present)
        xclip
        xsel
    )

    # wl-clipboard for Wayland clipboard support (not available on all versions)
    if apt-cache show wl-clipboard &>/dev/null 2>&1; then
        pkgs+=(wl-clipboard)
    fi

    $SUDO apt-get install -y "${pkgs[@]}"
    success "Runtime dependencies installed."
}

# ── Step 3: Build the application ─────────────────────────────────────────
build_app() {
    info "Building Greenshot..."

    [[ -f "${SRC_DIR}/Greenshot.sln" ]] || \
        die "Solution file not found at ${SRC_DIR}/Greenshot.sln. Run this script from the ported/ directory."

    rm -rf "$BUILD_DIR"
    mkdir -p "$BUILD_DIR"

    dotnet publish \
        "${SRC_DIR}/Greenshot/Greenshot.csproj" \
        --configuration Release \
        --runtime "linux-${DOTNET_ARCH}" \
        --self-contained true \
        --output "$BUILD_DIR" \
        -p:PublishSingleFile=true \
        -p:PublishReadyToRun=true \
        -p:DebugType=none \
        --verbosity quiet

    [[ -f "${BUILD_DIR}/Greenshot" ]] || \
        die "Build succeeded but output binary not found at ${BUILD_DIR}/Greenshot."

    success "Build complete → ${BUILD_DIR}/Greenshot"
}

# ── Step 4: Install the binary ────────────────────────────────────────────
install_app() {
    info "Installing Greenshot to system..."

    # Binary
    $SUDO install -m 755 "${BUILD_DIR}/Greenshot" "${INSTALL_BIN}"
    success "Binary installed: ${INSTALL_BIN}"

    # Icon (convert the .ico to PNG for Linux desktop integration)
    $SUDO mkdir -p "$INSTALL_ICON_DIR"
    local icon_src="${SRC_DIR}/Greenshot/Assets/greenshot.ico"
    if [[ -f "$icon_src" ]]; then
        if command -v convert &>/dev/null; then
            $SUDO convert "${icon_src}[3]" "${INSTALL_ICON_DIR}/greenshot.png" 2>/dev/null || \
            $SUDO convert "${icon_src}"    "${INSTALL_ICON_DIR}/greenshot.png" 2>/dev/null || true
        elif command -v ffmpeg &>/dev/null; then
            $SUDO ffmpeg -i "$icon_src" "${INSTALL_ICON_DIR}/greenshot.png" -y -loglevel quiet 2>/dev/null || true
        else
            warn "ImageMagick (convert) not found — skipping PNG icon conversion. Install imagemagick for a proper tray icon."
            # Copy ICO as-is; Avalonia can load it at runtime
        fi
        success "Icon installed: ${INSTALL_ICON_DIR}/greenshot.png"
    else
        warn "Icon source not found at ${icon_src}."
    fi

    # .desktop file
    $SUDO tee "${INSTALL_DESKTOP}" > /dev/null <<DESKTOP
[Desktop Entry]
Name=Greenshot
Comment=Screenshot tool for Linux — capture, annotate, share
Exec=${INSTALL_BIN}
Icon=greenshot
Type=Application
Categories=Graphics;Photography;Utility;
Keywords=screenshot;capture;screen;annotation;
StartupNotify=false
X-GNOME-Autostart-enabled=true
DESKTOP
    $SUDO chmod 644 "${INSTALL_DESKTOP}"
    success "Desktop entry installed: ${INSTALL_DESKTOP}"

    # Autostart entry (GNOME/KDE)
    local autostart_dir="${HOME}/.config/autostart"
    mkdir -p "$autostart_dir"
    cp "${SCRIPT_DIR}/greenshot.desktop" "${autostart_dir}/greenshot.desktop" 2>/dev/null || \
    tee "${autostart_dir}/greenshot.desktop" > /dev/null <<AUTOSTART
[Desktop Entry]
Name=Greenshot
Exec=${INSTALL_BIN}
Type=Application
X-GNOME-Autostart-enabled=true
AUTOSTART
    success "Autostart entry installed: ${autostart_dir}/greenshot.desktop"

    # Update icon cache
    if command -v gtk-update-icon-cache &>/dev/null; then
        $SUDO gtk-update-icon-cache -f -t /usr/share/icons/hicolor &>/dev/null || true
    fi

    # Update desktop database
    if command -v update-desktop-database &>/dev/null; then
        $SUDO update-desktop-database /usr/share/applications &>/dev/null || true
    fi
}

# ── Step 5: Optionally create config directory ─────────────────────────────
create_config_dir() {
    local cfg_dir="${HOME}/.config/greenshot"
    mkdir -p "$cfg_dir"
    info "Config directory: ${cfg_dir}/greenshot.json"
}

# ── Main ──────────────────────────────────────────────────────────────────
echo ""
echo -e "${CYAN}╔══════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║      Greenshot for Ubuntu — Installer    ║${NC}"
echo -e "${CYAN}╚══════════════════════════════════════════╝${NC}"
echo ""

# Parse flags
SKIP_DEPS=0
SKIP_DOTNET=0
SKIP_BUILD=0
for arg in "$@"; do
    case "$arg" in
        --skip-deps)   SKIP_DEPS=1   ;;
        --skip-dotnet) SKIP_DOTNET=1 ;;
        --skip-build)  SKIP_BUILD=1  ;;
        --help|-h)
            echo "Usage: $0 [--skip-deps] [--skip-dotnet] [--skip-build]"
            echo ""
            echo "  --skip-deps    Skip runtime dependency installation (apt packages)"
            echo "  --skip-dotnet  Skip .NET SDK check/installation"
            echo "  --skip-build   Skip build step (use existing .build_output/)"
            exit 0
            ;;
        *)
            die "Unknown option: $arg  (try --help)"
            ;;
    esac
done

[[ $SKIP_DOTNET -eq 0 ]] && ensure_dotnet
[[ $SKIP_DEPS   -eq 0 ]] && install_runtime_deps
[[ $SKIP_BUILD  -eq 0 ]] && build_app
install_app
create_config_dir

echo ""
echo -e "${GREEN}╔══════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║         Installation complete! ✓         ║${NC}"
echo -e "${GREEN}╚══════════════════════════════════════════╝${NC}"
echo ""
echo -e "  Run now:    ${CYAN}greenshot${NC}"
echo -e "  Settings:   ${CYAN}~/.config/greenshot/greenshot.json${NC}"
echo ""
echo -e "  Default hotkeys:"
echo -e "    ${YELLOW}Print${NC}          → Capture region"
echo -e "    ${YELLOW}Ctrl+Print${NC}     → Capture full screen"
echo -e "    ${YELLOW}Alt+Print${NC}      → Capture window"
echo -e "    ${YELLOW}Shift+Print${NC}    → Capture last region"
echo ""
echo -e "  ${YELLOW}Note:${NC} Global hotkeys require X11 (or XWayland on Wayland sessions)."
echo -e "        For pure Wayland, configure shortcuts in GNOME Settings → Keyboard."
echo ""
