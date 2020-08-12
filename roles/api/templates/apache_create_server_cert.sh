#!/bin/bash
SSLDIR=/etc/apache2/ssl
if [ ! -f $SSLDIR/server.key ]; then
        mkdir -p $SSLDIR
        KEY=$SSLDIR/server.key
        CERT=$SSLDIR/server.crt
        CommonName=$(hostname)
        subjectAltName={{ api_hostname }}
        organizationalUnitName=Test
        TOPLEVEL=DE
        ORG='Cactus eSecurity'
        LOCALITY=Frankfurt
        EMAIL=fworch@cactus.de
        SUBJ="
C=$TOPLEVEL
O=$ORG
localityName=$LOCALITY
commonName=$CommonName
organizationalUnitName=$organizationalUnitName
emailAddress=$EMAIL
"
		openssl req -nodes -newkey rsa:4096 -x509 -keyout $KEY -out $CERT -days 3650 -batch -subj "$(echo -n "$SUBJ" | tr "\n" "/")"
fi
