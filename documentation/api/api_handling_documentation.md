# Common API Helpers

## How to convert file from json to yaml

    python -c 'import sys, yaml, json; yaml.safe_dump(json.load(sys.stdin), sys.stdout, default_flow_style=False)' < file.json > file.yaml

## How to convert a yaml file to json

    python -c 'import sys, yaml, json; json.dump(yaml.safe_load(sys.stdin), sys.stdout)' < meta.yaml >meta.json

## How to convert JSON pretty print

from pp to compact:
    python -c 'import sys, json; json.dump(json.load(sys.stdin), sys.stdout)' < file.json > file.json

from compact to pp:
    python -c 'import sys, json; json.dump(json.load(sys.stdin), sys.stdout, indent=3)' < file.json > file.json
