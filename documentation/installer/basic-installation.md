# Installation instructions server

- use latest debian or ubuntu minimal server with ssh service running (need to install and configure sudo for debian)
- recommended platforms are Ubuntu Server 20.04 LTS and Debian 11. See [system requirements](https://fwo.cactus.de/wp-content/uploads/2021/07/fwo-system-requirements-v5.pdf) for supported platforms
- we will install various software components to your system. It is recommended to do so on a dedicated (test) system.

1) prepare your target system (make sure your user has full sudo permissions)

```console
su -
apt-get install git ansible sudo
```
if not already configured, add your current user to sudo group (make sure to activate this change by starting new shell or even rebooting):

```console
usermod -a -G sudo `whoami`
```

Also make sure your packages are up to date before FWORCH installation using e.g.

    sudo apt update && sudo apt upgrade

possibly followed by a reboot.


2) get Firewall Orchestrator with the following command (as normal user)
```console
git clone https://github.com/CactuseSecurity/firewall-orchestrator.git
```

3) Ansible Installation
  - Ubuntu 18.04, Debian 10 only: install latest ansible before firewall orchestrator installation

        cd firewall-orchestrator; ansible-playbook scripts/install-latest-ansible.yml -K

  - All platforms: install galaxy collections

        ansible-galaxy collection install community.postgresql

4) install (on localhost)

```console
cd firewall-orchestrator; ansible-playbook site.yml -K
```

Enter sudo password when prompted "BECOME or SUDO password:"

That's it. Firewall-orchestrator is ready for usage. You will find the randomly generated login credentials printed out at the very end of the installation:
```
...
TASK [display secrets for this installation] ***********************************
ok: [install-srv] => {
    "msg": [
        "Your initial UI admin password is 'xxx'",
        "Your api hasura admin secret is 'yyy'"
    ]
}

PLAY RECAP *********************************************************************
install-srv                 : ok=302  changed=171  unreachable=0    failed=0    skipped=127  rescued=0    ignored=0
```
Simply navigate to <https://localhost/> and login with user 'admin' and the UI admin password.

The api hasura admin secret can be used to access the API at <https://localhost:9443/>.
