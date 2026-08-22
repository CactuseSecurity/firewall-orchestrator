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

### Before you pull: back up an existing local `.claude/` directory

The repo tracks `.claude` as a symbolic link to the `.agents` submodule. If you already
have your own `.claude/` **directory** in your working copy, git silently deletes it and
everything inside it the first time you pull that change - the pull reports success and
prints no warning. This happens because `.gitignore` lists `**/.claude/`, and git treats
an ignored directory as disposable when a tracked path needs that name.

Affected are your local Claude Code permission allowlist (`settings.local.json`) and any
local skills, commands or agents you kept there. Move the directory aside **before**
pulling:
```shell
mv .claude ~/claude-backup-$(date +%F)
git pull
```

Afterwards there is nothing more to do: local state written through the link lands in the
`.agents` submodule, whose `.gitignore` already excludes `settings.local.json`,
`.credentials.json`, `shell-snapshots/` and `todos/`. Only a directory that predates the
symlink is at risk.

### Windows only: enable symbolic links before cloning

Only relevant on Windows. On Linux, macOS and WSL2 there is nothing to do.

This is not limited to agent tooling. Besides `.claude`, `AGENTS.md` and `CLAUDE.md`,
the repo links `roles/lib/files/FWO.Api.Client/Queries/GraphQL` to the shared
GraphQL query sources, so a checkout without symlink support breaks the build too.

Two things are needed, and both have to be in place **before cloning**, because
`core.symlinks` is evaluated at checkout time:

1. Allow Windows to create symbolic links: enable **Developer Mode**
   (Settings > System > For developers), or run git elevated. Without this privilege
   Windows refuses to create them no matter how git is configured.
2. Tell git to use them:
```shell
git config --global core.symlinks true
```
   The Git for Windows installer offers the same thing as the
   "Enable symbolic links" checkbox.

If the repo was already cloned without symlink support, the links exist as small text
files containing their target path. Repair them without re-cloning:
```shell
git config core.symlinks true
git submodule update --init --recursive
git checkout --force HEAD -- .claude AGENTS.md CLAUDE.md roles/lib/files/FWO.Api.Client/Queries/GraphQL
```

The `submodule update` step is not optional: `.claude`, `AGENTS.md` and `CLAUDE.md` all
point into `.agents`. On a clone made without `--recurse-submodules` that directory is
empty, so restoring the links alone leaves all three dangling while the command looks
like it succeeded.

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

Note that `.agents` is not just documentation. Through the repo-root `.claude` symlink its
contents are loaded automatically as project configuration by agent tooling, from a
separate repository. Should the submodule ever gain a `settings.json` defining hooks, a
pointer bump would run those commands on every developer machine that pulls it. Review
pointer bumps as you would review code, not as a routine sync.

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

List every symbolic link the repo tracks:
```shell
git ls-files -s | awk '$1=="120000"'
```
This tells you which paths are *supposed* to be links, but it cannot tell you whether they
were checked out as links. `git ls-files` reads the index, and with `core.symlinks=false`
git keeps the index entry at `120000` while writing a plain text file into the working
tree - so a broken checkout still reports `120000` here.

To check what is actually on disk, verify for each of them that it really is a link and
that its target resolves:
```shell
git ls-files -s | awk '$1=="120000"{print $4}' | while read -r f; do
  [ -L "$f" ] || echo "NOT A LINK: $f"
  [ -e "$f" ] || echo "DANGLING:   $f"
done
```
Empty output means all links are intact. Otherwise:

- `NOT A LINK` - the path was checked out as a plain file instead of a link. See the
  Windows section above.
- `DANGLING` - the link itself is fine, but its target is missing. For `.claude`,
  `AGENTS.md` and `CLAUDE.md` this means the `.agents` submodule was never initialised;
  fix it with `git submodule update --init --recursive`.

Do not use a typechange check (`git status --porcelain | awk '$1=="T"'`) on its own: it
finds only the first case. A clone made without `--recurse-submodules` contains real
symlinks pointing into an empty `.agents`, so it reports nothing at all while `AGENTS.md`
and `CLAUDE.md` dangle and `.claude` resolves onto an empty directory.

## Optional: agent-supported work

### Linux: install and authenticate the gh command line tool (used by agents)
```shell
sudo apt install gh
gh auth login
```

This works on current Ubuntu/Debian but not on older LTS releases, where GitHub's own apt repository is required.

## Troubleshooting: pre-commit Python / Ruff / Pyright

If `git commit` fails with messages like:

```text
Warning: No virtual environment found. Assuming python (with ruff and pyright) is available in base environment.
Error: 'python -m ruff' is not available. Please install ruff in the active environment.
```

### 1. What was the error?

The repository-managed pre-commit hook (`.githooks/pre-commit`) failed before creating the commit.
The hook requires `python`, `ruff`, and `pyright` to be available in the active environment.

### 2. Why does it occur?

This typically happens when:

- no local `.venv` is present or active, and no conda env is active,
- `python` points to a different interpreter than expected,
- required tooling is missing from the active interpreter,
- project Python dependencies are not installed yet.

In this repo, `core.hooksPath` is set to `.githooks`, so these checks run automatically on commit.
The hook prefers the repository-local virtual environment at `.venv` when it exists.
That means fixing a global Python installation often does nothing for the commit hook.

### 3. Fast fix for Ruff version mismatch

If the error looks like this:

```text
Error: Ruff version mismatch.
Installed ruff is 0.15.21, but pyproject.toml requires 0.16.0.
```

fix the repo-local `.venv`, not the global Python installation.

From the repo root:

```powershell
.\.venv\Scripts\python.exe -m pip install --upgrade ruff==0.16.0
.\.venv\Scripts\python.exe -m ruff check --fix
.\.venv\Scripts\python.exe -m ruff format
```

Then retry:

```powershell
git commit
```

Why this works:

- the pre-commit hook checks `.venv` first,
- `pyproject.toml` currently requires `ruff==0.16.0`,
- `roles/importer/files/importer/requirements.txt` currently pins `ruff==0.16.0`.

Quick verification:

```powershell
.\.venv\Scripts\python.exe -m ruff --version
```

### 4. If `.venv` does not exist yet

Create it once from the repo root:

```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -U pip
.\.venv\Scripts\python.exe -m pip install -r roles\importer\files\importer\requirements.txt -r scripts\requirements.txt
```

Then rerun the Ruff commands from the previous section.

### 5. Full setup if Python tooling is missing

Recommended setup (repo root):

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -U pip
python -m pip install -r roles\importer\files\importer\requirements.txt -r scripts\requirements.txt
```

Optional validation:

```powershell
python -m ruff --version
python -m pyright --version
```

Then retry commit.

Notes:

- `ruff` is pinned in this repo (`ruff==0.15.0`).

Temporary bypass only if absolutely necessary:

```powershell
git commit --no-verify
```
