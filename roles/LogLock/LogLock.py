import subprocess

path = "C:/Users/Nils/Documents/GitHub/firewall-orchestrator/roles/ui/files/FWO.UI/abc.log"

print("Requesting access...")
subprocess.Popen("acquire_lock.py " + path, shell=True)
print("... access granted.")
print("Releasing access...")
subprocess.Popen("release_lock.py " + path, shell=True)
print("... access released.")
