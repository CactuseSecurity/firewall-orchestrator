#!/usr/bin/python3

import sys
from contextlib import contextmanager
from time import sleep

try:
    import fcntl
except ImportError:
    fcntl = None


k_lock_retry_delay = 0.1
k_expected_argument_count = 2


def write_message(message):
    print(message)  # noqa: T201


@contextmanager
def locked_file(lock_file_path, mode):
    with open(lock_file_path, mode) as lock_file:
        if fcntl is not None:
            fcntl.flock(lock_file.fileno(), fcntl.LOCK_EX)
        try:
            yield lock_file
        finally:
            lock_file.flush()
            if fcntl is not None:
                fcntl.flock(lock_file.fileno(), fcntl.LOCK_UN)


def release_lock(lock_file_path, retry_delay=k_lock_retry_delay):
    while True:
        try:
            with locked_file(lock_file_path, "a") as lock_file:
                lock_file.write("RELEASED\n")
                write_message("Lock was released.")
                return
        except Exception as error:
            sleep(retry_delay)
            write_message(error)
        sleep(retry_delay)


def main():
    assert len(sys.argv) == k_expected_argument_count
    release_lock(sys.argv[1])


if __name__ == "__main__":
    main()
