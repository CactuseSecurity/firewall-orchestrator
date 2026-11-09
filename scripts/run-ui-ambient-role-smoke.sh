#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

ansible-playbook site.yml --tags integrationtests -K \
  -e installation_mode="${FWO_INSTALLATION_MODE:-upgrade}" \
  -e run_ui_ambient_role_smoke=true \
  "$@"
