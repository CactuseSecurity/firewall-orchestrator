#!/usr/bin/expect -f
# $Id: cisco-pix.sh,v 1.1.2.2 2007-12-13 10:47:32 tim Exp $
# $Source: /home/cvs/iso/package/importer/CACTUS/FWORCH/import/Attic/cisco-pix.sh,v $

set timeout 15
spawn /usr/bin/ssh -p 2222 admin@localhost
expect "password:$"
send "nunca2nunca\r\n"
expect "pixl>"
send "enable\r\n"   
expect "Password: $"
send "nunca2nunca\r\n"
expect "pixl#"
#send "show running\r\n" >/tmp/pix-config.out
log_file /tmp/pix-config.out
send "show running\r\n"
expect "pixl#"
send "exit\r\n"
expect "Logoff"
interact 
