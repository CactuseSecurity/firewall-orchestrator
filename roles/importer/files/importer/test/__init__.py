import atexit
import shutil
import tempfile
from pathlib import Path

from fwo_log import FWOLogger, LogLock

# FWOLogger starts the FWOLogLock background thread, which runs for the whole test session and polls
# LogLock.lock_file_path. Point it at a private lock file before starting it: on an installed FWO host
# the production lock file is written by the log swap, which would make the thread take
# LogLock.semaphore while tests patch or use it.
TEST_LOG_LOCK_DIR = Path(tempfile.mkdtemp(prefix="fwo_importer_test_log_lock_"))
LogLock.lock_file_path = TEST_LOG_LOCK_DIR / "importer_api_log.lock"
atexit.register(shutil.rmtree, TEST_LOG_LOCK_DIR, ignore_errors=True)

FWOLogger(5)
