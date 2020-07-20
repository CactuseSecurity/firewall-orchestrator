# everything you don't know about ldap ;-)

## ldap server on linux

## ldap client access

### adding information with ldapadd

password=passme

    ldapmodify -H ldaps://localhost/ -D cn=Manager,dc=example,dc=com -w passme -x -f adduser1.ldif
```
tim@ubu1804:~$ cat adduser1.ldif 
dn: uid=user1,dc=example,dc=com
changetype: add
cn: user1
uid: user1
sn: Meier
objectClass: inetOrgPerson
tim@ubu1804:~$ 

```

### set/change password of existing user

    tim@ubu1804:~$ ldappasswd -s changeme -w passme -D "cn=Manager,dc=example,dc=com" -x "uid=user1,dc=example,dc=com"

### searching ldap with ldapsearch
```
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

tim@ubu1804:~$ 

```

### check password
wrong password:

    tim@ubu1804:~$ ldapwhoami -x -w dontchangeme -D uid=user1,dc=example,dc=com  -H ldaps://localhost/
    ldap_bind: Invalid credentials (49)

correct password:

    tim@ubu1804:~$ ldapwhoami -x -w changeme -D uid=user1,dc=example,dc=com  -H ldaps://localhost/
    dn:uid=user1,dc=example,dc=com

### delete entries with ldapmodify ###

Delete entry user1 with

    ldapmodify -x -D "cn=Manager,dc=example,dc=com" -W -f delete.ldif

With delete.ldif
```
dn: uid=user1,dc=example,dc=com
changetype: delete
```

### communicate with multiple ldap servers ###

Not tested yet!
```
ldapsearch -H "ldaps://localhost:636,ldaps://127.0.0.1" -x
```

## authentication against ldap from .net (C#)

## querying multiple ldap servers in a row

