#!/usr/bin/python3

import sys
from time import sleep

assert len(sys.argv) == 2
lock_file_path = sys.argv[1]
lock_file = None

access_released = False
while not access_released:
    try:
        lock_file = open(lock_file_path, "a")
        lock_file.writelines("RELEASED\n")
        access_released = True
    except Exception as e:
        sleep(0.1)
        print(e)
    finally:
        if lock_file != None:
            lock_file.close()
    sleep(0.1)
