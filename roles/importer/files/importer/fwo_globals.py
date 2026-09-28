from __future__ import annotations

# fwo_globals.py

# Applies to outbound connections to external systems only: firewall management
# APIs and config files fetched over http(s). Their certificates are outside
# FWO's control, so verification stays operator configurable.
#
# It does NOT apply to FWO's own API and middleware. Those present internal CA
# certificates and are always verified against tls_ca_certificate from
# fworch.json, with the client identity attached; see
# FwoApi._configure_internal_api_session.
verify_certs = None
suppress_cert_warnings = None
debug_level = 0
shutdown_requested = False


def set_global_values(verify_certs_in: bool | None, suppress_cert_warnings_in: bool | None):
    global verify_certs, suppress_cert_warnings  # noqa: PLW0603
    verify_certs = verify_certs_in
    suppress_cert_warnings = suppress_cert_warnings_in
