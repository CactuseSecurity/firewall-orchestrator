### Client - Eto.Forms
The client runs on Windows / Linux / MacOS

#### Compile
1) Clone the repository from GitHub
2) Unzip it

##### Windows

3) Download and Install .Net Core SDK (https://dotnet.microsoft.com/download)

4) Open Windows Command Line 

5) Navigate to .../firewall-orchestrator-master/client/eto.forms/Firewall-Orchestrator.Wpf/ via
       
       cd .../firewall-orchestrator-master/client/eto.forms/Firewall-Orchestrator.Wpf/
       
6) Run
       
       dotnet publish --configuration Release --output publish --self-contained false --runtime win-x64 --framework netcoreapp3.1
       
7) Your executable can now be found in .../firewall-orchestrator-master/client/eto.forms/Firewall-Orchestrator.Ftk/bin/Release/netcoreapp3.1/win-x64       
       
##### Linux

3) Download and Install .Net Core SDK following the installation instructions for your Linux system (https://dotnet.microsoft.com/download)

3.1) On some few Linux based operating systems you might need to install GTK

4) Open your Terminal

5) Navigate to .../firewall-orchestrator-master/client/eto.forms/Firewall-Orchestrator.Gtk/ via

       cd .../firewall-orchestrator-master/client/eto.forms/Firewall-Orchestrator.Gtk/       

6) Run 

       dotnet publish --configuration Release --output publish --self-contained false --runtime linux-x64 --framework netcoreapp3.1
       
7) Your executable can now be found in .../firewall-orchestrator-master/client/eto.forms/Firewall-Orchestrator.Gtk/bin/Release/netcoreapp3.1/linux-x64  

##### MacOS

TO BE FILLED

#### Run

##### Windows

1) Download and Install .Net Core Runtime (https://dotnet.microsoft.com/download)
Not needed if you already installed .Net Core SDK

2) Execute the executable found in .../firewall-orchestrator-master/client/eto.forms/Firewall-Orchestrator.Wpf/bin/Release/netcoreapp3.1/win-x64   

##### Linux

1) Download and Install .Net Core Runtime following the installation instructions for your Linux system (https://dotnet.microsoft.com/download)
Not needed if you already installed .Net Core SDK

1.1) On some few Linux based operating systems you might need to install GTK

2) Open your Terminal

3) Navigate to .../firewall-orchestrator-master/client/eto.forms/Firewall-Orchestrator.Gtk/Release/netcoreapp3.1/linux-x64 via

       cd .../firewall-orchestrator-master/client/eto.forms/Firewall-Orchestrator.Gtk/Release/netcoreapp3.1/linux-x64    

4) Execute the executable found in .../firewall-orchestrator-master/client/eto.forms/Firewall-Orchestrator.Gtk/bin/Release/netcoreapp3.1/linux-x64 via
       
       dotnet Firewall-Orchestrator.Gtk.dll
              
##### MacOS

TO BE FILLED
