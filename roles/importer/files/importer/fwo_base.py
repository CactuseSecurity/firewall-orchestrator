import hashlib
import json
import re
from enum import Enum
from typing import TYPE_CHECKING, Any
import ipaddress
import traceback
import time

import fwo_config
import fwo_const
from fwo_enums import ConfFormat, ConfigAction
from model_controllers.fwconfig_import_ruleorder import RuleOrderService
if TYPE_CHECKING:
    from model_controllers.import_state_controller import ImportStateController
from fwo_log import FWOLogger
from services.service_provider import ServiceProvider
from services.global_state import GlobalState
from services.enums import Services, Lifetime
from services.uid2id_mapper import Uid2IdMapper
from services.group_flats_mapper import GroupFlatsMapper


def sanitize(content: Any, lower: bool = False) -> None | str:
    if content is None:
        return None
    result = str(content)
    result = result.replace("\"","")  # remove possibly contained apostrophe
    result = result.replace("\n"," ")  # replace possibly contained CR with space
    if lower:
        return result.lower()
    else:
        return result


def extend_string_list(list_string: str | None, src_dict: dict[str, list[str]], key: str, delimiter: str) -> str:
    if list_string is None:
        list_string = ''
    if list_string == '':
        if key in src_dict:
            result = delimiter.join(src_dict[key])
        else:
            result = ''
#            fwo_api.create_data_issue(fwo_api_base_url, jwt, import_id, key)
    else:
        if key in src_dict:
            old_list = list_string.split(delimiter)
            combined_list = old_list + src_dict[key]
            result = delimiter.join(combined_list)
        else:
            result = list_string
#            fwo_api.create_data_issue(fwo_api_base_url, jwt, import_id, key)
    return result

def string_is_uri(s: str) -> re.Match[str] | None: # TODO: should return bool?
    return re.match('http://.+', s) or re.match('https://.+', s) or  re.match('file://.+', s) 


def deserialize_class_to_dict_rec(obj: Any, seen: set[int] | None = None) -> dict[str, Any] | list[Any] | Any | str | int | float | bool | None: #TYPING: using model is forbidden?
    if seen is None:
        seen = set()

    # Handle simple immutable types directly (int, float, bool, str) and None
    if obj is None or isinstance(obj, (int, float, bool, str, ConfFormat, ConfigAction)):
        return obj

    # Check for circular references
    if id(obj) in seen:
        return f"<Circular reference to {obj.__class__.__name__}>"

    seen.add(id(obj))

    if isinstance(obj, list):
        # If the object is a list, deserialize each item
        return [deserialize_class_to_dict_rec(item, seen) for item in obj] # type: ignore
    elif isinstance(obj, dict):
        # If the object is a dictionary, deserialize each key-value pair
        return {key: deserialize_class_to_dict_rec(value, seen) for key, value in obj.items()} # type: ignore
    elif isinstance(obj, Enum):
        # If the object is an Enum, convert it to its value
        return obj.value
    elif hasattr(obj, '__dict__'):
        # If the object is a class instance, deserialize its attributes
        return {
            key: deserialize_class_to_dict_rec(value, seen)
            for key, value in obj.__dict__.items()
            if not callable(value) and not key.startswith('__')
        }
    else:
        # For other types, return the value as is
        return obj


def cidr_to_range(ip: str | None) -> list[str] | list[None]: # TODO: I have no idea what other than string it could be
    if isinstance(ip, str): # type: ignore
        # dealing with ranges:
        if '-' in ip:
            return '-'.split(ip)

        ip_version = valid_ip_address(ip)
        if ip_version=='Invalid':
            FWOLogger.warning("error while decoding ip '" + ip + "'")
            return [ip]
        elif ip_version=='IPv4':
            net = ipaddress.IPv4Network(ip)
        elif ip_version=='IPv6':
            net = ipaddress.IPv6Network(ip)
        return [str(net.network_address), str(net.broadcast_address)] # type: ignore

    return [ip]


def valid_ip_address(ip: str) -> str:
    try:
        # Try as network first (handles CIDR notation)
        network = ipaddress.ip_network(ip, strict=False)
        if network.version == 4:
            return "IPv4"
        else:
            return "IPv6"
    except ValueError:
        try:
            # Try as individual address
            addr = ipaddress.ip_address(ip)
            if addr.version == 4:
                return "IPv4"
            else:
                return "IPv6"
        except ValueError:
            return "Invalid"


