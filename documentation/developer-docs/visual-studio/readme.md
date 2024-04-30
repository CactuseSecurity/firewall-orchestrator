# Using Visual Studio (Code) for development

Useful for Blazor/C# testing. Works on Windows, MacOS and Linux.

## Installation VSCODE

- Install [.NET Core SDK](https://docs.microsoft.com/en-us/dotnet/core/install/linux-ubuntu#1804-)
- on Linux: install mono-complete (avoids error "System.TypeLoadException: Could not load type of field 'McMaster.Extensions.CommandLineUtils.CommandLineApplication")

      sudo apt install mono-complete
- Install [Visual Studio Code](https://code.visualstudio.com/Download) for Linux or Windows
 
## Add VS Code Extensions from Marketplace
recommended:
- C#
- Razor+
- Debugger for Firefox
- GitHub Pull Requests and Issues, see <https://marketplace.visualstudio.com/items?itemName=GitHub.vscode-pull-request-github>
- GitLens
- GraphQL for VSCode
- Perl
- Simple Perl
- Prettier-Code formatter
- Python

Testing:
- NXunit Test Explorer
- .NET Core Test Explorer
- Test Explorer UI

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
- finally: if references are not foud - simply run the project - this might solve dependencies  


### install nuget packages

    tim@acantha:~/dev/tpur-fwo/firewall-orchestrator/roles/lib/files/FWO.Middleware.Client$ dotnet add package System.IdentityModel.Tokens.Jwt

### add local project reference

    tim@acantha:~/dev/tpur-fwo/firewall-orchestrator/roles/auth/files/FWO_Auth_Server$ dotnet add reference ../../../lib/files/FWO_API_Client/FWO_API_Client.csproj
   
   
or alternatively add the following to csproj file:

    <ItemGroup>
        <ProjectReference Include="../../../lib/files/FWO_API_Client/FWO_API_Client.csproj"/>
    </ItemGroup>

## add a new project to the solution
        
### create new project "FWO.ApiConfig"
```code
  cd ~/dev/tpur-fwo/firewall-orchestrator/roles/lib/files
  mkdir FWO.ApiConfig
  cd FWO.ApiConfig
  dotnet new classlib -f netcoreapp3.1
```
### add project to solution file
```code
  cd ~/dev/tpur-fwo/firewall-orchestrator/roles
  dotnet sln FWO.sln add lib/files/FWO.ApiConfig
```

### add necessary references in project file

    cat ~/dev/tpur-fwo/firewall-orchestrator/roles/lib/files/FWO.ApiConfig/FWO.ApiConfig.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.IdentityModel.Tokens" Version="6.7.1" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\FWO.Middleware.Client\FWO.Middleware.Client.csproj" />
    <ProjectReference Include="..\FWO_API_Client\FWO.ApiClient.csproj" />
    <ProjectReference Include="..\FWO_Config\FWO.Config.csproj" />
    <ProjectReference Include="..\FWO_Logging\FWO.Logging.csproj" />
  </ItemGroup>

</Project>
```
