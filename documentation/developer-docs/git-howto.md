# git(hub) howto

It is recommended to create a personal fork and work on that, except you only make changes on documentation (but no code change). Just use the Fork button on the GitHub UI.

From that fork you can create local clones.

It is possible to sync your fork via the GitHub UI, but it leads at least to an ugly additional commit in your fork history: <https://rick.cogley.info/post/update-your-forked-repository-directly-on-github/>

So better use the command line:

Source: <https://help.github.com/en/github/collaborating-with-issues-and-pull-requests/syncing-a-fork>

Add ssh key to profile (Profile - Settings - ssh keys)

## add upstream URL (only has to be done once)

    git remote add upstream https://github.com/CactuseSecurity/firewall-orchestrator.git

### Sync with upstream

       git fetch upstream
       git checkout master

       (if there are already commits on local:)
       git merge upstream/master
       git push

       or shorter instead of fetch+checkout+merge:

       git pull upstream master

       (if there is a new commit because merge was necessary:)
       git push

### Working via ssh

    git remote add upstream ssh://github.com:CactuseSecurity/firewall-orchestrator.git

### Change upstream name
    git remote set-url upstream ssh://github.com:CactuseSecurity/firewall-orchestrator.git

## Example with non-master branch

       git clone git@github.com:tpurschke/firewall-orchestrator.git -b tim/make-api-reinstallable
       cd firewall-orchestrator/
       git remote add upstream git@github.com:CactuseSecurity/firewall-orchestrator.git
       git fetch upstream
       git checkout tim/make-api-reinstallable
       git merge upstream/tim/make-api-reinstallable
       git push

## Example: merge with conflicts

How to merge fork tpurschke/master into CactuseSecurity/master

1. get fork to merge

       git clone git@github.com:tpurschke/firewall-orchestrator.git -b master

   if you need to acces a "foreign" fork where you do not have access via ssh, use something like:

       git clone https://github.com/dos-box/firewall-orchestrator.git

2. change into repo and check out the correct branch or commit via its hash

       cd firewall-orchestrator
       a) git checkout b77e63e6e4e315164029ff20d2096ba75fd150d2
       b) git checkout testbranch123
       c) git checkout master

3. add remote upstream repo

       git remote add upstream https://github.com/CactuseSecurity/firewall-orchestrator.git
       git fetch upstream

4. merge

       git merge upstream/master

    results in output:

       Auto-merging roles/database/tasks/iso-setup-database-as-postgres-user.yml
       CONFLICT (content): Merge conflict in roles/database/tasks/iso-setup-database-as-postgres-user.yml
       Automatic merge failed; fix conflicts and then commit the result.

5. make manual changes, eg.

       vi roles/database/tasks/iso-setup-database-as-postgres-user.yml

6. submit changes

       git commit --all
       git push

7. Finally merge repos (now without conflicts) via github web ui

## Working with an additional branch
1. create branch in main repo cactus via github WebUI
2. go into local repo linked to both upsteam cactus and own fork and check links:

        tim@acantha:~/VisualStudioCodeProjects/fwo-tpurschke/firewall-orchestrator$ git remote -v
        origin git@github.com:tpurschke/firewall-orchestrator.git (fetch)
        origin git@github.com:tpurschke/firewall-orchestrator.git (push)
        upstream-cactus git@github.com:CactuseSecurity/firewall-orchestrator.git (fetch)
        upstream-cactus git@github.com:CactuseSecurity/firewall-orchestrator.git (push)
        tim@acantha:~/VisualStudioCodeProjects/fwo-tpurschke/firewall-orchestrator

3. fetch new branch into local repo:

        git fetch upstream-cactus

4. checkout new branch

        git checkout -b auth_frontend

5. push new branch to fork

        git push -u origin auth_frontend

## Submodules

### Automatic submodule sync via repo hooks
Enable the repo-managed hooks once (per clone) to keep submodules up to date automatically:
```shell
git config core.hooksPath .githooks
```
The hooks run after `git pull`, `git checkout`, and `git rebase` and initialize and update the submodules.
Notes:
- The hook is quiet if you do not have access to a submodule repository (no error output).
- The hook checks out the configured submodule branch from `.gitmodules` before updating, to avoid detached HEAD.
- This intentionally moves submodules to the newest commit on their configured branch, even if the superproject has not updated the pointer yet. Expect the submodule to appear "modified" in `git status`, unless you follow the next subsections advice.

### Avoid Advancing the Submodule Pointer
On the upstream we automatically advance the submodule pointer via automated pull requests.
To prevent merge conflicts and unintended divergence from upstream, **do not commit or push local changes that advance the submodule reference (commit pointer)** in this repository. This might happen if you directly commit or stage all files. It should not happen if you use explicit staging of your files in vscode. 

To automatically ignore local submodule pointer changes, run:
```shell
git config submodule.agents.ignore all
```

### Trigger hook 
In order to initially trigger the hook which does the initialisation, we need to do any of the operations (git checkout, git merge, git rewrite)

Here we do a simple checkout of another branch (assuming you are on main branch)

       git checkout develop

Now you should see the submodule in your IDE.

### Manual submodule operations (not necessary when using .githooks)
If you like to manually execute the submodule setup, see the sections below. Otherwise, please refer to the sections above.

#### Initial update (not necessary when using .githooks)
Update submodules to the commits recorded in the superproject (safe, reproducible). Initializes them if necessary.
Execute this command after the initial clone of the fwo repo in the fwo repo root directory:
```shell
git submodule update --init --recursive
```

#### Update agents repo manually (not necessary when using .githooks)
This updates the agents repo manually. Update submodules to the latest commit on their configured remote tracking branch. Execute this command to get the newest version of all submodules from their respective repositories.
```shell
git -C .agents checkout main
git submodule update --remote --merge --recursive
```

### Check correct file state
```shell
tim@acantha24:~/dev/tim/fwo$ git ls-tree HEAD .agents
160000 commit 73cfbb4efad58dd569c0c0ab4d7ecebc63d23ddd  .agents
tim@acantha24:~/dev/tim/fwo$ git ls-tree HEAD AGENTS.md
120000 blob 95e38a6a9ddf012aae10a06a19b6d8c1a65ec8b8    AGENTS.md
tim@acantha24:~/dev/tim/fwo$ 
```
Notes:
- 160000 - sub module
- 120000 - symbolic link
