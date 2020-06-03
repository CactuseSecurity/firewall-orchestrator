# Client - Eto.Forms
The client runs on Windows / Linux / MacOS

Tested with 
  - Windows 10
  - MacOs 10.15
  - Ubuntu 20.04 (Tim, 2020-05-31)

## Compile on development machine (same OS as target platform)
1) Clone the repository from GitHub https://github.com/CactuseSecurity/firewall-orchestrator.git (Windows: unzip)

2) Download and Install **.NET Core SDK** (https://dotnet.microsoft.com/download)
   - Windows:
          
          Click "Download .NET Core SDK"
   
   - MacOS:
          
          Click "Download .NET Core SDK"
          
   - Linux:
          
          1. Click "Install .NET Core"  
          2. Select your Linux packet manager
          3. Follow instructions for paragraph "Install .NET Core SDK"

3) Open Command Line / Terminal 

4) Navigate to 
   - Windows: 
   
         firewall-orchestrator\client\eto.forms\Firewall-Orchestrator.Wpf
   - MacOS:
   
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
   
         firewall-orchestrator/client/eto.forms/Firewall-Orchestrator.Wpf/publish
   - MacOs:
   
         firewall-orchestrator/client/eto.forms/Firewall-Orchestrator.Mac/publish
   - Linux:
   
         firewall-orchestrator/client/eto.forms/Firewall-Orchestrator.Gtk/publish
         
7) You may delete everything but the publish folder now

## Copy to and run on target client

1) On the target client: Download and Install .Net Core Runtime (https://dotnet.microsoft.com/download)

   (not necessary if you already installed .Net Core SDK)

2) Copy client from development system to target client

3) Do the following while in client folder to start client:

   - Windows:
   
       Execute execetuable
   - MacOs:
   
       Execute execetuable  
   - Linux:
   
       dotnet Firewall-Orchestrator.Gtk.dll
