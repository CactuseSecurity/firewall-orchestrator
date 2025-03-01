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

## list of perl packages

    libdbi-perl
    libdbd-pg-perl
    libdate-calc-perl
    psmisc
    libnet-cidr-perl
    libsys-syslog-perl
    libexpect-perl
    libcgi-pm-perl
    python3-jsonpickle
    python3-gnupg
    python3-pytest
    python3-pydantic


## list of python packages

    python3-netaddr
    python3-jsonpickle
    python3-gnupg
    python3-pydantic
