
class FwLoginFailed(Exception):
    """Raised when login to FW management failed"""

    def __init__(self, message="Login to FW management failed"):
        self.message = message
        super().__init__(self.message)

class FwLogoutFailed(Exception):
    """Raised when logout from FW management failed"""

    def __init__(self, message="Logout from FW management failed"):
        self.message = message
        super().__init__(self.message)

class SecretDecryptionFailed(Exception):
    """Raised when the attempt to decrypt a secret with the given key fails"""

    def __init__(self, message="Could not decrypt an API secret with given key"):
        self.message = message
        super().__init__(self.message)

class FwoApiLoginFailed(Exception):
    """Raised when login to FWO API fails"""

    def __init__(self, message="Login to FWO API failed"):
        self.message = message
        super().__init__(self.message)

class FwoApiFailedLockImport(Exception):
    """Raised when unable to lock import (import running?)"""

    def __init__(self, message="Locking import failed - already running?"):
        self.message = message
        super().__init__(self.message)

class FwoApiWriteError(Exception):
    """Raised when an FWO API mutation fails"""

    def __init__(self, message="FWO API mutation failed"):
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

class FwoApiServiceUnavailable(Exception):
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

class ImportInterruption(Exception):
    """Custom exception to signal an interrupted call requiring rollback."""
    def __init__(self, message="Operation interrupted. Rollback required."):
        super().__init__(message)

class FwoImporterError(Exception):
    """Custom exception to signal a failed import attempt."""
    def __init__(self, message="Operation interrupted. Rollback required."):
        super().__init__(message)

class RollbackNecessary(Exception):
    """Custom exception to signal a failed import attempt which needs a rollback."""
    def __init__(self, message="Rollback required."):
        super().__init__(message)

class RollbackError(Exception):
    """Custom exception to signal a failed rollback attempt."""
    def __init__(self, message="Rollback failed."):
        super().__init__(message)

class FwApiError(Exception):
    """Custom exception to signal a failure during access checkpoint api."""
    def __init__(self, message="Error while trying to access firewall management API."):
        super().__init__(message)

class FwApiResponseDecodingError(Exception):
    """Custom exception to signal a failure during decoding checkpoint api response to JSON."""
    def __init__(self, message="Error while trying to decode firewall management API response into JSON."):
        super().__init__(message)
