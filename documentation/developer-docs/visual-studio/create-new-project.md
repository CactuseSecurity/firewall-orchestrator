# add a new project to the solution
        
- create new project "FWO.ApiConfig"
```code
  cd ~/dev/tpur-fwo/firewall-orchestrator/roles/lib/files
  mkdir FWO.ApiConfig
  cd FWO.ApiConfig
  dotnet new classlib -f netcoreapp3.1
```
- add project to solution file
```code
  cd ~/dev/tpur-fwo/firewall-orchestrator/roles
  dotnet sln FWO.sln add lib/files/FWO.ApiConfig
```
