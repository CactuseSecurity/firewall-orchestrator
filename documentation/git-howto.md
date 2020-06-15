
<pre>

hod@fworch-dev:~/firewall-orchestrator$ git pull
Already up to date.
12:36
Kann keine Aktion in Richtung  CactuseSecurity/firewall-orchestrator Repo  erkennen.
Fork bleibt auf dem alten Stand und der Clone vom Fork auch
12:38
Habe es nun so gemacht und es funktioniert:https://help.github.com/en/github/collaborating-with-issues-and-pull-requests/syncing-a-fork
12:58
git remote -v
git remote add upstream https://github.com/CactuseSecurity/firewall-orchestrator.git
git remote -v
git fetch upstream
git checkout master
git merge upstream/master
git push
13:00
Und beim nächsten Mal nur noch:
git fetch upstream
git checkout master
git merge upstream/master
git push
13:03
besser wäre natürlich
git remote add upstream ssh://github.com/CactuseSecurity/firewall-orchestrator.git
13:12
umbenennen mit git remote set-url upstream ssh://github.com/CactuseSecurity/firewall-orchestrator.git
</pre>
