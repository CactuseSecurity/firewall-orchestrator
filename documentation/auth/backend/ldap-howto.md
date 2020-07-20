# everything you don't know about ldap ;-)

## ldap server on linux

## ldap client access

### adding information with ldapadd

password=passme

    ldapadd -x -D "cn=Manager,dc=example,dc=com" -H ldaps://localhost -W -f x.ldif
    cat x.ldif
```
dn: uid=testuser,dc=example,dc=com
objectClass: posixAccount
objectClass: shadowAccount
objectClass: inetOrgPerson
cn: First Name
sn: Last Name
uid: testuser
userPassword: abcdef
uidNumber: 5000
gidNumber: 5000
homeDirectory: /home/testuser
loginShell: /bin/sh
gecos: Comments
tim@ubu1804:~$ 
```

### searching ldap with ldapsearch

    ldapsearch uid=testuser4 -x

    ldapsearch -x -b "dc=example,dc=com"  -H ldaps://localhost
```
# extended LDIF
#
# LDAPv3
# base <dc=example,dc=com> with scope subtree
# filter: (objectclass=*)
# requesting: ALL
#

# example.com
dn: dc=example,dc=com
objectClass: domain
dc: example

# testuser, example.com
dn: uid=testuser,dc=example,dc=com
objectClass: posixAccount
objectClass: shadowAccount
objectClass: inetOrgPerson
cn: First Name
sn: Last Name
uid: testuser
uidNumber: 5000
gidNumber: 5000
homeDirectory: /home/testuser
loginShell: /bin/sh
gecos: Comments

# search result
search: 2
result: 0 Success

# numResponses: 3
# numEntries: 2
tim@ubu1804:~$ 

```

### delete entries with ldapmodify ###

Delete entry testuser from last chapter with
```
ldapmodify -x -D "cn=Manager,dc=example,dc=com" -W -f delete.ldif
```
With delete.ldif
```
dn: uid=testuser,dc=example,dc=com
changetype: delete
```

### communicate with multiple ldap servers ###

Not tested yet!
```
ldapsearch -H "ldaps://localhost:636,ldaps://127.0.0.1" -x
```

### set/change password of existing user

Not tested yet!
```
tim@ubu1804:~$ ldappasswd -s welcome123 -W -D "cn=Manager,dc=example,dc=com" -x "uid=testuser4,dc=example,dc=com"
Enter LDAP Password: 
tim@ubu1804:~$ 
```


## authentication against ldap from .net (C#)

## querying multiple ldap servers in a row

