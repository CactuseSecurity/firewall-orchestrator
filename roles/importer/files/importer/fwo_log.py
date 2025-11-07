import logging
import time
import threading
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from importer.services.uid2id_mapper import Uid2IdMapper
    from importer.model_controllers.import_state_controller import ImportStateController
from typing import Any, Literal


class LogLock:
    semaphore = threading.Semaphore()

    @staticmethod
    def handle_log_lock():
        # Initialize values
        lock_file_path = "/var/fworch/lock/importer_api_log.lock"
        log_owned_by_external = False
        stopwatch = time.time()

        while True:
            try:
                with open(lock_file_path, "a+") as file:
                    # Jump to the beginning of the file
                    file.seek(0)
                    # Read the file content
                    lock_file_content = file.read().strip()
                    # Forcefully release lock after timeout
                    if log_owned_by_external and time.time() - stopwatch > 10:
                        file.write("FORCEFULLY RELEASED\n")
                        stopwatch = -1
                        LogLock.semaphore.release()
                        log_owned_by_external = False
                    # GRANTED - lock was granted by us
                    elif lock_file_content.endswith("GRANTED"):
                        # Request lock if it is not already requested by us
                        # (in case of restart with log already granted)
                        if not log_owned_by_external:
                            LogLock.semaphore.acquire()
                            stopwatch = time.time()
                            log_owned_by_external = True
                    # REQUESTED - lock was requested by log swap process
                    elif lock_file_content.endswith("REQUESTED"):
                        # only request lock if it is not already requested by us
                        if not log_owned_by_external:
                            LogLock.semaphore.acquire()
                            stopwatch = time.time()
                            log_owned_by_external = True
                            file.write("GRANTED\n")
                    # RELEASED - lock was released by log swap process
                    elif lock_file_content.endswith("RELEASED"):
                        # only release lock if it was formerly requested by us
                        if log_owned_by_external:
                            stopwatch = -1
                            LogLock.semaphore.release()
                            log_owned_by_external = False
            except Exception as _:
                pass
            # Wait a second
            time.sleep(1)


# Used to accquire lock before log processing
# class LogFilter(logging.Filter):
#     def filter(self, record):
#         # Acquire lock
#         LogLock.semaphore.acquire()
#         # Return True to allow the log record to be processed
#         return True


# Used to release lock after log processing
# class LogHandler(logging.StreamHandler):
#     def emit(self, record):
#         # Call the parent class's emit method to perform the actual logging
#         super().emit(record)
#         # Release lock
#         LogLock.semaphore.release()


def getFwoLogger(debug_level: int = 0):
    if int(debug_level) >= 1:
        log_level = logging.DEBUG
    else:
        log_level = logging.INFO

    logger = logging.getLogger()
    #log_handler = LogHandler(stream=sys.stdout)
    #log_filter = LogFilter()

    log_format = "%(asctime)s [%(levelname)-5.5s] [%(filename)-25.25s:%(funcName)-25.25s:%(lineno)4d] %(message)s"
    #log_handler.setLevel(log_level)
    #log_handler.addFilter(log_filter)
    #handlers = [log_handler]
    
    #logging.basicConfig(format=log_format, datefmt="%Y-%m-%dT%H:%M:%S%z", handlers=handlers, level=log_level)
    logging.basicConfig(format=log_format, datefmt="%Y-%m-%dT%H:%M:%S%z", level=log_level)
    logger.setLevel(log_level)

    # Set log level for noisy requests/connectionpool module to WARNING:
    connection_log = logging.getLogger("urllib3.connectionpool")
    connection_log.setLevel(logging.WARNING)
    connection_log.propagate = True
    return logger


def getFwoAlertLogger(debug_level: int = 0):
    debug_level = int(debug_level) # TODO: Check why str is passed sometimes or why the int cast is needed
    if debug_level >= 1:
        llevel = logging.DEBUG
    else:
        llevel = logging.INFO

    logger = logging.getLogger() # use root logger
    # log_handler = LogHandler(stream=sys.stdout)
    # log_filter = LogFilter()

    logformat = "%(asctime)s %(message)s"
    # log_handler.setLevel(llevel)
    # log_handler.addFilter(log_filter)
    # handlers = [log_handler]

    # logging.basicConfig(format=logformat, datefmt="", handlers=handlers, level=llevel)
    logging.basicConfig(format=logformat, datefmt="", level=llevel)
    logger.setLevel(llevel)

    # set log level for noisy requests/connectionpool module to WARNING: 
    connection_log = logging.getLogger("urllib3.connectionpool")
    connection_log.setLevel(logging.WARNING)
    connection_log.propagate = True
    
    if debug_level>8:
        logger.debug ("debug_level=" + str(debug_level) )
    return logger


class ChangeLogger:
    """
         A singleton service that holds data and provides logic to compute changelog data for network objects, services and rules.
    """

    _instance = None
    changed_nwobj_id_map: dict[str, int]
    changed_svc_id_map: dict[str, int]
    _import_state = None
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


    def create_change_id_maps(self, uid2id_mapper: "Uid2IdMapper", changed_nw_objs: list[str], changed_svcs: list[str], removedNwObjIds: list[dict[str, str]], removedNwSvcIds: list[dict[str, str]]):
                                                                                                                        #TODO: removedNwObjUids?               #TODO: removedNwObjUids?
        self._uid2id_mapper = uid2id_mapper

        self.changed_object_id_map = {
            next(removedNwObjId['obj_id']
                for removedNwObjId in removedNwObjIds
                if removedNwObjId['obj_uid'] == old_item
            ): self._uid2id_mapper.get_network_object_id(old_item) 
            for old_item in changed_nw_objs
        }

        self.changed_service_id_map = {
            next(removedNwSvcId['svc_id']
                for removedNwSvcId in removedNwSvcIds
                if removedNwSvcId['svc_uid'] == old_item
            ): self._uid2id_mapper.get_service_object_id(old_item)
            for old_item in changed_svcs
        }


    def create_changelog_import_object(self, type: str, import_state: "ImportStateController", change_action: str, changeTyp: Literal[2, 3], importTime: str, rule_id: int, rule_id_alternative: int = 0) -> dict[str, Any]:
        
        uniqueName = self._get_changelog_import_object_unique_name(rule_id)
        old_rule_id = None
        new_rule_id = None
        self._import_state = import_state

        if change_action in ['I', 'C']:
            new_rule_id = rule_id
        
        if change_action == 'C':
            old_rule_id = rule_id_alternative

        if change_action == 'D':
            old_rule_id = rule_id

        rule_changelog_object: dict[str, Any] =  {
            f"new_{type}_id": new_rule_id,
            f"old_{type}_id": old_rule_id,
            "control_id": self._import_state.ImportId,
            "change_action": change_action,
            "mgm_id": self._import_state.MgmDetails.Id,
            "change_type_id": changeTyp,
            "change_time": importTime,
            "unique_name": uniqueName,
        }

        return rule_changelog_object
    

    def _get_changelog_import_object_unique_name(self, changelog_entity_id: int) -> str:
        return  str(changelog_entity_id)
    
