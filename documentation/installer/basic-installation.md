# Installation instructions server

- use latest debian or ubuntu minimal server with ssh service running (need to install and configure sudo for debian)
- recommended platforms are Ubuntu Server 22.04 LTS and Debian 12. See [system requirements](https://fwo.cactus.de/wp-content/uploads/2021/07/fwo-system-requirements-v5.pdf) for supported platforms
- we will install various software components to your system. It is recommended to do so on a dedicated (test) system.

1) prepare your target system (make sure your user has full sudo permissions)

```console
su -
apt-get install git sudo ansible
```
if not already configured, add your current user to sudo group (make sure to activate this change by starting new shell or even rebooting):

```console
usermod -a -G sudo `whoami`
```

Also make sure your packages are up to date before FWORCH installation using e.g.

    sudo apt update && sudo apt upgrade

possibly followed by a reboot.

2) Getting Firewall Orchestrator

with the following command (as normal user)

```console
git clone https://github.com/CactuseSecurity/firewall-orchestrator.git
```

3) Ansible installation

Make sure you have ansible version 2.13 or above installed on your system (check with "ansible --version"). If this is not the case, install a newer ansible. One possible way is to run the four commands of the following script (and entering your sudo password) - run them separately if the script :
        cd firewall-orchestrator
        firewall-orchestrator/scripts/install-ansible-from-venv.sh

4) Firewall Orchestrator installation

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


If using the python venv method, you may now exit venv with:

        deactivate