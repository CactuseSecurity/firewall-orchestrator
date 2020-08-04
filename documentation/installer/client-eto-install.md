# Client - Eto.Forms

The client runs on Windows / Linux / MacOS

Tested with

- Windows 10
- MacOs 10.15
- Ubuntu 20.04 (Tim, 2020-05-31)

## Compile on development machine (same OS as target platform)

1) Clone the [repository from GitHub](https://github.com/CactuseSecurity/firewall-orchestrator.git) (Windows: unzip)

2) [Download and Install **.NET Core SDK**](https://dotnet.microsoft.com/download)

- Windows:

  ```console
  Click "Download .NET Core SDK"
  ```

- MacOS:

  ```console
  Click "Download .NET Core SDK"
  ```

- Linux:

  ```console
  1\. Click "Install .NET Core"
     2\. Select your Linux packet manager
     3\. Follow instructions for paragraph "Install .NET Core SDK"
  ```

3) Open Command Line / Terminal

4) Navigate to

- Windows:

  ```console
  firewall-orchestrator\client\eto.forms\Firewall-Orchestrator.Wpf
  ```

- MacOS:

  ```console
  firewall-orchestrator/client/eto.forms/Firewall-Orchestrator.Mac
  ```

- Linux:

  ```console
  firewall-orchestrator/client/eto.forms/Firewall-Orchestrator.Gtk
  ```

5) and start compilation

- Windows:

  ```console
  dotnet publish --configuration Release --output publish --self-contained false --runtime win-x64 --framework netcoreapp3.1
  ```

- MacOs:

  ```console
  dotnet publish --configuration Release --output publish --self-contained false --runtime osx-x64 --framework netcoreapp3.1
  ```

- Linux:

  ```console
  dotnet publish --configuration Release --output publish --self-contained false --runtime linux-x64 --framework netcoreapp3.1
  ```

6) Your executable can now be found in

- Windows:

  ```console
  firewall-orchestrator/client/eto.forms/Firewall-Orchestrator.Wpf/publish
  ```

- MacOs:

  ```console
  firewall-orchestrator/client/eto.forms/Firewall-Orchestrator.Mac/publish
  ```

- Linux:

  ```console
  firewall-orchestrator/client/eto.forms/Firewall-Orchestrator.Gtk/publish
  ```

7) You may delete everything but the publish folder now

## Copy to and run on target client

1) On the target client: [Download and Install .Net Core Runtime](https://dotnet.microsoft.com/download)

(not necessary if you already installed .Net Core SDK)

2) Copy client from development system to target client

3) Do the following while in client folder to start client:

- Windows:

  run executable

- MacOs:

  run executable

- Linux:

  dotnet Firewall-Orchestrator.Gtk.dll
