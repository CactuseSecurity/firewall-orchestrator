# fwo_globals.py
verify_certs = None
suppress_cert_warnings = None
debug_level = 0
shutdown_requested = False


def set_global_values(verify_certs_in: bool | None, suppress_cert_warnings_in: bool | None):
    global verify_certs, suppress_cert_warnings, debug_level
    verify_certs = verify_certs_in
    suppress_cert_warnings = suppress_cert_warnings_in
