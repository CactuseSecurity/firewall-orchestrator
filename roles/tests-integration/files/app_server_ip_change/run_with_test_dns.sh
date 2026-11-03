#!/usr/bin/env bash
# Runs the app server ip change test with a name server that answers the reverse lookups of the test ips.
# Started as root by test-app-server-ip-change.yml inside "unshare --mount --propagation private": the bind
# mount of resolv.conf below is only visible to this process tree, so the middleware and every other process
# keep the resolver configuration of the system. The test itself runs as FWO_TEST_RUN_USER.
set -euo pipefail

readonly kResponderStartSeconds=30

responder_pid=""
stop_responder() {
  if [[ -n "${responder_pid}" ]]; then
    kill "${responder_pid}" 2>/dev/null || true
    wait "${responder_pid}" 2>/dev/null || true
  fi
}
trap stop_responder EXIT

ready_file="${FWO_TEST_HARNESS_DIR}/ptr_responder.ready"
rm -f "${ready_file}"
"${FWO_TEST_PYTHON}" -I "${FWO_TEST_HARNESS_DIR}/ptr_responder.py" \
  --listen "${FWO_TEST_DNS_ADDRESS}" \
  --ptr "${FWO_TEST_OLD_IP}=${FWO_TEST_SERVER_NAME}" \
  --ptr "${FWO_TEST_NEW_IP}=${FWO_TEST_SERVER_NAME}" \
  --ready-file "${ready_file}" \
  --max-seconds "${FWO_TEST_DNS_MAX_SECONDS}" &
responder_pid=$!

for _ in $(seq 1 "${kResponderStartSeconds}"); do
  [[ -f "${ready_file}" ]] && break
  sleep 1
done
if [[ ! -f "${ready_file}" ]]; then
  echo "the test name server did not start within ${kResponderStartSeconds} seconds" >&2
  exit 1
fi

mount --bind "${FWO_TEST_HARNESS_DIR}/resolv.conf" /etc/resolv.conf

# runuser resets PATH on some distributions, so dotnet is called with its full path
dotnet_bin="$(command -v dotnet)"
runuser -u "${FWO_TEST_RUN_USER}" -- env HOME="${FWO_TEST_RUN_HOME}" \
  "${dotnet_bin}" test "${FWO_TEST_HARNESS_DIR}/FWO.Test.AppServerIpChange.csproj" --configuration Debug --nologo
