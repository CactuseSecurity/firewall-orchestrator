#!/usr/bin/python3

import sys
from time import sleep

assert len(sys.argv) == 2
lock_file_path = sys.argv[1]

last_line = ""
while True:
    try:
        with open(lock_file_path, "a+") as lock_file:
            # Read the last line of the lock file
            lock_file.seek(0)
            lines = lock_file.readlines()
            last_line = lines[-1].strip() if lines else "" 
            # Exit if lock was granted
            if last_line == "GRANTED":
                print("Lock was granted.")
                exit()
            # Request lock if not already done
            elif last_line != "REQUESTED":
                lock_file.write("REQUESTED\n")
                print("Lock was requested. Waiting until it is granted.")
    except Exception as e:
        sleep(0.1)
        print(e)
    sleep(0.1)
