# debug python code in vscode

debugging c# and python does not seem to be compatible, so we need to change the .vscode/lauch.json file.

add the following for python debugging (replacing the parameter with sensible values):

```json
    "configurations": [
        
        {
            "name": "Python: Current File",
            "type": "python",
            "request": "launch",
            "program": "${file}",
            "console": "integratedTerminal",
            "env": { "PYTHONPATH":"${PYTHONPATH}:${workspaceRoot}"},
            "args": ["-a localhost", "-u hugo", "-w ~/api_pwd", "-l layer1", "-c/home/tim/tmp/blb_mgm.cfg.anon"]
        },
```