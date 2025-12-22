import uuid
from typing import List


class UidManager:
    def __init__(self):
        self.used_uids: List[str] = []

    def create_uid(self):
        need_new_uid = True
        new_uid = ""

        while need_new_uid:
            new_uid = str(uuid.uuid4())
            if new_uid not in self.used_uids:
                self.used_uids.append(new_uid)
                need_new_uid = False

        return new_uid
