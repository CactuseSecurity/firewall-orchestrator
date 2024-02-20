#!/usr/bin/python3

import sys
from time import sleep, time

assert len(sys.argv) == 2
lock_file_path = sys.argv[1]

last_line = ""
start_time = time()

while True:
    try:            
        with open(lock_file_path, "a+") as lock_file:
            # Read the last line of the lock file
            lock_file.seek(0)
            lines = lock_file.readlines()
            last_line = lines[-1].strip() if lines else "" 
            # Exit if lock was granted
            if last_line.endswith("GRANTED"):
                print("Lock was granted.")
                exit()
            # Check if timeout reached
            if time() - start_time > 10:
                lock_file.write("FORCEFULLY GRANTED\n")                
                print("Forcefully granted lock after timeout was reached.")
                exit()
            # Request lock if not already done
            if not last_line.endswith("REQUESTED"):
                lock_file.write("REQUESTED\n")
                print("Lock was requested. Waiting until it is granted.")
    except Exception as e:
        sleep(0.1)
        print(e)
    sleep(0.1)
