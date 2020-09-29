# Using Visual Studio (Code) for development

Useful for Blazor/C# testing. Works on Windows, MacOS and Linux.

## Installation VSCODE

- Install [.NET Core SDK](https://docs.microsoft.com/en-us/dotnet/core/install/linux-ubuntu#1804-)
- Install [Visual Studio Code](https://code.visualstudio.com/Download) for Linux or Windows
- Add the following extensions via File - Preferences - Extensions:
  - GitHub Pull Requests and Issues, see <https://marketplace.visualstudio.com/items?itemName=GitHub.vscode-pull-request-github>
  - C#
  
## Configure global git settings on client (first time git only)

    git config --global user.name "John Doe"
    git config --global user.email johndoe@example.com

## Clone project
Clone your own fork, eg.

    git clone 

## Add upstream cactus repo
(starting with vscode 1.49 this can now also be done in vscode UI)

change into the workspace directory on command line and run

    git remote add upstream-cactus git@github.com:CactuseSecurity/firewall-orchestrator.git

Use the following command to verify settings:

    tim@acantha:~/VisualStudioCodeProjects/fwo-tpurschke/firewall-orchestrator$ git remote -v
    origin git@github.com:tpurschke/firewall-orchestrator.git (fetch)
    origin git@github.com:tpurschke/firewall-orchestrator.git (push)
    upstream-cactus git@github.com:CactuseSecurity/firewall-orchestrator.git (fetch)
    upstream-cactus git@github.com:CactuseSecurity/firewall-orchestrator.git (push)
    tim@acantha:~/VisualStudioCodeProjects/fwo-tpurschke/firewall-orchestrator$ 

if vs code does not show the upstream repo in pull-from, just give it some time to show up?!

## Select, prepare and run project

## repair references and intellisense / refresh 

- Press CTRL - SHIFT - P to "Show Command Palette" and type Reload Window to update all references
  - wait 3 secs to allow for refresh of all references
  - +run restore if offered in the right bottom corner
- if in a .cs file no references for objects are shown, just start editing (changing) it and the references will appear
- might also help: Go to File - Preferences - Settings
  - search for omnisharp (in extension c# configuration) and enter the name of the missing / not working solution (project) into the "Omnisharp: Default Launch Solution" form, e.g. FWO.sln or FWO_Auth.sln

### install nuget packages

    tim@acantha:~/dev/tpur-fwo/firewall-orchestrator/roles/lib/files/FWO_Auth_Client$ dotnet add package System.IdentityModel.Tokens.Jwt

### add local project reference

    tim@acantha:~/dev/tpur-fwo/firewall-orchestrator/roles/auth/files/FWO_Auth_Server$ dotnet add reference ../../../lib/files/FWO_API_Client/FWO_API_Client.csproj
   
   
or alternatively add the following to csproj file:

    <ItemGroup>
        <ProjectReference Include="../../../lib/files/FWO_API_Client/FWO_API_Client.csproj"/>
    </ItemGroup>

   
### Create proper configuration in visual studio code
```console
tim@acantha:~$ cat /home/tim/dev/tpurschke-fwo/firewall-orchestrator/.vscode/launch.json 
{
   "version": "0.2.0",
   "configurations": [
        {
            "name": "Blazor (web)",
            "type": "coreclr",
            "request": "launch",
            "preLaunchTask": "build_FWO",
            "program": "${workspaceFolder}/roles/frontend/files/Blazor/FWO/FWO/bin/Debug/netcoreapp3.1/FWO.dll",
            "args": [],
            "cwd": "${workspaceFolder}/roles/frontend/files/Blazor/FWO/FWO",
            "stopAtEntry": false,
            "serverReadyAction": {
                "action": "openExternally",
                "pattern": "\\bNow listening on:\\s+(https?://\\S+)"
            },
            "env": {
                "ASPNETCORE_ENVIRONMENT": "Development"
            },
            "sourceFileMap": {
                "/Views": "${workspaceFolder}/Views"
            }
        },
        {
            "name": ".NET Core Attach ",
            "type": "coreclr",
            "request": "attach",
            "processId": "${command:pickProcess}"
        },
        {
            "name": "AuthServer",
            "type": "coreclr",
            "request": "launch",
            "preLaunchTask": "build",
            // If you have changed target frameworks, make sure to update the program path.
            "program": "${workspaceFolder}/roles/auth/files/FWO_Auth/FWO_Auth_Server/bin/Debug/netcoreapp3.1/FWO_Auth_Server.dll",
            "args": [],
            "cwd": "${workspaceFolder}/roles/auth/files/FWO_Auth/FWO_Auth_Server",
            // For more information about the 'console' field, see https://aka.ms/VSCode-CS-LaunchJson-Console
            "console": "internalConsole",
            "stopAtEntry": false
        }
    ]
}

tim@acantha:~$ cat /home/tim/dev/tpurschke-fwo/firewall-orchestrator/.vscode/tasks.json 
{
    "version": "2.0.0",
    "tasks": [
        {
            "label": "build",
            "command": "dotnet",
            "type": "process",
            "args": [
                "build",
                "${workspaceFolder}/roles/auth/files/FWO_Auth/FWO_Auth_Server/FWO_Auth_Server.csproj",
                "/property:GenerateFullPaths=true",
                "/consoleloggerparameters:NoSummary"
            ],
            "problemMatcher": "$msCompile"
        },
        {
            "label": "publish",
            "command": "dotnet",
            "type": "process",
            "args": [
                "publish",
                "${workspaceFolder}/roles/auth/files/FWO_Auth/FWO_Auth_Server/FWO_Auth_Server.csproj",
                "/property:GenerateFullPaths=true",
                "/consoleloggerparameters:NoSummary"
            ],
            "problemMatcher": "$msCompile"
        },
        {
            "label": "build_FWO",
            "command": "dotnet",
            "type": "process",
            "args": [
                "build",
                "${workspaceFolder}/roles/frontend/files/Blazor/FWO/FWO/FWO.csproj",
                "/property:GenerateFullPaths=true",
                "/consoleloggerparameters:NoSummary"
            ],
            "problemMatcher": "$msCompile"
        },
        {
            "label": "watch",
            "command": "dotnet",
            "type": "process",
            "args": [
                "watch",
                "run",
                "${workspaceFolder}/roles/auth/files/FWO_Auth/FWO_Auth_Server/FWO_Auth_Server.csproj",
                "/property:GenerateFullPaths=true",
                "/consoleloggerparameters:NoSummary"
            ],
            "problemMatcher": "$msCompile"
        }
    ]
tim@acantha:~/dev/tpurschke-fwo/firewall-orchestrator$ 
```
## setting up central backend host
using devservffm as a central server for all services that are not debugged via visual studio (code)

- webhook receiver url: https://cactus.de:60344/fwo

### add local config on development client

    /etc/fworch/   (in Windows c:\etc\fworch\)
        fworch.yaml
        secrets/
          jwt_private_key.pem
          jwt_public_key.pem

### /etc/fworch/fworch.yaml
```yaml
fworch_home: "/usr/local/fworch"
dotnet_mode: "Release"

# api
api_uri: "https://127.0.0.1:9443/api/v1/graphql"
api_hasura_jwt_alg: "RS256"

# auth
auth_JWT_key_file: "/usr/local/fworch/etc/secrets/jwt_private_key.pem"
auth_hostname: "127.0.0.1"
auth_server_port: "8888"
```

### port forwarding per ssh

- central server (to be built): cactus.de
  - 9443:localhost:9443
  - 636:localhost:636


### local listeners on client

- 5001 - blazor
- 8888 - auth-server
