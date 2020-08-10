# ldap structure
~~~console

(root)
dc:fworch.internal--------------------------------------------------------------------------------------
|                                                                                                      |
ou:user----------------------------------------------------------------------------|               ou:role -->
|                                                                                  |
|                                                                                  anwender-----------------------------------------------------------------------------------------
|                                                                                  |                                                                                               |
ou:systemuser-------------------------------------                                 ou:mandant1-------------------------------------------------------------------------            ou:mandant2
|                       |                        |                                 |                      |                       |                   |
|                                                                                  |                      |                       |                   |
cn:admin                cn:dbadmin               cn:n.n                            cn:admin               cn:fritz                cn:testuser         cn:n.n
uid: 12345                                                                         ...                                            | -->>
passwd: sha256(fworch.1)
n.n.


--> role -----------------------------------------------
      |                |                  |              |
    rolle1          rolle2
    cn: rolle1
    member-uids
    link:-> 12345 (admin)
    link:->
    link:->


-->> testuser
# testuser, mandant1, anwender, user, bla, internal
dn: uid=testuser,ou=mandant1,ou=anwender,ou=user,dc=fworch.internal
mail: fred.feuerstein@mail.de
departmentNumber: 76543
memberOf: cn=rolle1,ou=role,dc=fworch.internal
memberOf: cn=rolle2,ou=role,dc=fworch.internal
telephoneNumber: +49 (69) 98765-1234
givenName: Fred
sn: Feuerstein
cn: Feuerstein, Fred
objectClass: top
objectClass: inetorgperson
objectClass: shadowAccount
objectClass: blablub
objectClass: posixAccount
objectClass: organizationalPerson
objectClass: person
gecos: Fred Feuerstein
gidNumber: 9876
uidNumber: 9900123
uid: testuser
passwd: laberbla
~~~

## Questions
- difference systemuser, anwender?
- two admins (systemuser, anwender)?
- exact division between roles and users?
- tenants (granular access on a "per-device-level") rather as roles than user?
- where is the ldap admin (Manager) located in the above directory?
- uid numeric or string?
- difference cn, uid?
- difference uidNumber, uid?
