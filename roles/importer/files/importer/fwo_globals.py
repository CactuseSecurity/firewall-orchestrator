from __future__ import annotations

import ssl
from pathlib import Path

# fwo_globals.py

# Applies to outbound connections to external systems only: firewall management
# APIs and config files fetched over http(s). Their certificates are outside
# FWO's control, so verification is one operator setting for all firewall
# connections (config importCheckCertificates), which the command line can only
# force on.
#
# It does NOT apply to FWO's own API and middleware. Those present internal CA
# certificates and are always verified against tls_ca_certificate from
# fworch.json, with the client identity attached; see
# FwoApi._configure_internal_api_session.
verify_certs: bool | str | None = None
suppress_cert_warnings: bool | None = None
cli_verify_certs: bool = False
debug_level = 0
shutdown_requested = False

# The operating system trust stores of the supported platforms (Debian/Ubuntu, Red Hat/Rocky).
# requests would otherwise use the certifi bundle, which ignores CAs an operator added to the host.
SYSTEM_CA_BUNDLE_CANDIDATES: list[str] = [
    "/etc/ssl/certs/ca-certificates.crt",
    "/etc/pki/tls/certs/ca-bundle.crt",
]


def set_global_values(verify_certs_in: bool | str | None, suppress_cert_warnings_in: bool | None) -> None:
    global verify_certs, suppress_cert_warnings  # noqa: PLW0603
    verify_certs = verify_certs_in
    suppress_cert_warnings = suppress_cert_warnings_in


def set_cli_verify_certs(cli_verify_certs_in: bool | None) -> None:
    """Remember the command line switch that forces certificate checking for every management."""
    global cli_verify_certs  # noqa: PLW0603
    cli_verify_certs = bool(cli_verify_certs_in)


def find_system_ca_bundle(candidates: list[str] | None = None) -> str | None:
    """Return the first existing operating system CA bundle, or None if there is none."""
    paths = list(SYSTEM_CA_BUNDLE_CANDIDATES if candidates is None else candidates)
    paths.append(ssl.get_default_verify_paths().openssl_cafile)
    for path in paths:
        if path and Path(path).is_file():
            return path
    return None


def resolve_requests_verify(check_certificates: bool, candidates: list[str] | None = None) -> bool | str:
    """
    Translate the certificate checking decision into the value requests expects for verify.

    False switches checking off. Otherwise the host trust store is named explicitly, so a CA
    the operator added to the host is honoured; True (the certifi bundle) is the fallback.
    """
    if not check_certificates:
        return False
    return find_system_ca_bundle(candidates) or True
