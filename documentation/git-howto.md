Quelle: https://help.github.com/en/github/collaborating-with-issues-and-pull-requests/syncing-a-fork

    git remote -v
    git remote add upstream https://github.com/CactuseSecurity/firewall-orchestrator.git
    git remote -v
    git fetch upstream
    git checkout master
    git merge upstream/master
    git push

Und beim nächsten Mal nur noch:

    git fetch upstream
    git checkout master
    git merge upstream/master
    git push

besser wäre natürlich

    git remote add upstream ssh://github.com/CactuseSecurity/firewall-orchestrator.git
    
umbenennen mit 

    git remote set-url upstream ssh://github.com/CactuseSecurity/firewall-orchestrator.git

