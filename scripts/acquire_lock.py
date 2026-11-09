#!/usr/bin/python3

import sys
from contextlib import contextmanager
from time import sleep, time

try:
    import fcntl
except ImportError:
    fcntl = None


k_lock_retry_delay = 0.1
k_lock_timeout = 10
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


def read_last_line(lock_file):
    lock_file.seek(0)
    lines = lock_file.readlines()
    return lines[-1].strip() if lines else ""


def append_line(lock_file, line):
    lock_file.seek(0, 2)
    lock_file.write(f"{line}\n")


def acquire_lock(lock_file_path, timeout=k_lock_timeout, retry_delay=k_lock_retry_delay):
    start_time = time()

    while True:
        try:
            with locked_file(lock_file_path, "a+") as lock_file:
                last_line = read_last_line(lock_file)
                if last_line.endswith("GRANTED"):
                    write_message("Lock was granted.")
                    return
                if time() - start_time > timeout:
                    append_line(lock_file, "FORCEFULLY GRANTED")
                    write_message("Forcefully granted lock after timeout was reached.")
                    return
                if not last_line.endswith("REQUESTED"):
                    append_line(lock_file, "REQUESTED")
                    write_message("Lock was requested. Waiting until it is granted.")
        except Exception as error:
            sleep(retry_delay)
            write_message(error)
        sleep(retry_delay)


def main():
    assert len(sys.argv) == k_expected_argument_count
    acquire_lock(sys.argv[1])


if __name__ == "__main__":
    main()
