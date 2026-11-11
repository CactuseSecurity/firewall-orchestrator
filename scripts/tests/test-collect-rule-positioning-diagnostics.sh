#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
collector="$repo_root/scripts/collect-rule-positioning-diagnostics.sh"
fixture_root="$(mktemp -d)"
trap 'rm -rf "$fixture_root"' EXIT

log_dir="$fixture_root/logs"
bin_dir="$fixture_root/bin"
output_dir="$fixture_root/output"
mkdir -p "$log_dir" "$bin_dir" "$output_dir"

cat > "$bin_dir/systemctl" <<'EOF'
#!/usr/bin/env bash
printf '%s\n' \
  'ActiveState=active' \
  'ActiveEnterTimestamp=Tue 2026-11-11 10:00:00 UTC' \
  'ExecStart={ path=/usr/bin/false ; argv[]=/usr/bin/false ; }' \
  'MainPID=4321'
EOF
chmod +x "$bin_dir/systemctl"

cat > "$log_dir/middleware.log.1" <<'EOF'
2026-11-11T10:00:00+00:00 Info - Rule positioning diagnostic, RPDIAG {"schema_version":1,"attempt_id":"attempt-b","sequence":1,"implementation_task_id":42,"stage":"workflow","outcome":"start","facts":{}}
2026-11-11T10:00:01+00:00 Error - API Connections (GraphQlApiConnection.cs in line 123), query SECRET_MIDDLEWARE_QUERY_B; message SECRET_HASURA_MESSAGE_B
2026-11-11T10:00:02+00:00 Info - Rule positioning diagnostic, RPDIAG {"schema_version":1,"attempt_id":"attempt-b","sequence":2,"implementation_task_id":42,"stage":"workflow","outcome":"complete","facts":{}}
2026-11-11T10:00:03+00:00 Error - API Connections, SECRET_OUTSIDE_WINDOW
EOF
gzip "$log_dir/middleware.log.1"

cat > "$log_dir/middleware.log" <<'EOF'
2026-11-11T09:59:59+00:00 Info - Rule positioning diagnostic, RPDIAG {"schema_version":1,"attempt_id":"other-task","sequence":1,"implementation_task_id":77,"stage":"workflow","outcome":"start","facts":{"secret":"SECRET_OTHER_TASK"}}
2026-11-11T11:00:00+00:00 Info - Rule positioning diagnostic, RPDIAG {"schema_version":1,"attempt_id":"attempt-a","sequence":2,"implementation_task_id":42,"stage":"workflow","outcome":"complete","facts":{}}
2026-11-11T10:59:58+00:00 Info - Rule positioning diagnostic, RPDIAG {"schema_version":1,"attempt_id":"attempt-a","sequence":1,"implementation_task_id":42,"stage":"workflow","outcome":"start","facts":{}}
2026-11-11T10:59:59+00:00 Error - API Connections (GraphQlApiConnection.cs in line 456), query SECRET_MIDDLEWARE_QUERY_A; message SECRET_HASURA_MESSAGE_A
2026-11-11T11:00:01+00:00 Info - Rule positioning, Implementation task 42: Stored; failure category: None.
2026-11-11T11:00:02+00:00 Info - Rule positioning, Implementation task 77: SECRET_OTHER_OUTCOME
2026-11-11T10:59:59+00:00 Error - Customer text, misleading Implementation task 42: Stored; failure category: None. SECRET_MISLEADING_OUTCOME
EOF

cat > "$log_dir/api.log" <<'EOF'
{"timestamp":"2026-11-11T10:00:01+00:00","type":"http-log","level":"info","request":{"method":"POST","path":"/v1/graphql","headers":{"x-hasura-role":"middleware-server","x-hasura-user-id":"SECRET_USER"},"query":"SECRET_QUERY"},"response":{"status":200}}
{"timestamp":"2026-11-11T10:00:03+00:00","type":"http-log","request":{"headers":{"x-hasura-role":"middleware-server"}},"secret":"SECRET_API_OUTSIDE_WINDOW"}
{"timestamp":"2026-11-11T10:59:59+00:00","type":"http-log","level":"error","request":{"method":"POST","path":"/v1/graphql","headers":{"x-hasura-role":"middleware-server"},"query":"SECRET_QUERY_2"},"response":{"status":500}}
{"timestamp":"2026-11-11T10:59:59+00:00","type":"http-log","request":{"headers":{"x-hasura-role":"admin"}},"secret":"SECRET_WRONG_ROLE"}
EOF

output_path="$output_dir/bundle.txt"
middleware_glob="$log_dir/middleware.log*"
api_glob="$log_dir/api.log*"
if [[ "$(uname -s)" == MINGW* ]]; then
  native_log_dir="$(cygpath -m "$log_dir")"
  middleware_glob="$native_log_dir/middleware.log*"
  api_glob="$native_log_dir/api.log*"
fi
actual_path="$(PATH="$bin_dir:$PATH" \
  FWO_MIDDLEWARE_LOG_GLOB="$middleware_glob" \
  FWO_API_LOG_GLOB="$api_glob" \
  bash "$collector" 42 "$output_path")"

[[ "$actual_path" == "$output_path" ]]
[[ "$(find "$output_dir" -maxdepth 1 -type f | wc -l | tr -d ' ')" == "1" ]]
if [[ "$(uname -s)" != MINGW* ]]; then
  [[ "$(stat -c '%a' "$output_path")" == "600" ]]
fi
grep -q 'attempt-a.*"sequence":1' "$output_path"
grep -q 'attempt-a.*"sequence":2' "$output_path"
grep -q 'attempt-b.*"sequence":1' "$output_path"
grep -q 'attempt-b.*"sequence":2' "$output_path"
grep -q '"category":"API Connections"' "$output_path"
grep -q '"source_line":123' "$output_path"
grep -q '"source_line":456' "$output_path"
grep -q '"status":"Stored"' "$output_path"
grep -q '"role":"middleware-server"' "$output_path"
grep -q '"status_code":500' "$output_path"
! grep -q 'SECRET_' "$output_path"
! grep -q 'Implementation task 77:' "$output_path"

if bash "$collector" 42 "$output_path" >/dev/null 2>&1; then
  echo 'collector overwrote an existing file' >&2
  exit 1
fi
if bash "$collector" 0 "$output_dir/invalid.txt" >/dev/null 2>&1; then
  echo 'collector accepted an invalid implementation-task ID' >&2
  exit 1
fi

echo PASS
