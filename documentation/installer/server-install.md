# Installation instructions server

- use latest debian or ubuntu minimal server with ssh service running (need to install and configure sudo for debian)
- currently recommended platform is Ubuntu Server 20.04 TLS
- We will install various software components to your system. It is recommended to do so on a dedicated (test) system.

1) prepare your test system (make sure your user has full sudo permissions)

```console
su -
apt-get install git ansible sudo
```
if not already configured, add your current user to sudo group (make sure to activate this change by starting new shell or even rebooting):

```console
usermod -a -G sudo `whoami`

exit

2) get Firewall Orchestrator with the following command

```console
git clone https://github.com/CactuseSecurity/firewall-orchestrator.git
```

3) if ansible version < 2.8 (older systems like ubuntu 18.04, debian 10), install latest ansible 

       cd firewall-orchestrator; ansible-playbook scripts/install-latest-ansible.yml -K

4) install (on localhost)

Otherwise, directly run:

```console
cd firewall-orchestrator; ansible-playbook site.yml -K
```
Enter sudo password when prompted "BECOME or SUDO password:"

That's it firewall-orchestrator is ready for usage. You will find the randomly generated login credentials printed out at the very end of the installation:
```
...
TASK [display secrets for this installation] ***********************************
ok: [fworch-srv] => {
    "msg": [
        "Your initial UI admin password is 'xxx'",
        "Your api hasura admin secret is 'yyy'"
    ]
}

PLAY RECAP *********************************************************************
fworch-srv                 : ok=302  changed=171  unreachable=0    failed=0    skipped=127  rescued=0    ignored=0
```
Simply navigate to <https://localhost/> and login with user 'admin' and the UI admin password.

The api hasura admin secret can be used to access the API at <https://localhost:9443/>.
