#!/usr/bin/python3

import sys
from time import sleep

assert len(sys.argv) == 2
lock_file_path = sys.argv[1]

while True:
    try:
        with open(lock_file_path, "a") as lock_file:
            lock_file.write("RELEASED\n")
            print("Lock was released.")
            exit()
    except Exception as e:
        sleep(0.1)
        print(e)
    sleep(0.1)
