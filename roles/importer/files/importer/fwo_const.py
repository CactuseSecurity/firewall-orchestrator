from __future__ import annotations

import os
from typing import Any

BASE_DIR = "/usr/local/fworch"
IMPORTER_BASE_DIR = BASE_DIR + "/importer"

FW_MODULE_NAME = "fwcommon"  # the module start-point for product specific code
FULL_CONFIG_SIZE_LIMIT = 5000000  # native configs greater than 5 MB will not be stored in DB
LIST_DELIMITER = "|"
USER_DELIMITER = "@"
ANY_IP_IPV4 = "0.0.0.0/0"
ANY_IP_START = "0.0.0.0/32"
ANY_IP_END = "255.255.255.255/32"
ANY_IP_PROTOCOL_ID = -1
DUMMY_IP = "0.0.0.0/32"
DEFAULT_COLOR = "black"
NAT_POSTFIX = "_NatNwObj"
FWO_API_HTTP_IMPORT_TIMEOUT = 14400  # 4 hours
FWO_HTTP_CONNECT_TIMEOUT = 60  # max seconds to establish a connection before giving up on a stalled endpoint
# (connect, read) timeout tuple guarding outbound HTTP calls so a stalled endpoint cannot hang an importer worker
FWO_HTTP_TIMEOUT = (FWO_HTTP_CONNECT_TIMEOUT, FWO_API_HTTP_IMPORT_TIMEOUT)
# max seconds to connect to the FWO API or middleware: they are local or on the LAN, so a connect that
# takes longer means the host is down - and bounding it keeps the retry budget near its ~90 s
FWO_API_CONNECT_TIMEOUT = 10
# (connect, read) timeout of requests to the FWO API and middleware
FWO_API_HTTP_TIMEOUT = (FWO_API_CONNECT_TIMEOUT, FWO_API_HTTP_IMPORT_TIMEOUT)
# waits between retries of a request to the FWO API or middleware which never reached the service
# (refused connection, 503 from apache) - about 90 s in total: long enough to ride out a restart of
# apache, the api or the middleware (10 s ExecStartPre plus startup), short enough not to stall an
# import for long. See fwo_http_retry.
FWO_API_RETRY_DELAYS_SECONDS: tuple[int, ...] = (2, 4, 8, 16, 30, 30)
# limits for reading a config file from a URL or the local file system (direct import), so a large,
# compressed or slow response cannot exhaust the importer's memory or block it for hours
CONFIG_FILE_MAX_BYTES = 256 * 1024 * 1024  # decompressed; the database cannot store a much larger config anyway
CONFIG_FILE_READ_TIMEOUT = 300  # max seconds to wait for the next chunk of a config file download
CONFIG_FILE_TOTAL_TIMEOUT = 1800  # max seconds for a complete config file download
CONFIG_FILE_CHUNK_SIZE = 1024 * 1024
IMPORTER_USER_NAME = "importer"  # TODO: move to config file?
# stm_import.import_type_id of a firewall config import - the type the import loop locks managements with
CONFIG_IMPORT_TYPE_ID = 1

# exit code of an importer that finished its running import after SIGUSR1 (EX_TEMPFAIL from
# sysexits.h). Must equal fworch_importer_drained_exit_code in inventory/group_vars/all.yml, which the
# unit declares as a clean exit that is not restarted.
IMPORTER_DRAINED_EXIT_CODE = 75
# exit code of an importer that stopped because a shutdown was requested (SIGINT/SIGTERM) - a regular stop, not a failure
IMPORTER_SHUTDOWN_EXIT_CODE = 0
# severity of the data issue logged for every import the orphan cleanup rolled back (see orphaned_imports)
ORPHANED_IMPORT_DATA_ISSUE_SEVERITY = 1
FWO_CONFIG_FILENAME = BASE_DIR + "/etc/fworch.json"
MAIN_KEY_FILE = BASE_DIR + "/etc/secrets/main_key"
IMPORTER_PWD_FILE = BASE_DIR + "/etc/secrets/importer_pwd"
IMPORT_TMP_PATH = BASE_DIR + "/tmp/import"  # noqa: S108
DEFAULT_SECTION_HEADER_TEXT = "section without name"
_graphql_query_path = os.getenv("FWO_GRAPHQL_QUERY_PATH", BASE_DIR + "/fwo-api-calls/")
if not _graphql_query_path.endswith("/"):
    _graphql_query_path += "/"
GRAPHQL_QUERY_PATH = _graphql_query_path

# possible ConfigFormat values: normalized|checkpoint|fortimanager|fortioOS|azure|ciscoFirePower
# info legacy : barracuda|junos|netscreen

# how many objects (network, services, rules, ...) should be sent to the FWO API in one go?
# should be between 500 and 2.000 in production (results in a max obj number of max. 5 x this value - nwobj/svc/rules/...)
# the database has a limit of 255 MB per jsonb
# https://stackoverflow.com/questions/12632871/size-limit-of-json-data-type-in-postgresql
# >25.000 rules exceed this limit
API_CALL_CHUNK_SIZE = 1000
RULE_NUM_NUMERIC_STEPS = 1024.0

EMPTY_NORMALIZED_FW_CONFIG_JSON_DICT: dict[str, list[Any]] = {  # TYPING: DO NOT USE THIS!!!!
    "network_objects": [],
    "service_objects": [],
    "user_objects": [],
    "zone_objects": [],
    "rules": [],
    "gateways": [],
}
