#!/usr/bin/python3
# takes json from stdin and converts it from ugly to pretty json format

import json
json_obj = json.loads(input())
print(json.dumps(json_obj, indent=2))
