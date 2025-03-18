import uuid

class UidManager:
    def __init__(self):
        self.used_uids = []

    def create_uid(self):
        need_new_uid = True
        new_uid = ""

        while need_new_uid:
            new_uid = str(uuid.uuid4())
            if not new_uid in self.used_uids:
                self.used_uids.append(new_uid)
                need_new_uid = False

        return new_uid