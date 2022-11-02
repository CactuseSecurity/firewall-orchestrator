from urllib.parse import urlparse
import socket

debug_level=0


def setGlobalValues (
        verify_certs_in=None, 
        suppress_cert_warnings_in=None,
        debug_level_in = 0,
    ):
    global verify_certs
    global suppress_cert_warnings
    global debug_level
    verify_certs = verify_certs_in
    suppress_cert_warnings = suppress_cert_warnings_in
    debug_level = int(debug_level_in)
 