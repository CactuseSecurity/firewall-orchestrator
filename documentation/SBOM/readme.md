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

## list of deb packages
    acl
    ansible
    apache2
    apt-transport-https
    ca-certificates
    curl
    docker-ce
    docker-ce-cli
    containerd.io
    dotnet-runtime
    dotnet-sdk
    fonts-liberation
    glibc-langpack-en
    gnupg2
    ldap-utils
    libapache2-mod-wsgi-py3
    libasound2
    libldap2-dev
    libpangoft2
    libpq-dev    
    libpq5
    libpython3-dev
    libappindicator3-1
    libatk-bridge2.0-0
    libatk1.0-0
    libcups2
    libdbus-1-3
    libdrm2
    libgbm1
    libnspr4
    libnss3
    libssl-dev
    libx11-xcb1
    libxcomposite1
    libxdamage1
    libxrandr2
    logrotate
    openssh-client
    openssh-server
    openssl
    perl
    postgresql
    postgresql-client
    python3-cryptography
    python3-docker
    python3-pip
    python3-psycopg2
    python3-openssl
    python3-dev
    libldap2-dev
    python3-pyldap
    python3-setuptools
    python3-venv
    rsync
    rsyslog
    slapd
    xdg-utils


## list of perl packages

    libdbi-perl

    
        
          
    

        
        Expand All
    
    @@ -31,9 +91,13 @@ we are using cycloneDx
  
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


## list of python packages with venv (importer module)
    pydantic>=2.0,<3.0
    jsonpickle>=3.0
    gnupg>=0.5
    pytest>=7.0
    graphql-core>=3.0
    requests>=2.0
    cryptography>=40.0
    netaddr>=1.0
    urllib3>=2.0
