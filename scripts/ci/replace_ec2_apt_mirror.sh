#!/usr/bin/env bash
# Replaces the Ubuntu mirror of AWS (<region>.ec2.archive.ubuntu.com) in the apt mirror list of a
# GitHub-hosted runner by the central archive. On AWS-hosted runners that mirror is often so slow
# that installing the .NET SDK takes more than half an hour instead of seconds (#5776). Runners
# on Azure use another mirror and stay unchanged, as does a runner without a mirror list.
#
# apt verifies the same archive signatures whichever mirror delivers the packages.
#
# Usage: replace_ec2_apt_mirror.sh [apt mirror list]
#
# NOTE: used by .github/workflows/test-install.yml, documented in
# documentation/developer-docs/github/test-install-workflow.md - please keep that doc in
# sync when changing it.
set -euo pipefail

mirror_list="${1:-/etc/apt/apt-mirrors.txt}"
ec2_mirror_pattern='[a-z0-9-]+\.ec2\.archive\.ubuntu\.com'
replacement_mirror='archive.ubuntu.com'

if [[ ! -f "$mirror_list" ]]; then
    echo "No apt mirror list at $mirror_list, nothing to replace."
    exit 0
fi

if grep -qE "//${ec2_mirror_pattern}/" "$mirror_list"; then
    sed -i -E "s#//${ec2_mirror_pattern}/#//${replacement_mirror}/#g" "$mirror_list"
    echo "Replaced the EC2 mirror in $mirror_list by ${replacement_mirror}:"
else
    echo "$mirror_list names no EC2 mirror and stays unchanged:"
fi
cat "$mirror_list"
