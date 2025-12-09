import logging
import threading
import time
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from models.import_state import ImportState

    from importer.services.uid2id_mapper import Uid2IdMapper
from typing import Any, Literal


class LogLock:
    semaphore = threading.Semaphore()

    @staticmethod
    def handle_log_lock():
        # Initialize values
        lock_file_path = "/var/fworch/lock/importer_api_log.lock"
        log_owned = False
        stopwatch = time.time()

        while True:
            try:
                with open(lock_file_path, "a+") as file:
                    # Jump to the beginning of the file
                    file.seek(0)
                    # Read the file content
                    lock_file_content = file.read().strip()

                    if log_owned:
                        # Forcefully release lock after timeout
                        if time.time() - stopwatch > 10:
                            file.write("FORCEFULLY RELEASED\n")
                            stopwatch = -1
                            LogLock.semaphore.release()
                            log_owned = False

                        elif lock_file_content.endswith("RELEASED"):
                            # RELEASED - lock was released by log swap process
                            # only release lock if it was formerly requested by us
                            stopwatch = -1
                            LogLock.semaphore.release()
                            log_owned = False

                    elif lock_file_content.endswith("GRANTED"):
                        # Request lock if it is not already requested by us
                        # (in case of restart with log already granted)
                        LogLock.semaphore.acquire()
                        stopwatch = time.time()
                        log_owned = True

                    elif lock_file_content.endswith("REQUESTED"):
                        # REQUESTED - lock was requested by log swap process
                        LogLock.semaphore.acquire()
                        stopwatch = time.time()
                        log_owned = True
                        file.write("GRANTED\n")
            except Exception as _:
                pass
            # Wait a second
            time.sleep(1)


class FWOLogger:
    logger: logging.Logger
    debug_level: int

    def __new__(cls, debug_level: int = 0):
        if not hasattr(cls, "instance"):
            cls.instance = super(FWOLogger, cls).__new__(cls)
        return cls.instance

    def __init__(self, debug_level: int = 0):
        self.logger = get_fwo_logger(debug_level)
        self.debug_level = debug_level

    def get_logger(self) -> logging.Logger:
        return self.logger

    def set_debug_level(self, debug_level: int):
        if int(debug_level) >= 1:
            log_level = logging.DEBUG
        else:
            log_level = logging.INFO
        self.logger.setLevel(log_level)

    @staticmethod
    def debug(msg: str, needed_level: int = 1):
        log = FWOLogger.instance.get_logger()
        if FWOLogger.instance.debug_level >= needed_level:
            # Find the caller's frame to show correct file/function/line info
            log.debug(msg, stacklevel=2)

    @staticmethod
    def error(msg: str):
        logger = FWOLogger.instance.get_logger()
        logger.error(msg, stacklevel=2)

    @staticmethod
    def info(msg: str):
        logger = FWOLogger.instance.get_logger()
        logger.info(msg, stacklevel=2)

    @staticmethod
    def warning(msg: str):
        logger = FWOLogger.instance.get_logger()
        logger.warning(msg, stacklevel=2)

    @staticmethod
    def exception(msg: str, exc_info: Any = None):
        logger = FWOLogger.instance.get_logger()
        logger.exception(msg, exc_info=exc_info, stacklevel=2)

    @staticmethod
    def is_debug_level(level: int) -> bool:
        return FWOLogger.instance.debug_level >= level


def get_fwo_logger(debug_level: int = 0) -> logging.Logger:
    if int(debug_level) >= 1:
        log_level = logging.DEBUG
    else:
        log_level = logging.INFO

    logger = logging.getLogger()
    log_format = "%(asctime)s [%(levelname)-5.5s] [%(filename)-25.25s:%(funcName)-25.25s:%(lineno)4d] %(message)s"

    logging.basicConfig(format=log_format, datefmt="%Y-%m-%dT%H:%M:%S%z", level=log_level)
    logger.setLevel(log_level)

    # Set log level for noisy requests/connectionpool module to WARNING:
    connection_log = logging.getLogger("urllib3.connectionpool")
    connection_log.setLevel(logging.WARNING)
    connection_log.propagate = True
    return logger


class ChangeLogger:
    """
    A singleton service that holds data and provides logic to compute changelog data for network objects, services and rules.
    """

    _instance = None
    changed_nwobj_id_map: dict[int, int]
    changed_svc_id_map: dict[int, int]
    _import_state: ImportState | None = None
    _uid2id_mapper: "Uid2IdMapper | None" = None

    def __new__(cls):
        """
        Singleton pattern: Creates instance and sets defaults if constructed first time and sets that object to a protected class variable.
        If the constructor is called when there is already an instance returns that instance instead. That way there will only be one instance of this type throudgh the whole runtime.
        """
        if cls._instance is None:
            cls._instance = super(ChangeLogger, cls).__new__(cls)
            cls.changed_nwobj_id_map = {}
            cls.changed_svc_id_map = {}
        return cls._instance

    def create_change_id_maps(
        self,
        uid2id_mapper: "Uid2IdMapper",
        changed_nw_objs: list[str],
        changed_svcs: list[str],
        removed_nw_objs: list[dict[str, Any]],
        removed_nw_svcs: list[dict[str, Any]],
    ):
        self._uid2id_mapper = uid2id_mapper

        self.changed_object_id_map = {
            next(
                removedNwObjId["obj_id"] for removedNwObjId in removed_nw_objs if removedNwObjId["obj_uid"] == old_item
            ): self._uid2id_mapper.get_network_object_id(old_item)
            for old_item in changed_nw_objs
        }

        self.changed_service_id_map = {
            next(
                removedNwSvcId["svc_id"] for removedNwSvcId in removed_nw_svcs if removedNwSvcId["svc_uid"] == old_item
            ): self._uid2id_mapper.get_service_object_id(old_item)
            for old_item in changed_svcs
        }

    def create_changelog_import_object(
        self,
        type: str,
        import_state: "ImportState",
        change_action: str,
        change_typ: Literal[2, 3],
        import_time: str,
        rule_id: int,
        rule_id_alternative: int = 0,
    ) -> dict[str, Any]:
        unique_name = self._get_changelog_import_object_unique_name(rule_id)
        old_rule_id = None
        new_rule_id = None
        self._import_state = import_state

        if change_action in ["I", "C"]:
            new_rule_id = rule_id

        if change_action == "C":
            old_rule_id = rule_id_alternative

        if change_action == "D":
            old_rule_id = rule_id

        rule_changelog_object: dict[str, Any] = {
            f"new_{type}_id": new_rule_id,
            f"old_{type}_id": old_rule_id,
            "control_id": self._import_state.import_id,
            "change_action": change_action,
            "mgm_id": self._import_state.mgm_details.mgm_id,
            "change_type_id": change_typ,
            "change_time": import_time,
            "unique_name": unique_name,
        }

        return rule_changelog_object

    def _get_changelog_import_object_unique_name(self, changelog_entity_id: int) -> str:
        return str(changelog_entity_id)
