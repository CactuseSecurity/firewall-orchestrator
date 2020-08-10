
# Check Point R8x API enhancement ideas

a) increase api access speed
```console
[--sync] {true|false}
    Synchronous execution of task - commands that generate the task will wait until the task is finished.
    Default {true}
    Environment variable: MGMT_CLI_SYNC
```
b) add 2FA
```console
        mgmt_cli login --client-cert path-to-certificate-file.p12 password secret
```
c) Get OS information from CP gateway via API, see sk143612