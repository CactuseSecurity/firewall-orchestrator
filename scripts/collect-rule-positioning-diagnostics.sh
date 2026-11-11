#!/usr/bin/env bash
set -euo pipefail
umask 077

usage() {
  echo "Usage: $0 <positive-implementation-task-id> [output-file]" >&2
  exit 2
}

[[ $# -ge 1 && $# -le 2 ]] || usage
implementation_task_id="$1"
[[ "$implementation_task_id" =~ ^[1-9][0-9]*$ ]] || usage

timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
output_path="${2:-rule-positioning-diagnostics-impltask-${implementation_task_id}-${timestamp}.txt}"
[[ ! -e "$output_path" ]] || {
  echo "Refusing to overwrite existing output: $output_path" >&2
  exit 1
}

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
middleware_glob="${FWO_MIDDLEWARE_LOG_GLOB:-/var/log/fworch/middleware.log*}"
api_glob="${FWO_API_LOG_GLOB:-/var/log/fworch/api.log*}"

python_command="python3"
if ! "$python_command" -c 'import sys; raise SystemExit(sys.version_info < (3, 8))' >/dev/null 2>&1; then
  python_command="python"
fi
"$python_command" - "$implementation_task_id" "$output_path" "$middleware_glob" "$api_glob" "$repo_root" <<'PY'
import datetime as dt
import glob
import gzip
import hashlib
import json
import os
import re
import shlex
import socket
import subprocess
import sys
from pathlib import Path

task_id = int(sys.argv[1])
output_path = Path(sys.argv[2])
middleware_glob = sys.argv[3]
api_glob = sys.argv[4]
repo_root = sys.argv[5]


def read_lines(pattern):
    for name in sorted(glob.glob(pattern)):
        opener = gzip.open if name.endswith(".gz") else open
        try:
            with opener(name, "rt", encoding="utf-8", errors="replace") as stream:
                for line in stream:
                    yield line.rstrip("\r\n")
        except OSError:
            continue


def parse_timestamp(value):
    if not isinstance(value, str):
        return None
    value = value.strip().replace("Z", "+00:00")
    try:
        parsed = dt.datetime.fromisoformat(value)
    except ValueError:
        return None
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=dt.timezone.utc)
    return parsed.astimezone(dt.timezone.utc)


def line_timestamp(line):
    return parse_timestamp(line.split(" ", 1)[0]) if line else None


middleware_lines = list(read_lines(middleware_glob))
diagnostics = []
attempt_windows = {}
for line in middleware_lines:
    marker = "RPDIAG "
    position = line.find(marker)
    if position < 0:
        continue
    try:
        payload = json.loads(line[position + len(marker):])
    except (json.JSONDecodeError, TypeError):
        continue
    if payload.get("implementation_task_id") != task_id:
        continue
    attempt_id = str(payload.get("attempt_id", ""))
    sequence = payload.get("sequence")
    if not attempt_id or not isinstance(sequence, int):
        continue
    timestamp = line_timestamp(line)
    diagnostics.append((attempt_id, sequence, line, timestamp))
    if timestamp is not None:
        start, end = attempt_windows.get(attempt_id, (timestamp, timestamp))
        attempt_windows[attempt_id] = (min(start, timestamp), max(end, timestamp))

diagnostics.sort(key=lambda entry: (entry[0], entry[1]))
windows = list(attempt_windows.values())


def in_attempt_window(timestamp):
    return timestamp is not None and any(start <= timestamp <= end for start, end in windows)


safe_statuses = {"NotApplicable", "Stored", "ClearedAfterFailure", "PersistenceFailed"}
safe_failure_categories = {
    "None", "InvalidInput", "UnsupportedTask", "ConfigurationInvalid",
    "PositioningMethodNotImplemented", "StrategyRegistrationAmbiguous", "CalculationFailed",
}
outcome_pattern = re.compile(
    rf"^[^ ]+ Info - Rule positioning(?: \(RulePositioningWorkflowCaller\.cs in line [0-9]+\))?, "
    rf"Implementation task {task_id}: ([A-Za-z]+); failure category: ([A-Za-z]+)\.$"
)
outcome_entries = []
for line in middleware_lines:
    match = outcome_pattern.match(line)
    if not match or match.group(1) not in safe_statuses or match.group(2) not in safe_failure_categories:
        continue
    outcome_entries.append((line_timestamp(line), {
        "timestamp": line.split(" ", 1)[0],
        "implementation_task_id": task_id,
        "status": match.group(1),
        "failure_category": match.group(2),
    }))
outcome_entries.sort(key=lambda item: item[0] or dt.datetime.min.replace(tzinfo=dt.timezone.utc))

connection_pattern = re.compile(
    r"^[^ ]+ Error - API Connections(?: \((GraphQlApiConnection(?:Subscriptions)?\.cs) in line ([0-9]+)\))?,"
)
connection_errors = []
for line in middleware_lines:
    timestamp = line_timestamp(line)
    match = connection_pattern.match(line)
    if not match or not in_attempt_window(timestamp):
        continue
    entry = {
        "timestamp": line.split(" ", 1)[0],
        "severity": "Error",
        "category": "API Connections",
    }
    if match.group(1):
        entry["source_file"] = match.group(1)
        entry["source_line"] = int(match.group(2))
    connection_errors.append((timestamp, entry))
connection_errors.sort(key=lambda item: item[0])


def find_key(value, wanted):
    if isinstance(value, dict):
        for key, child in value.items():
            if str(key).lower() in wanted:
                return child
        for child in value.values():
            found = find_key(child, wanted)
            if found is not None:
                return found
    elif isinstance(value, list):
        for child in value:
            found = find_key(child, wanted)
            if found is not None:
                return found
    return None


api_entries = []
for line in read_lines(api_glob):
    try:
        payload = json.loads(line)
    except (json.JSONDecodeError, TypeError):
        continue
    if find_key(payload, {"type"}) != "http-log":
        continue
    role = find_key(payload, {"x-hasura-role", "x_hasura_role"})
    if role != "middleware-server":
        continue
    raw_timestamp = find_key(payload, {"timestamp", "time"})
    parsed_timestamp = parse_timestamp(raw_timestamp)
    if not in_attempt_window(parsed_timestamp):
        continue
    status = find_key(payload, {"status", "status_code", "statuscode"})
    method = find_key(payload, {"method"})
    path = find_key(payload, {"path", "url"})
    level = find_key(payload, {"level"})
    entry = {
        "timestamp": raw_timestamp,
        "role": "middleware-server",
    }
    if isinstance(level, str):
        entry["level"] = level
    if isinstance(method, str):
        entry["method"] = method
    if isinstance(path, str):
        entry["path"] = path
    if isinstance(status, (int, float, str)) and not isinstance(status, bool):
        try:
            entry["status_code"] = int(status)
        except (TypeError, ValueError):
            pass
    api_entries.append((parsed_timestamp, entry))
api_entries.sort(key=lambda item: item[0])


def command_output(command):
    try:
        return subprocess.run(
            command,
            check=False,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            text=True,
            timeout=5,
        ).stdout.strip()
    except (OSError, subprocess.SubprocessError):
        return ""


systemctl_output = command_output([
    "systemctl", "show", "fworch-middleware",
    "--property=ActiveState",
    "--property=ActiveEnterTimestamp",
    "--property=ExecStart",
    "--property=MainPID",
])
service_properties = {}
for line in systemctl_output.splitlines():
    key, separator, value = line.partition("=")
    if separator and key in {"ActiveState", "ActiveEnterTimestamp", "ExecStart", "MainPID"}:
        service_properties[key] = value

exec_start = service_properties.get("ExecStart", "")
executable = ""
path_match = re.search(r"\bpath=([^ ;}]+)", exec_start)
if path_match:
    executable = path_match.group(1)
elif exec_start:
    try:
        executable = shlex.split(exec_start)[0]
    except (ValueError, IndexError):
        executable = ""
if executable:
    service_properties["ExecStart"] = executable
else:
    service_properties.pop("ExecStart", None)

executable_sha256 = ""
if executable and os.path.isabs(executable) and os.path.isfile(executable):
    digest = hashlib.sha256()
    try:
        with open(executable, "rb") as stream:
            for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(chunk)
        executable_sha256 = digest.hexdigest()
    except OSError:
        executable_sha256 = ""

git_head = command_output(["git", "-C", repo_root, "rev-parse", "HEAD"])
generated_at = dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z")

sections = [
    "# Rule positioning diagnostic bundle",
    "collector_schema_version=1",
    f"generated_at_utc={generated_at}",
    f"hostname={socket.gethostname()}",
    f"repository_head={git_head or 'unavailable'}",
    f"implementation_task_id={task_id}",
    "",
    "## Middleware service",
]
for key in ("ActiveState", "ActiveEnterTimestamp", "ExecStart", "MainPID"):
    if key in service_properties:
        sections.append(f"{key}={service_properties[key]}")
if executable_sha256:
    sections.append(f"ExecStartSHA256={executable_sha256}")

sections.extend(["", "## RPDIAG events"])
sections.extend(entry[2] for entry in diagnostics)
sections.extend(["", "## Implementation task outcomes"])
sections.extend(json.dumps(entry, separators=(",", ":"), sort_keys=True) for _, entry in outcome_entries)
sections.extend(["", "## Middleware API connection errors in attempt windows"])
sections.extend(json.dumps(entry, separators=(",", ":"), sort_keys=True) for _, entry in connection_errors)
sections.extend(["", "## Hasura middleware-server HTTP logs in attempt windows"])
sections.extend(json.dumps(entry, separators=(",", ":"), sort_keys=True) for _, entry in api_entries)
sections.append("")

output_path.parent.mkdir(parents=True, exist_ok=True)
flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
descriptor = os.open(output_path, flags, 0o600)
try:
    with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as stream:
        stream.write("\n".join(sections))
    os.chmod(output_path, 0o600)
except Exception:
    try:
        output_path.unlink()
    except OSError:
        pass
    raise
PY

printf '%s\n' "$output_path"
