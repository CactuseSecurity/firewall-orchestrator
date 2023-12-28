import logging
from sys import stdout
import fwo_globals
import asyncio
import time
import random
from asyncio import Semaphore

class LogLock:
    semaphore = Semaphore(value=1)

    async def get_file(path):
        while True:
            try:
                # Try to access file
                file = open(path, "a+")
                # Move the file cursor to the beginning
                file.seek(0)
                return file
            except Exception:
                # Random offset to avoid synced file access
                await asyncio.sleep(random.random() / 10)

    async def handle_log_lock():
        # Initialize values
        lock_file_path = "/var/fworch/lock/importer_api_log.lock"
        log_owned_by_external = False
        stopwatch = time.time()

        while True:
            try:
                async with await LogLock.get_file(lock_file_path) as file:
                    lock_file_content = (await file.read()).strip()

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
                            await LogLock.semaphore.acquire()
                            stopwatch = time.time()
                            log_owned_by_external = True
                    # REQUESTED - lock was requested by log swap process
                    elif lock_file_content.endswith("REQUESTED"):
                        # only request lock if it is not already requested by us
                        if not log_owned_by_external:
                            await LogLock.semaphore.acquire()
                            stopwatch = time.time()
                            log_owned_by_external = True
                            file.write("FORCEFULLY RELEASED\n")
                    # RELEASED - lock was released by log swap process
                    elif lock_file_content.endswith("RELEASED"):
                        # only release lock if it was formerly requested by us
                        if log_owned_by_external:
                            stopwatch = -1
                            LogLock.semaphore.release()
                            log_owned_by_external = False
            except Exception:
                pass
            
            await asyncio.sleep(1)

# Used to accquire lock before log processing
class LogFilter(logging.Filter):
    def filter(self, record):
        # Acquire lock
        LogLock.semaphore.acquire()
        # Return True to allow the log record to be processed
        return True  

# Used to release lock after log processing
class LogHandler(logging.StreamHandler):
    def emit(self, record):
        # Call the parent class's emit method to perform the actual logging
        super().emit(record)
        # Release lock
        LogLock.semaphore.release()

def getFwoLogger():
    debug_level = int(fwo_globals.debug_level)
    if debug_level >= 1:
        log_level = logging.DEBUG
    else:
        log_level = logging.INFO

    logger = logging.getLogger()
    log_handler = LogHandler(stream=stdout)
    log_filter = LogFilter()

    log_format = "%(asctime)s [%(levelname)-5.5s] [%(filename)-10.10s:%(funcName)-10.10s:%(lineno)4d] %(message)s"
    log_handler.setLevel(log_level)
    log_handler.addFilter(log_filter)
    handlers = [log_handler]
    
    logging.basicConfig(format=log_format, datefmt="%Y-%m-%dT%H:%M:%S%z", handlers=handlers, level=log_level)
    logger.setLevel(log_level)

    # Set log level for noisy requests/connectionpool module to WARNING:
    connection_log = logging.getLogger("urllib3.connectionpool")
    connection_log.setLevel(logging.WARNING)
    connection_log.propagate = True

    if debug_level > 8:
        logger.debug("debug_level=" + str(debug_level))

    return logger


def getFwoAlertLogger(debug_level=0):
    debug_level=int(debug_level)
    if debug_level>=1:
        llevel = logging.DEBUG
    else:
        llevel = logging.INFO

    logger = logging.getLogger() # use root logger
    log_handler = LogHandler(stream=stdout)
    log_filter = LogFilter()

    logformat = "%(asctime)s %(message)s"
    log_handler.setLevel(llevel)
    log_handler.addFilter(log_filter)
    handlers = [log_handler]

    logging.basicConfig(format=logformat, datefmt="", handlers=handlers, level=llevel)
    logger.setLevel(llevel)

    # set log level for noisy requests/connectionpool module to WARNING: 
    connection_log = logging.getLogger("urllib3.connectionpool")
    connection_log.setLevel(logging.WARNING)
    connection_log.propagate = True
    
    if debug_level>8:
        logger.debug ("debug_level=" + str(debug_level) )
    return logger
