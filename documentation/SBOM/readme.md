# creating SBOM

we are using cycloneDx

## standard script 

    wget https://github.com/CycloneDX/cyclonedx-cli/releases/download/v0.27.2/cyclonedx-linux-x64
    sudo mv cyclonedx-linux-x64 /usr/local/bin/cyclonedx
    sudo chmod 755 /usr/local/bin/cyclonedx

## script for C#

    dotnet tool install --global CycloneDX
    cd fwo-cactus
    git pull
    dotnet-CycloneDX -j roles/FWO.sln 
