import logging
from sys import stdout


def getFwoLogger(debug_level=0):
    debug_level=int(debug_level)
    if debug_level>=1:
        llevel = logging.DEBUG
    else:
        llevel = logging.INFO

    logger = logging.getLogger() # use root logger
    logHandler = logging.StreamHandler(stream=stdout)
    logformat = "%(asctime)s [%(levelname)-5.5s] [%(filename)-10.10s:%(funcName)-10.10s:%(lineno)4d] %(message)s"
    logHandler.setLevel(llevel)
    handlers = [logHandler]
    logging.basicConfig(format=logformat, datefmt="%Y-%m-%d %H:%M:%S", handlers=handlers, level=llevel)
    logger.setLevel(llevel)

    # set log level for noisy requests/connectionpool module to WARNING: 
    connection_log = logging.getLogger("urllib3.connectionpool")
    connection_log.setLevel(logging.WARNING)
    connection_log.propagate = True
    
    if debug_level>8:
        logger.debug ("debug_level=" + str(debug_level) )
    return logger
