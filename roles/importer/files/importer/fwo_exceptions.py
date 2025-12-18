rollback_string = "Operation interrupted. Rollback required."


class FwLoginFailedError(Exception):
    """Raised when login to FW management failed"""

    def __init__(self, message: str = "Login to FW management failed"):
        self.message = message
        super().__init__(self.message)


class FwApiCallFailedError(Exception):
    """Raised when FW management API call failed"""

    def __init__(self, message: str = "An API call to the FW management failed"):
        self.message = message
        super().__init__(self.message)


class FwLogoutFailedError(Exception):
    """Raised when logout from FW management failed"""

    def __init__(self, message: str = "Logout from FW management failed"):
        self.message = message
        super().__init__(self.message)


class FwoNativeConfigFetchError(Exception):
    """Raised when getting native config from FW management fails, no rollback necessary"""

    def __init__(self, message: str = "Login to FW management failed"):
        self.message = message
        super().__init__(self.message)


class FwoNormalizedConfigParseError(Exception):
    """Raised while parsing normalized config"""

    def __init__(self, message: str = "Parsing normalized config failed"):
        self.message = message
        super().__init__(self.message)


class SecretDecryptionFailedError(Exception):
    """Raised when the attempt to decrypt a secret with the given key fails"""

    def __init__(self, message: str = "Could not decrypt an API secret with given key"):
        self.message = message
        super().__init__(self.message)


class FwoApiLoginFailedError(Exception):
    """Raised when login to FWO API fails"""

    def __init__(self, message: str = "Login to FWO API failed"):
        self.message = message
        super().__init__(self.message)


class FwoApiFailedLockImportError(Exception):
    """Raised when unable to lock import (import running?)"""

    def __init__(self, message: str = "Locking import failed - already running?"):
        self.message = message
        super().__init__(self.message)


class FwoApiFailedUnLockImportError(Exception):
    """Raised when unable to remove import lock"""

    def __init__(self, message: str = "Unlocking import failed"):
        self.message = message
        super().__init__(self.message)


class FwoApiWriteError(Exception):
    """Raised when an FWO API mutation fails"""

    def __init__(self, message: str = "FWO API mutation failed"):
        self.message = message
        super().__init__(self.message)


class FwoApiFailureError(Exception):
    """Raised for any other FwoApi call exceptions"""

    def __init__(self, message: str = "There was an unclassified error while executing an FWO API call"):
        self.message = message
        super().__init__(self.message)


class FwoApiTimeoutError(Exception):
    """Raised for 502 http error with proxy due to timeout"""

    def __init__(
        self,
        message: str = "reverse proxy timeout error during FWO API call - try increasing the reverse proxy timeout",
    ):
        self.message = message
        super().__init__(self.message)


class FwoApiServiceUnavailableError(Exception):
    """Raised for 503 http error Serice unavailable"""

    def __init__(self, message: str = "FWO API Hasura container died"):
        self.message = message
        super().__init__(self.message)


class ConfigFileNotFoundError(Exception):
    """can only happen when specifying config file with -i switch"""

    def __init__(self, message: str = "Could not read config file"):
        self.message = message
        super().__init__(self.message)


class ImportRecursionLimitReachedError(Exception):
    """Raised when recursion of function inimport process reaches max allowed recursion limit"""

    def __init__(self, message: str = "Max recursion level reached - aborting"):
        self.message = message
        super().__init__(self.message)


class ImportInterruptionError(Exception):
    """Custom exception to signal an interrupted call requiring rollback."""

    def __init__(self, message: str = rollback_string):
        super().__init__(message)


class FwoImporterError(Exception):
    """Custom exception to signal a failed import attempt."""

    def __init__(self, message: str = rollback_string):
        super().__init__(message)


class FwoImporterErrorInconsistenciesError(Exception):
    """Custom exception to signal a failed import attempt."""

    def __init__(self, message: str = rollback_string):
        super().__init__(message)


class RollbackNecessaryError(Exception):
    """Custom exception to signal a failed import attempt which needs a rollback."""

    def __init__(self, message: str = "Rollback required."):
        super().__init__(message)


class RollbackError(Exception):
    """Custom exception to signal a failed rollback attempt."""

    def __init__(self, message: str = "Rollback failed."):
        super().__init__(message)


class FwApiError(Exception):
    """Custom exception to signal a failure during access checkpoint api."""

    def __init__(self, message: str = "Error while trying to access firewall management API."):
        super().__init__(message)


class FwApiResponseDecodingError(Exception):
    """Custom exception to signal a failure during decoding checkpoint api response to JSON."""

    def __init__(self, message: str = "Error while trying to decode firewall management API response into JSON."):
        super().__init__(message)


class FwoApiFailedDeleteOldImportsError(Exception):
    """Custom exception to signal a failure during deletion of old import data."""

    def __init__(self, message: str = "Error while trying to remove old import data."):
        super().__init__(message)


class FwoDuplicateKeyViolationError(Exception):
    """Custom exception to signal a duplicate key violation during import."""

    def __init__(self, message: str = "Error while trying to add data with duplicate keys"):
        super().__init__(message)


class FwoUnknownDeviceForManagerError(Exception):
    """Custom exception to signal an unknown device during import."""

    def __init__(self, message: str = "Could not find device in manager config"):
        super().__init__(message)


class FwoDeviceWithoutLocalPackageError(Exception):
    """Custom exception to signal a device without local package."""

    def __init__(self, message: str = "Could not local package for device in manager config"):
        super().__init__(message)


class ShutdownRequestedError(Exception):
    pass
