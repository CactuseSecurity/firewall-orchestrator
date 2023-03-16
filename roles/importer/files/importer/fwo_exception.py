
class FwLoginFailed(Exception):
    """Raised when login to FW management failed"""

    def __init__(self, message="Login to FW management failed"):
            self.message = message
            super().__init__(self.message)

class FwoApiLoginFailed(Exception):
    """Raised when login to FWO API failed"""

    def __init__(self, message="Login to FWO API failed"):
            self.message = message
            super().__init__(self.message)

class FwoApiFailedLockImport(Exception):
    """Raised when unable to lock import (import running?)"""

    def __init__(self, message="Locking import failed - already running?"):
            self.message = message
            super().__init__(self.message)

class FwoApiFailure(Exception):
    """Raised for any other FwoApi call exceptions"""

    def __init__(self, message="There was an unclassified error while executing an FWO API call"):
            self.message = message
            super().__init__(self.message)

class FwoApiTimeout(Exception):
    """Raised for 502 http error with proxy due to timeout"""

    def __init__(self, message="reverse proxy timeout error during FWO API call - try increasing the reverse proxy timeout"):
            self.message = message
            super().__init__(self.message)

class FwoApiTServiceUnavailable(Exception):
    """Raised for 503 http error Serice unavailable"""

    def __init__(self, message="FWO API Hasura container died"):
            self.message = message
            super().__init__(self.message)

class ConfigFileNotFound(Exception):
    """can only happen when specifying config file with -i switch"""

    def __init__(self, message="Could not read config file"):
            self.message = message
            super().__init__(self.message)


class ImportRecursionLimitReached(Exception):
    """Raised when recursion of function inimport process reaches max allowed recursion limit"""

    def __init__(self, message="Max recursion level reached - aborting"):
            self.message = message
            super().__init__(self.message)
