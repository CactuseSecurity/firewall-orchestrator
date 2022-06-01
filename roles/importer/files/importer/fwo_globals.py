from urllib.parse import urlparse
import socket

debug_level=0


def setGlobalValues (
        proxy_in=None,
        verify_certs_in=None, 
        suppress_cert_warnings_in=None,
        debug_level_in = 0,
        fwo_api_url = None
    ):
    global verify_certs
    global suppress_cert_warnings
    global proxy
    global debug_level
    verify_certs = verify_certs_in
    suppress_cert_warnings = suppress_cert_warnings_in
    debug_level = debug_level_in
    
    if proxy_in is None:
        proxy = {}
    else:
        proxy = { "no_proxy": "localhost,127.0.0.1,::", "http_proxy": proxy_in, "https_proxy": proxy_in }
        
        # if fwo_api host == local importer host or fwo_api host resolved = localhost: add hostname of fwo_api host to no_proxy exceptions
        api_url = urlparse(fwo_api_url)
        api_hostname = api_url.hostname
        api_ip = socket.gethostbyname(api_hostname)
        if api_hostname == 'localhost' or api_ip == '127.0.0.1' or api_ip == '::':
            proxy['no_proxy'] += ',' + api_hostname

        local_importer_hostname = socket.gethostname()
        importer_ip = socket.gethostbyname(local_importer_hostname)
        if importer_ip == api_ip or local_importer_hostname == api_hostname:
            proxy['no_proxy'] += ',' + local_importer_hostname