def lcs_dp(seq1: list[Any], seq2: list[Any]) -> tuple[list[list[int]], int]:
    """
    Compute the length and dynamic programming (DP) table for the longest common subsequence (LCS)
    between seq1 and seq2. Returns (dp, length) where dp is a 2D table and
    length = dp[len(seq1)][len(seq2)].
    """
    m: int = len(seq1)
    n: int = len(seq2)
    dp: list[list[int]] = [[0]*(n+1) for _ in range(m+1)]

    for i in range(m):
        for j in range(n):
            if seq1[i] == seq2[j]:
                dp[i+1][j+1] = dp[i][j] + 1
            else:
                dp[i+1][j+1] = max(dp[i+1][j], dp[i][j+1])
    return dp, dp[m][n]


def backtrack_lcs(seq1: list[Any], seq2: list[Any], dp: list[list[int]]) -> list[tuple[int, int]]:
    """
    Backtracks the dynamic programming (DP) table to recover one longest common subsequence (LCS) (as a list of (i, j) index pairs).
    These index pairs indicate positions in seq1 and seq2 that match in the LCS.
    """
    lcs_indices: list[tuple[int, int]] = []
    i: int = len(seq1)
    j: int = len(seq2)
    while i > 0 and j > 0:
        if seq1[i-1] == seq2[j-1]:
            lcs_indices.append((i-1, j-1))
            i -= 1
            j -= 1
        elif dp[i-1][j] >= dp[i][j-1]:
            i -= 1
        else:
            j -= 1
    lcs_indices.reverse()
    return lcs_indices


def compute_min_moves(source: list[Any], target: list[Any]) -> dict[str, Any]:
    """
    Computes the minimal number of operations required to transform the source list into the target list,
    where allowed operations are:
       - pop-and-reinsert a common element (to reposition it)
       - delete (an element in source not present in target)
       - insert (an element in target not present in source)

    Returns a dictionary with all gathered data (total_moves, operations, deletions, insertions and moves) where operations is a list of suggested human readable operations.
    """
    # Build sets (assume uniqueness for membership checks)
    target_set: set[Any] = set(target)
    source_set: set[Any] = set(source)

    # Identify the common elements:
    s_common: list[Any] = [elem for elem in source if elem in target_set]
    t_common: list[Any] = [elem for elem in target if elem in source_set]

    # Calculate deletions and insertions:
    deletions: list[tuple[int, Any]] = [ (i, elem) for i, elem in enumerate(source) if elem not in target_set ]
    insertions: list[tuple[int, Any]] = [ (j, elem) for j, elem in enumerate(target) if elem not in source_set ]

    # Compute the longest common subsequence (LCS) between S_common and T_common – these are common elements already in correct relative order.
    lcs_data: tuple[list[list[int]], int] = lcs_dp(s_common, t_common)
    lcs_indices: list[tuple[int, int]] = backtrack_lcs(s_common, t_common, lcs_data[0])

    # To decide which common elements must be repositioned, mark the indices in S_common which are part of the LCS.
    in_place: list[bool] = [False] * len(s_common)
    for i, _ in lcs_indices:
        in_place[i] = True
    # Every common element in S_common not in the LCS will need a pop-and-reinsert.
    reposition_moves: list[tuple[int, Any, int]] = []
    # To better explain (rough indexing): We traverse the source list and when we get to a common element,
    # we check if it is “in place”. Note that because S_common is a filtered version of source, we need
    # to convert back to indices in the original source. We do this by iterating over source and whenever
    # we encounter an element in target_set, we pop the next value from S_common.
    s_common_iter: int = 0
    for orig_index, elem in enumerate(source):
        if elem in target_set:
            # This element is one of the common ones.
            if not in_place[s_common_iter]:
                # This element is not in the LCS so it will be repositioned.
                # We will reinsert it to the position where it should appear in target.
                reposition_moves.append((orig_index, elem, target.index(elem)))
            s_common_iter += 1

    total_moves: int = (len(deletions)
                   + len(insertions)
                   + len(reposition_moves))

    # Build a list of human‐readable operations.
    operations: list[str] = []
    for idx, elem in deletions:
        operations.append(f"Delete element '{elem}' at source index {idx}.")
    for idx, elem in insertions:
        operations.append(f"Insert element '{elem}' at target position {idx}.")
    for idx, elem, target_pos in reposition_moves:
        operations.append(f"Pop element '{elem}' from source index {idx} and reinsert at target position {target_pos}.")

    return {
        "moves": total_moves,
        "operations": operations,
        "deletions": deletions,
        "insertions": insertions,
        "reposition_moves": reposition_moves
    }


