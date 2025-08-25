# fwo_globals.py
verify_certs = None
suppress_cert_warnings = None
debug_level = 0
shutdown_requested = False

def set_global_values(verify_certs_in=None, suppress_cert_warnings_in=None, debug_level_in=0):
    global verify_certs, suppress_cert_warnings, debug_level
    verify_certs = verify_certs_in
    suppress_cert_warnings = suppress_cert_warnings_in
    debug_level = int(debug_level_in)
