# everything you don't know about ldap ;-)

## ldap server on linux

see ansible installation under <https://github.com/CactuseSecurity/firewall-orchestrator/tree/master/roles/openldap-server>

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


### searching ldap with ldapsearch

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

### check password
wrong password:

    tim@ubu1804:~$ ldapwhoami -x -w fworch.2  -D uid=admin,ou=systemuser,ou=user,dc=fworch,dc=internal  -H ldaps://localhost/
    ldap_bind: Invalid credentials (49)

correct password:

    tim@ubu1804:~$ ldapwhoami -x -w fworch.1  -D uid=admin,ou=systemuser,ou=user,dc=fworch,dc=internal  -H ldaps://localhost/
    dn:uid=admin,ou=systemuser,ou=user,dc=fworch,dc=internal

### delete entries with ldapmodify ###

Delete entry user1 with

    ldapmodify -x -D "cn=Manager,dc=example,dc=com" -W -f delete.ldif

With delete.ldif

    dn: uid=user1,dc=example,dc=com
    changetype: delete

### communicate with multiple ldap servers ###
Not tested yet!

    ldapsearch -H "ldaps://localhost:636,ldaps://127.0.0.1" -x

## authentication against ldap from .net (C#)

ext. documentation, see <https://auth0.com/blog/using-ldap-with-c-sharp/>

## querying multiple ldap servers in a row
