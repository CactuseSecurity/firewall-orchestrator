# How to make installer also run on Red Hat
## Installation Instructions Red Hat
- tested with RHEL 8.3
- download OS (in my case dvd iso with 9 GB) from <https://developers.redhat.com/products/rhel/download>
- create vbox with Linux Red Hat 64 bit 2048 MB RAM, 24 GB HDD
- install OS
  - install language: English US
  - switch keyboard to German (also move it up to become default)
  - time zone: Berlin
  - turn on networking
  - select destination hdd
  - installation source = local media
  - software selection (used Minimal Install plus Guest Agents & Standard)
  - add root pwd and new admin user
  - connect to red hat, but do not select "connect to red hat insight"
  - Begin Installation
## Further Thoughts on porting ansible code
- adding all parameters as variables
- main issue is to know the package names per distribution
- other issues include name and location of Linux Distribution dependent config files
- package updates/upgrades (apt update, apt upgrade) will have to be handled with when statements per OS (or give apt_rpm module a try which allows for updates)
- replace all apt module occurences with package module:
