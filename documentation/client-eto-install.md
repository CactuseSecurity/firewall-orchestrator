# Client - Eto.Forms
The client runs on Windows / Linux / MacOS

## Compile on development machine (same OS as target platform)
1) Clone the repository from GitHub https://github.com/CactuseSecurity/firewall-orchestrator.git (Windows: unzip)

2) Download and Install .Net Core SDK (https://dotnet.microsoft.com/download)
   - just click download, selection for your platform should be automatic???
   - start installer

3) Open Command Line / terminal 

4) Navigate/cd to 
   - Windows: 
   
         firewall-orchestrator\client\eto.forms\Firewall-Orchestrator.Wpf
   - MacOs:
   
         firewall-orchestrator/client/eto.forms/Firewall-Orchestrator.Mac
   - Linux:
   
         firewall-orchestrator/client/eto.forms/Firewall-Orchestrator.Gtk
           
5) and start compilation 
   - Windows: 
   
         dotnet publish --configuration Release --output publish --self-contained false --runtime win-x64 --framework netcoreapp3.1
   - MacOs: 
   
         dotnet publish --configuration Release --output publish --self-contained false --runtime MacOs --framework netcoreapp3.1
   - Linux: 
   
         dotnet publish --configuration Release --output publish --self-contained false --runtime linux-x64 --framework netcoreapp3.1
       
6) Your executable can now be found in
   - Windows:
   
         firewall-orchestrator/client/eto.forms/Firewall-Orchestrator.Ftk/bin/Release/netcoreapp3.1/win-x64
   - MacOs:
   
         firewall-orchestrator/client/eto.forms/Firewall-Orchestrator.???/bin/Release/netcoreapp3.1/win-x64
   - Linux:
   
         firewall-orchestrator/client/eto.forms/Firewall-Orchestrator.Gtk/bin/Release/netcoreapp3.1/linux-x64  

## Copy to and run on target client

1) On the target client: Download and Install .Net Core Runtime (https://dotnet.microsoft.com/download)

   (not necessary if you already installed .Net Core SDK)

2) Copy client from development system to target client

3) Execute client

       cd firewall-orchestrator-master/client/eto.forms/Firewall-Orchestrator.[Wpf|Mac|Gtk]/bin/Release/netcoreapp3.1/[win-x64|Mac|linux-x64]
       dotnet Firewall-Orchestrator.[Gtk].dll
