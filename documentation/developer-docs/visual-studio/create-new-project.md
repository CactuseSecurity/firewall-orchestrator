# add a new project to the solution
        
## create new project "FWO.ApiConfig"
```code
  cd ~/dev/tpur-fwo/firewall-orchestrator/roles/lib/files
  mkdir FWO.ApiConfig
  cd FWO.ApiConfig
  dotnet new classlib -f netcoreapp3.1
```
## add project to solution file
```code
  cd ~/dev/tpur-fwo/firewall-orchestrator/roles
  dotnet sln FWO.sln add lib/files/FWO.ApiConfig
```

## add necessary references in project file

    cat ~/dev/tpur-fwo/firewall-orchestrator/roles/lib/files/FWO.ApiConfig/FWO.ApiConfig.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netcoreapp3.1</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.IdentityModel.Tokens" Version="6.7.1" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\FWO_Auth_Client\FWO.Auth.Client.csproj" />
    <ProjectReference Include="..\FWO_API_Client\FWO.ApiClient.csproj" />
    <ProjectReference Include="..\FWO_Config\FWO.Config.csproj" />
    <ProjectReference Include="..\FWO_Logging\FWO.Logging.csproj" />
  </ItemGroup>

</Project>
```
