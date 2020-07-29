~~~console
(root)
dc:fworch.internal--------------------------------------------------------------------------------------
|                                                                                                      |
ou:user----------------------------------------------------------------------------|               ou:groups -->
|                                                                                  |
|                                                                                  anwender-----------------------------------------------------------------------------------------
|                                                                                  |                                                                                               |
ou:systemuser-------------------------------------                                 ou:mandant1-------------------------------------------------------------------------             ou:mandant2
|                       |                        |                                 |                      |                       |                   |
|                                                                                  |                      |                       |                   |
cn:admin                  cn:db_admin            cn:n.n                            cn:admin               cn:fritz                cn:testuser         cn:n.n
uid: 12345                                                                         ...                                            | -->>
passwd: sha256(fworch.1)
n.n.





--> groups-----------------------------------------------
      |                |                  |              |
    rolle1          rolle2             
    cn: rolle1
    member-uids
    link:-> 12345 (mamager)
    link:->
    link:->



-->> testuser
# testuser, mandant1, anwender, user, bla, internal
dn: uid=testuser,ou=mandant1,ou=anwender,ou=user,dc=bla.internal
mail: fred.feuerstein@mail.de
departmentNumber: 76543
memberOf: cn=rolle1,ou=groups,dc=bla.internal
memberOf: cn=rolle2,ou=groups,dc=bla.internal
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
