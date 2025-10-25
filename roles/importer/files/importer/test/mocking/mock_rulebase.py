class MockRulebase:
    id = None
    uid = ""
    name = ""
    mgm_uid = ""
    is_global = False
    rules = {}


    def to_dict(self):
        return {
            "id": self.id,
            "uid": self.uid,
            "name": self.name,
            "mgm_uid": self.mgm_uid,
            "is_global": self.is_global,
            "Rules": self.rules
        }
    