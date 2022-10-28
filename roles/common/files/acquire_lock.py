import sys
from time import sleep

assert len(sys.argv) == 1
lock_file_path = sys.argv[0]

access_possible = False
while not access_possible:
    try:
        lock_file = open(lock_file_path, "r")
        lock_file_content_raw = lock_file.readlines()
        if len(lock_file_content_raw) == 0:
            # empty file
            access_possible = True
        else:
            lock_file_content = lock_file_content_raw[-1].strip()
            if lock_file_content == "":
                access_possible = True
            elif lock_file_content.endswith("ACKNOWLEDGED"):
                access_possible = True
            elif lock_file_content.endswith("RELEASED"):
                print("Waiting for release acknowledge.")
                sleep(0.5)
    except:
        sleep(0.1)
    finally:
        if lock_file != None:
            lock_file.close()
    sleep(0.1)

access_requested = False
while not access_requested:
    try:
        lock_file = open(lock_file_path, "w")
        lock_file.writelines("REQUESTED\n")
        access_requested = True
    except:
        sleep(0.1)
    finally:
        if lock_file != None:
            lock_file.close()
    sleep(0.1)

access_granted = False
while not access_granted:
    try:
        lock_file = open(lock_file_path, "a+")
        # jump to beginning of file
        lock_file.seek(0)
        access_granted = lock_file.readlines(
        )[-1].strip().endswith("GRANTED")
    except:
        sleep(0.1)
    finally:
        if lock_file != None:
            lock_file.close()
    sleep(0.1)
