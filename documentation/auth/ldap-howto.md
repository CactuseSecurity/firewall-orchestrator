# everything you don't know about ldap ;-)

## ldap server on linux

- most information about Openldap under <https://www.openldap.org/doc/admin24/index.html>
- our implementation is mostlyhere <https://github.com/CactuseSecurity/firewall-orchestrator/tree/master/roles/openldap-server>

## general information

- see structure of ldap tree here <https://github.com/CactuseSecurity/firewall-orchestrator/blob/master/documentation/auth/ldap_structure.png>
- every entry in ldap has a distinguished name (dn) which is unique
- the dn is composed of the tree path to the entry
- to access ldap you have to bind as an user (entry in ldap)
- this is done by including the option -D
- most user/entries you bind with have passwords, you pass these as text with -x or link to the file where they are stored with -y
- if you don't choose a bind option, you bind as anonymous
- ldap is currently rwe by user manager and read only by inspector (their dn's are in the examples later)

## second ldap database

- for test purposes you may install an additional domain example.com with test users and roles
- install with 

    cd firewall-orchestrator; ansible-playbook -e "second_ldap_db=yes" site.yml -K

- to access you have to bind with the fworch.internal manager dn (-D) and password (-w/-y) and change the searchbase (-b). E.g

    sudo ldapsearch -b dc=example,dc=com -D cn=Manager,ou=systemuser,ou=user,dc=fworch,dc=internal -y /usr/local/fworch/etc/secrets/ldap_manager_pw.txt


## some specific questions

Is it possible to gain all information below a tree node?
- Yes, this answer uses C# and the library Novell
- Good documentation https://www.novell.com/documentation/developer/ldapcsharp/?page=/documentation/developer/ldapcsharp/cnet/data/bovtz77.html
- 1. You have to bind the LDAP server (documentation link Chapter 3.1)
- 2. Search with a search base (documentation link Chapter 3.2)
- Example:
- string searchBase = "ou=tenant2,ou=operator,ou=user,dc=fworch,dc=internal";
- LdapSearchQueue queue=ldapConn.Search (searchBase,...)
- This only searches in and below tenent2 (can be adjusted and finetuned with Search Scope)
- If you want to use Linux command line use ldapsearch -b "searchbase" ...
List all users with identical login name
- On command Line: ldapsearch -D uid=inspector,ou=systemuser,ou=user,dc=fworch,dc=internal -y /usr/local/fworch/etc/secrets/ldap_inspector_pw.txt uid=user1_demo -x
- If you want to search only in tenant 1 add "-b ou=tenant1,ou=operator,ou=user,dc=fworch,dc=internal" to query


## ldap client access

### adding information with ldapadd

(default password=passme)

    ldapmodify -H ldaps://localhost/ -D cn=Manager,dc=example,dc=com -w passme -x -f adduser1.ldif

    tim@ubu1804:~$ cat adduser1.ldif
    dn: uid=user1,dc=example,dc=com
    changetype: add
    cn: user1
    uid: user1
    sn: Meier
    objectClass: inetOrgPerson
    tim@ubu1804:~$

### set/change password of existing user

For default installation:

    tim@ubu1804:~$ ldappasswd -s changeme -w passme -D "cn=Manager,dc=example,dc=com" -x "uid=user1,dc=example,dc=com"

For cactus installation:

    ldappasswd -s <new passwd of user admin> -w <pwd of Manager> -D "cn=Manager,dc=fworch,dc=internal" -x "uid=admin,ou=systemuser,ou=user,dc=fworch,dc=internal"

### adding a test user

```
ldapmodify -H ldaps://localhost -D cn=Manager,dc=fworch,dc=internal -w IuMGtWNzEHdvodr -x -f fgreporter.ldif 

ldappasswd -s fworch.1 -w IuMGtWNzEHdvodr -D "cn=Manager,dc=fworch,dc=internal" -x "uid=fgreporter,ou=systemuser,ou=user,dc=fworch,dc=internal"

cat fgreporter.ldif 
dn: uid=fgreporter,ou=systemuser,ou=user,dc=fworch,dc=internal
changetype: add
objectClass: top
objectclass: inetorgperson
cn: fgreporter
sn: fgreporter
```

### searching ldap with ldapsearch

The old way is

    tim@ubu1804:~$ ldapsearch uid=user1 -x
    # extended LDIF
    #
    # LDAPv3
    # base <dc=example,dc=com> (default) with scope subtree
    # filter: uid=user1
    # requesting: ALL
    #

    # user1, example.com
    dn: uid=user1,dc=example,dc=com
    cn: user1
    uid: user1
    sn: Meier
    objectClass: inetOrgPerson
    userPassword:: e1NTSEF9VmhoditGb3RabnlzZjNsbmZNZ0ZKVUJsT05QckVJQWo=

    # search result
    search: 2
    result: 0 Success

    # numResponses: 2
    # numEntries: 1
    
The search above won't work anymore. LDAP is now exlusive to the users "manager" and "inspector" (read only). The new way

    fworch@FWO:~$ ldapsearch -x -W -D cn=manager,ou=systemuser,ou=user,dc=fworch,dc=internal -y /usr/local/fworch/etc/secrets/ldap_manager_pw.txt uid=admin

### check password
wrong password:

    tim@ubu1804:~$ ldapwhoami -x -w fworch.2 -D uid=admin,ou=tenant0,ou=operator,ou=user,dc=fworch,dc=internal -H ldaps://localhost/
    ldap_bind: Invalid credentials (49)

correct password:

    tim@ubu1804:~$ ldapwhoami -x -w fworch.1 -D uid=admin,ou=tenant0,ou=operator,ou=user,dc=fworch,dc=internal -H ldaps://localhost/
    dn:uid=admin,ou=tenant0,ou=operator,ou=user,dc=fworch,dc=internal

### delete entries with ldapmodify

Delete entry user1 with

    ldapmodify -x -D "cn=Manager,dc=example,dc=com" -W -f delete.ldif

With delete.ldif

    dn: uid=user1,dc=example,dc=com
    changetype: delete

### add role

Add role with

    ldapadd -x -W -D cn=Manager,dc=fworch,dc=internal -y /usr/local/fworch/etc/secrets/ldap_manager_pw.txt -f testgroup.ldif

With testgroup.ldif

    dn: cn=testrole,ou=role,dc=fworch,dc=internal
    objectClass: top
    objectClass: groupofuniquenames
    cn: testrole
    uniqueMember:    #This is mandatory
    description: This is a test role
    
There exists a concept "memberOf" which lists the roles of an uid. https://github.com/osixia/docker-openldap/issues/304 There are concerns about the security of this feature (its produced by Microsoft)

### add user to role

Add user user1_demo to role testrole with

    ldapmodify -x -W -D cn=Manager,dc=fworch,dc=internal -y /usr/local/fworch/etc/secrets/ldap_manager_pw.txt -f add_user.ldif
    
With add_user.ldif

    dn: cn=testrole,ou=role,dc=fworch,dc=internal
    changetype: modify
    add: uniquemember
    uniquemember: uid=user1_demo,ou=tenant1,ou=operator,ou=user,dc=fworch,dc=internal
    
Here user1_demo is not required to exist somewhere in the ldap tree.

### communicate with multiple ldap servers
Not tested yet!

    ldapsearch -H "ldaps://localhost:636,ldaps://127.0.0.1" -x
    
### querying AD
#### search user
currently works via ldap not ldaps.
Example:
```console
tim@deb10-test:/var/log/fworch$ ldapsearch -x -D "ad-readonly@int.cactus.de" -H ldap://192.168.100.8 -W -b "DC=Users,DC=int,DC=cactus,DC=de" "(sAMAccountName=tim)"
dn: CN=Tim Purschke,CN=Users,DC=int,DC=cactus,DC=de
cn: Tim Purschke
distinguishedName: CN=Tim Purschke,CN=Users,DC=int,DC=cactus,DC=de
displayName: Tim Purschke
uSNChanged: 4413227
name: Tim Purschke
objectGUID:: 8rakK4DX40ahetu1vNDebA==
userAccountControl: 512
objectSid:: AQUAAAAAAAUVAAAA2YTAfH0kWZgXgpVqUAQAAA==
sAMAccountName: tim
userPrincipalName: tim@int.cactus.de
```
#### search for groups
    ldapsearch -x -W -H "ldap://192.168.100.8" -D "ad-readonly@int.cactus.de" -b "DC=int,DC=cactus,DC=de" -y ./pwd "(objectClass=group)"
    ldapsearch -x -W -H "ldap://192.168.100.8" -D "ad-readonly@int.cactus.de" -b "DC=int,DC=cactus,DC=de" -y ./pwd "(&(name=T2*)(objectClass=group))"
    
#### TLS-Fehler stringray

Auf dem System ist keine Standard-Serverreferenz vorhanden. Serveranwendungen, die Standard-Systemreferenzen verwenden, werden keine SSL-Verbindungen akzeptieren. Als Beispiel einer solchen Anwendung dient der Verzeichnisserver. Dies hat keine Auswirkung auf Anwendungen wie der Internet Information Server, die die eigenen Referenzen verwalten, .

#### test with stingray.int.cactus.de

source: <https://tylersguides.com/guides/search-active-directory-ldapsearch/>

```code
tim@ubu18test:~/firewall-orchestrator$ openssl s_client -connect 192.168.100.8:636 -showcerts </dev/null
CONNECTED(00000005)
write:errno=104
---
no peer certificate available
---
No client certificate CA names sent
---
SSL handshake has read 0 bytes and written 315 bytes
Verification: OK
---
New, (NONE), Cipher is (NONE)
Secure Renegotiation IS NOT supported
Compression: NONE
Expansion: NONE
No ALPN negotiated
Early data was not sent
Verify return code: 0 (ok)
---
ldapsearch -x -D "tim@cactus.de" -H ldaps://192.168.100.8 -W -b "dc=cactus" "(sAMAccountName=user)" 
```

## authentication against ldap from .net (C#)

ext. documentation, see <https://auth0.com/blog/using-ldap-with-c-sharp/>


## ldap and c#

- Good documentation <https://www.novell.com/documentation/developer/ldapcsharp/?page=/documentation/developer/ldapcsharp/cnet/data/bovtz77.html>

## test if everything is ok after installation

- the default tree dc=fworch,dc=internal

    sudo ldapsearch -D cn=Manager,ou=systemuser,ou=user,dc=fworch,dc=internal -y /usr/local/fworch/etc/secrets/ldap_manager_pw.txt
    
- the optional second tree

    sudo ldapsearch -b dc=example,dc=com -D cn=Manager,ou=systemuser,ou=user,dc=fworch,dc=internal -y /usr/local/fworch/etc/secrets/ldap_manager_pw.txt

- use other binds (-D) like writer or inspector for further testing

