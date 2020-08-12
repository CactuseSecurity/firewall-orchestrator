#!/bin/sh
# run as fworch
SERVER_LIST="127.0.0.1"
#SERVER_LIST="admsrv websrvffm chatsrv test3 opnsense11 dnssrvffm alpsrvtest daba nagios sting-gw isodev isoback2 spiegel admsrvda iso-test-importer klaut gware wasp howto itchy mailin proxy proxylan spike vpn-gateway wwwprivat"
for h in $SERVER_LIST; do
    ip=$(dig +search +short $h)
    #ssh-keygen -R $h
    #ssh-keygen -R "$ip"
    ssh-keyscan -H "$ip" >> ~/.ssh/known_hosts
    ssh-keyscan -H $h >> ~/.ssh/known_hosts
done
