# How to make the install process run faster

## install packages on your master tst machine

    cd firewall-organizer; ansible-playbook -i inventory bin/preinstall-packages.yml -K

cat bin/preinstall-packages.yml

    sudo apt install apache2 apt-transport-https dotnet-sdk-
