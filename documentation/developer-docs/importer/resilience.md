# Importer resilience against service restarts

The importer talks to the FWO API (Hasura) and the middleware through apache. Restarting any of
them - the installer does so on every run, an operator may do so at any time - interrupts those
connections for a few seconds. This page describes how the importer rides that out.

## Retries of requests that never reached the service

`fwo_http_retry.send_with_retry` wraps every HTTP request `FwoApi` sends (GraphQL calls, chunked
calls, middleware endpoints, login and token refresh). It repeats a request after the waits in
`fwo_const.FWO_API_RETRY_DELAYS_SECONDS` (about 90 s of waits per request in total, plus up to
`fwo_const.FWO_API_CONNECT_TIMEOUT` per attempt) when

- the connection was refused or could not be established in time
  (`fwo_const.FWO_API_CONNECT_TIMEOUT`, 10 s - the api and the middleware are local or on the LAN,
  and the bound keeps the time spent on connecting small next to the waits), or
- the answer is a 503.

A refused connection never reached anything, and a 503 from apache's proxy means its backend was not
reached, so for the FWO API both are safe to repeat for mutations too. The middleware itself can
also answer 503 for some endpoints after processing the request, so only idempotent middleware
endpoints may be called through this retry. Today the importer only calls reads and the token
endpoints there, where a repeat is harmless. Read timeouts, connections dropped after the request
was sent and 502 proxy timeouts are never retried, because the backend may already have applied the
request. TLS errors are never retried either - they are configuration errors.

Retries never delay a stop: once a shutdown is requested, the outcome of the attempt just made is
returned at once instead of waiting for the next one. Only during a deferred rollback (see below)
do retries go on, so that the rollback can finish.

For GraphQL calls (`FwoApi.call`) and middleware endpoint calls (`FwoApi.call_endpoint`), a request
that still fails after the last retry, and a connection dropped mid-request, raise
`FwoApiServiceUnavailableError`. Login and token refresh keep raising `FwoApiLoginFailedError`; the
import loop retries the login on its next cycle.

## Ending the cycle while the api is gone

When an import fails with `FwoApiServiceUnavailableError`, it is rolled back like any failed import,
and `import_main_loop.main_loop` then skips the remaining managements of the cycle. Every one of them
would fail the same way. The next cycle starts after the usual `importSleepTime`.

The rollback of the interrupted import may itself fail while the api is down, leaving the import's
lock behind. `main_loop` therefore requests the orphan cleanup (below) again, which removes that lock
after the next successful login, before any import.

## Locks left by an import that has ended

The final unlock of an import (setting its `stop_time`) and the rollback of a failed import only log
their own failures. If the api is gone for longer than the retries right then, and back before the
next management is imported, nothing ends the cycle - and the lock would block the management until
the importer process restarts. After every import that took a lock, `import_single_management`
therefore asks whether that import still holds it (`orphaned_imports.import_left_open`,
`getOpenImport.graphql`). If it does, or if the question cannot be answered, `main_loop` requests the
orphan cleanup for the next cycle. An import whose data was written completely but whose unlock
failed is rolled back that way too, and its management is imported again in that cycle.

## Imports left unfinished

An import keeps its management locked (an `import_control` row without `stop_time`) until it
completes or is rolled back. A process that is SIGKILLed - the OOM killer, `TimeoutStopSec`
expiring, a power loss - or that cannot reach the api for its own rollback leaves that lock behind,
and the management could not be imported again until someone rolled it back in the UI.

On its first successful login, `import_main_loop` therefore rolls back every config import
(`import_type_id = 1`) still open for a management whose `importer_hostname` is this host
(`orphaned_imports.py`). Imports of other importer hosts are never touched. If the cleanup fails, it
is repeated on the next cycle; importing goes on regardless. A manual `import_mgm.py` run on this
host that is still running when the importer service starts is rolled back - do not restart the
importer service during a manual import. The same holds for an import forced with `import_mgm.py -f`
on another host for a management owned by this host: it is rolled back when this host's importer
(re)starts. The cleanup also runs again in the cycle after an api outage ended a cycle, or after an
import still held its lock once it ended (`main_loop` re-arms it, see above), so a manual `import_mgm.py` run - or an import forced with `-f`
from another host - of this host's managements that is still running at that moment is rolled back
too, not only at a service (re)start.

Every import rolled back this way is also recorded as a data issue (severity 1) of its management,
and in the cycle of the cleanup those managements are imported after all others. A management whose
import keeps getting the importer process killed - for instance by `MemoryMax` with
`OOMPolicy=kill` - would otherwise be imported first after every restart, kill the process again and
starve every management after it. The data issue makes such a management visible in the UI.

## Shutdown during a rollback

SIGINT and SIGTERM raise `ShutdownRequestedError` wherever the importer is, so that a long api
call does not delay a stop. `FwoApi` lets that exception through unchanged, and retries of api
requests stop once a shutdown is requested, so a stop is not delayed by retries either. During a
rollback, raising would cut the rollback short and leave the import's lock behind.
`roll_back_exception_handler` therefore runs inside `fwo_signalling.shutdown_deferred()`: a signal
arriving then only sets `fwo_globals.shutdown_requested` (and the block sets
`fwo_globals.shutdown_deferral_active`), retries go on so the rollback can finish, and the importer
stops at its next shutdown check once the rollback is done.

A requested shutdown ends the importer with exit code 0 (`fwo_const.IMPORTER_SHUTDOWN_EXIT_CODE`), so systemd
logs a regular stop and not a failure; a drain exits with 75 (see below).

## Finishing the running import before a stop (drain)

`SIGUSR1` asks the importer to finish the import it is running and to start no new one. As soon as
no import holds a lock, it exits with code 75 (`fwo_const.IMPORTER_DRAINED_EXIT_CODE`), which the
unit declares as a clean exit that systemd does not restart. The installer uses this before an
upgrade, see `documentation/developer-docs/installer/systemd-units.md`. To drain by hand:

    sudo kill -USR1 "$(systemctl show -p MainPID --value fworch-importer-api)"

The service then stays stopped - systemd does not restart an importer that exited after draining -
until it is started again with `sudo systemctl start fworch-importer-api`.
