"""
Keeps credentials and API session tokens out of error texts and log lines.

Error texts of firewall API calls end up in alerts, data issues and the importer log, which are read by more
people than the import credentials are meant for. A session token in there can be used on the management API
until the session times out.
"""

from __future__ import annotations

import hashlib
import re
from typing import Any, cast
from urllib.parse import quote, quote_plus

REDACTED_VALUE = "<redacted>"
# the user info of a url ("user:password@") up to the last "@" of the url, so that an "@" or "/" in the password
# cannot cut it short; a url in a text ends at whitespace, a quote, a bracket or an angle bracket. The fixed
# width look-behind keeps the search linear, as only positions right after "://" can start a match.
URL_CREDENTIALS_PATTERN = re.compile(r"(?<=://)[^\s'\"()<>]*@")
# header and payload keys whose values grant access to a firewall management API; compared in lower case
SENSITIVE_KEYS: frozenset[str] = frozenset(
    {
        "authorization",
        "cookie",
        "set-cookie",
        "x-chkp-sid",
        "x-auth-token",
        "x-api-key",
        "x-hasura-admin-secret",
        "session",
        "sid",
        "password",
        "passwd",
        "pass",
        "secret",
        "token",
        "access_token",
        "api_key",
        "apikey",
        "api-key",
    }
)
MAX_REDACTION_DEPTH = 100
SESSION_FINGERPRINT_LENGTH = 8


def redact_sensitive_values(data: Any, depth: int = 0) -> Any:
    """
    Return a copy of a header or payload structure with the values of sensitive keys replaced.

    Dicts and lists are copied, other values are returned unchanged. Structures nested deeper than
    MAX_REDACTION_DEPTH are replaced as a whole, so nothing below that depth can leak.
    """
    if depth > MAX_REDACTION_DEPTH:
        return REDACTED_VALUE
    if isinstance(data, dict):
        mapping = cast("dict[Any, Any]", data)
        return {
            key: REDACTED_VALUE if str(key).lower() in SENSITIVE_KEYS else redact_sensitive_values(value, depth + 1)
            for key, value in mapping.items()
        }
    if isinstance(data, list):
        elements = cast("list[Any]", data)
        return [redact_sensitive_values(element, depth + 1) for element in elements]
    return data


def redact_secret_in_text(text: str, secret: str | None) -> str:
    """
    Return the text with every occurrence of a known secret replaced, also in its URL-encoded forms.

    Needed for free text a firewall returns, e.g. an error page that echoes the requested URL with the token in
    its query string. Key-based redaction cannot find a secret there.
    """
    if not secret:
        return text
    secret_forms = sorted({secret, quote(secret, safe=""), quote_plus(secret)}, key=len, reverse=True)
    for secret_form in secret_forms:
        text = text.replace(secret_form, REDACTED_VALUE)
    return text


def redact_url_credentials(text: str) -> str:
    """
    Return the text with the user info of every url in it ("scheme://user:password@host") replaced.

    Needed for the text of transport errors: requests names a proxy url it cannot parse in full, including the
    proxy credentials, and those credentials are not known to the importer, so redact_secret_in_text cannot find
    them.
    """
    return URL_CREDENTIALS_PATTERN.sub(f"{REDACTED_VALUE}@", text)


def session_fingerprint(session_id: str | None) -> str:
    """
    Return a short, non-reversible identifier of an API session, enough to correlate log lines of one session.
    """
    if not session_id:
        return "none"
    return hashlib.sha256(session_id.encode()).hexdigest()[:SESSION_FINGERPRINT_LENGTH]