def write_native_config_to_file(import_state: 'ImportStateController', config_native: dict[str, Any] | None) -> None:
    from fwo_const import IMPORT_TMP_PATH
    if FWOLogger.is_debug_level(7):
        debug_start_time = int(time.time())
        try:
            full_native_config_filename = f"{IMPORT_TMP_PATH}/mgm_id_{str(import_state.mgm_details.mgm_id)}_config_native.json"
            with open(full_native_config_filename, "w") as json_data:
                json_data.write(json.dumps(config_native, indent=2))
        except Exception:
            FWOLogger.error(f"import_management - unspecified error while dumping config to json file: {str(traceback.format_exc())}")
            raise

        time_write_debug_json = int(time.time()) - debug_start_time
        FWOLogger.debug(f"import_management - writing debug config json files duration {str(time_write_debug_json)}s")


def init_service_provider() -> ServiceProvider:
    service_provider = ServiceProvider()
    service_provider.register(Services.FWO_CONFIG, lambda: fwo_config.read_config(), Lifetime.SINGLETON)
    service_provider.register(Services.GROUP_FLATS_MAPPER, lambda: GroupFlatsMapper(), Lifetime.IMPORT)
    service_provider.register(Services.PREV_GROUP_FLATS_MAPPER, lambda: GroupFlatsMapper(), Lifetime.IMPORT)
    service_provider.register(Services.UID2ID_MAPPER, lambda: Uid2IdMapper(), Lifetime.IMPORT)
    service_provider.register(Services.RULE_ORDER_SERVICE, lambda: RuleOrderService(), Lifetime.IMPORT)
    return service_provider

def register_global_state(import_state: 'ImportStateController') -> None:
    service_provider = ServiceProvider()
    service_provider.register(Services.GLOBAL_STATE, lambda: GlobalState(import_state), Lifetime.SINGLETON)


def _diff_dicts(a: dict[Any, Any], b: dict[Any, Any], strict: bool, path: str) -> list[str]:
    diffs: list[str] = []
    for k in a:
        if k not in b:
            diffs.append(f"Key '{k}' missing in second object at {path}")
        else:
            diffs.extend(find_all_diffs(a[k], b[k], strict, f"{path}.{k}"))
    for k in b:
        if k not in a:
            diffs.append(f"Key '{k}' missing in first object at {path}")
    return diffs


def _diff_lists(a: list[Any], b: list[Any], strict: bool, path: str) -> list[str]:
    diffs: list[str] = []
    for i, (x, y) in enumerate(zip(a, b)):
        diffs.extend(find_all_diffs(x, y, strict, f"{path}[{i}]"))
    if len(a) != len(b):
        diffs.append(f"list length mismatch at {path}: {len(a)} != {len(b)}")
    return diffs


def _diff_scalars(a: Any, b: Any, strict: bool, path: str) -> list[str]:
    diffs: list[str] = []
    if a != b:
        if not strict and (a is None or a == '') and (b is None or b == ''):
            return diffs
        diffs.append(f"Value mismatch at {path}: {a} != {b}")
    return diffs


def find_all_diffs(a: Any, b: Any, strict: bool = False, path: str = "root") -> list[str]:
    if isinstance(a, dict) and isinstance(b, dict):
        return _diff_dicts(a, b, strict, path) # type: ignore
    elif isinstance(a, list) and isinstance(b, list):
        return _diff_lists(a, b, strict, path) # type: ignore
    else:
        return _diff_scalars(a, b, strict, path)


def sort_and_join(input_list: list[str]) -> str:
    """ Sorts the input list of strings and joins them using the standard list delimiter. """
    return fwo_const.LIST_DELIMITER.join(sorted(input_list))

def sort_and_join_refs(input_list: list[tuple[str, str]]) -> tuple[str, str]:
    """ Sorts the input list of (uid, name) tuples and joins uids and names separately using the standard list delimiter. """
    sorted_list = sorted(input_list, key=lambda x: x[1])  # sort by name
    uids = [item[0] for item in sorted_list]
    names = [item[1] for item in sorted_list]
    joined_uids = fwo_const.LIST_DELIMITER.join(uids)
    joined_names = fwo_const.LIST_DELIMITER.join(names)
    return joined_uids, joined_names

def generate_hash_from_dict(input_dict: dict[Any, Any]) -> str:
    """ Generates a consistent hash from a dictionary by serializing it with sorted keys. """
    dict_string = json.dumps(input_dict, sort_keys=True)
    return hashlib.sha256(dict_string.encode('utf-8')).hexdigest()
