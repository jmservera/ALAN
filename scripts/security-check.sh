#!/bin/bash
# Security checking script for ALAN infrastructure
# Runs Checkov security scanning on Bicep templates

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

print_info() {
    echo -e "${BLUE}ℹ${NC} $1"
}

print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}⚠${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

print_header() {
    echo ""
    echo "================================"
    echo "$1"
    echo "================================"
    echo ""
}

# Check if Checkov is installed
if ! command -v checkov &> /dev/null; then
    print_error "Checkov is not installed."
    print_info "Install it with: pip install checkov"
    exit 1
fi

print_header "ALAN Infrastructure Security Check"

print_info "Checkov version: $(checkov --version)"

# Navigate to the repository root
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
cd "$REPO_ROOT"

# Check if infra directory exists
if [ ! -d "infra" ]; then
    print_error "infra directory not found!"
    exit 1
fi

print_info "Scanning Bicep templates in: infra/"
echo ""

# Run Checkov with configuration from .checkov.yml
# Use --skip-download to work offline
# Use --framework bicep to scan Bicep files
checkov \
    --directory infra/ \
    --framework bicep \
    --skip-download \
    --compact \
    --output cli

exit_code=$?

echo ""
if [ $exit_code -eq 0 ]; then
    print_success "Security scan completed - No issues found!"
else
    print_warning "Security scan completed with findings. Review the output above."
    print_info "To skip specific checks, add them to .checkov.yml"
fi

exit $exit_code
