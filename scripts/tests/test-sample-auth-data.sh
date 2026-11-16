#!/usr/bin/env bash
# Exercise the production LDAP tasks against a stateful ldapmodify stub.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
test_directory="$(mktemp -d)"
trap 'rm -rf "$test_directory"' EXIT
mkdir -p "$test_directory/bin" "$test_directory/state" "$test_directory/ldif"

cat >"$test_directory/bin/ldapmodify" <<'SH'
#!/usr/bin/env bash
set -euo pipefail
input="$(cat)"
if [[ "$input" == *$'\ndn: '* ]]; then
    echo 'Test stub received multiple records' >&2
    exit 80
fi
if [[ -n "${LDAP_STUB_FORCED_RESULT:-}" ]]; then
    echo "ldap_modify: forced test result ($LDAP_STUB_FORCED_RESULT)" >&2
    exit "$LDAP_STUB_FORCED_RESULT"
fi
record_key="$(printf '%s' "$input" | cksum | cut -d ' ' -f 1)"
record_file="$LDAP_STUB_STATE/$record_key"
if [[ -f "$record_file" ]]; then
    if [[ "$input" == *'changetype: modify'* ]]; then
        exit 20
    fi
    exit 68
fi
printf '%s\n' "$input" >"$record_file"
SH
chmod +x "$test_directory/bin/ldapmodify"

export PATH="$test_directory/bin:$PATH"
export LDAP_STUB_STATE="$test_directory/state"
export ANSIBLE_LOCAL_TEMP="$test_directory/local"
export ANSIBLE_REMOTE_TEMP="$test_directory/remote"
export ANSIBLE_CACHE_PLUGIN=memory
export ANSIBLE_ROLES_PATH="$repo_root/roles"
export ANSIBLE_NOCOLOR=1
ansible-playbook -i localhost, -c local \
    "$repo_root/roles/tests-integration/files/auth/test-sample-auth-data.yml" \
    -e "test_directory=$test_directory" -e ansible_become=false
