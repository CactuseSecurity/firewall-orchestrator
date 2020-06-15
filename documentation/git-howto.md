Quelle: https://help.github.com/en/github/collaborating-with-issues-and-pull-requests/syncing-a-fork

Add ssh key to profile (Profile - Settings - ssh keys)

# First time add upstream URL (only has to be done once): 

    git remote -v
    git remote add upstream https://github.com/CactuseSecurity/firewall-orchestrator.git
    git remote -v
    
# Sync with upstream 

    git fetch upstream
    git checkout master
    git merge upstream/master
    git push


# Working via ssh

    git remote add upstream ssh://github.com/CactuseSecurity/firewall-orchestrator.git
    
# Change upstream name

    git remote set-url upstream ssh://github.com/CactuseSecurity/firewall-orchestrator.git

