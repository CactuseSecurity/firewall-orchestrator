It is recommended to create a personal fork and work on that, except you only make changes on documentation (but no code change). Just use the Fork button on the GitHub UI.

From that fork you can create local clones.

It is possible to sync your fork via the GitHub UI, but it leads at least to an ugly additional commit in your fork history: <https://rick.cogley.info/post/update-your-forked-repository-directly-on-github/>

So better use the command line:

Source: <https://help.github.com/en/github/collaborating-with-issues-and-pull-requests/syncing-a-fork>

Add ssh key to profile (Profile - Settings - ssh keys)

# First time add upstream URL (only has to be done once)

```
git remote -v
git remote add upstream https://github.com/CactuseSecurity/firewall-orchestrator.git
git remote -v
```

# Sync with upstream

```
git fetch upstream
git checkout master

(if there are already commits on local:)
git merge upstream/master
git push

or shorter instead of fetch+checkout+merge:

git pull upstream master

(if there is a new commit because merge was necessary:)
git push
```

# Working via ssh

```
git remote add upstream ssh://github.com/CactuseSecurity/firewall-orchestrator.git
```

# Change upstream name

```
git remote set-url upstream ssh://github.com/CactuseSecurity/firewall-orchestrator.git
```

# Example with non-master branch

```
git clone git@github.com:tpurschke/firewall-orchestrator.git -b tim/make-api-reinstallable
cd firewall-orchestrator/
git remote add upstream git@github.com:CactuseSecurity/firewall-orchestrator.git
git fetch upstream
git checkout tim/make-api-reinstallable
git merge upstream/tim/make-api-reinstallable
git push
```
