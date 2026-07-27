# Basic server installation

Install Firewall Orchestrator on a dedicated Debian 12+ or Ubuntu 22.04+ server. Use an account that can run `sudo`. For proxy, upgrade, test, or other non-default setups, see the [advanced installation guide](install-advanced.md).

1. Install Git and clone the repository:

```console
sudo apt update
sudo apt install --yes git
git clone https://github.com/CactuseSecurity/firewall-orchestrator.git
cd firewall-orchestrator
```

2. Create and activate the installer Ansible environment:

```console
source scripts/install-ansible-from-venv.sh
```

The script installs `python3-venv` if needed, installs the supported Ansible version and required collections, and leaves the virtual environment active. It preserves any existing pip configuration.

3. Run the installer:

```console
./scripts/run-playbook-with-sudo.sh site.yml
```

Enter your sudo password if prompted. At the end of the installation, record the generated UI administrator password and sign in at <https://localhost/> as `admin`.

When finished, leave the installer virtual environment:

```console
deactivate
```
