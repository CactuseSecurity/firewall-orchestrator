# Using Visual Studio (Code) for development

Useful for Blazor/C# testing. Works on Windows, MacOS and Linux.

## Installation

- Install [.NET Core SDK](https://docs.microsoft.com/en-us/dotnet/core/install/linux-ubuntu#1804-)
- Install [Visual Studio Code](https://code.visualstudio.com/Download) for Linux or Windows
- Add the following extensions via File - Preferences - Extensions:
  - GitHub Pull Requests and Issues, see <https://marketplace.visualstudio.com/items?itemName=GitHub.vscode-pull-request-github>
  - C#
  
## Clone project
Clone your own fork, eg.

    git clone 

## Add upstream cactus repo (needs to be done on command line)

change into the workspace directory on command line and run
~~~console
git remote add upstream-cactus git@github.com:CactuseSecurity/firewall-orchestrator.git
~~~
Use the followin command to verify settings:
~~~console
tim@acantha:~/VisualStudioCodeProjects/fwo-tpurschke/firewall-orchestrator$ git remote -v
origin	git@github.com:tpurschke/firewall-orchestrator.git (fetch)
origin	git@github.com:tpurschke/firewall-orchestrator.git (push)
upstream-cactus	git@github.com:CactuseSecurity/firewall-orchestrator.git (fetch)
upstream-cactus	git@github.com:CactuseSecurity/firewall-orchestrator.git (push)
tim@acantha:~/VisualStudioCodeProjects/fwo-tpurschke/firewall-orchestrator$ 
~~~

### sync fork

## Select, prepare and run project
