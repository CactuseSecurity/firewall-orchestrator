# ldap structure
~~~console

(root)
dc:fworch.internal--------------------------------------------------------------------------------------
|                                                                                                      |
ou:user----------------------------------------------------------------------------|               ou:role -->
|                                                                                  |
|                                                                                  anwender-----------------------------------------------------------------------------------------
|                                                                                  |                                                                                               |
ou:systemuser-------------------------------------                                 ou:tenant1-------------------------------------------------------------------------            ou:tenant2
|                       |                        |                                 |                      |                       |                   |
|                                                                                  |                      |                       |                   |
cn:admin                cn:dbadmin               cn:n.n                            cn:admin               cn:user1_demo                cn:testuser         cn:n.n
uid: 12345                                                                         ...                                            | -->>
passwd: sha256(fworch.1)
n.n.


--> role -----------------------------------------------------------------------------------------
      |                                                     |                  |              |
    rolle1                                               rolle2
    dn: ou=rolle1,ou=role,dc=fworch.internal
    member-uids: admin, user1, user2, ....


-->> testuser
# testuser, tenant1, anwender, user, bla, internal
dn: uid=testuser,ou=tenant1,ou=anwender,ou=user,dc=fworch.internal
cn: Feuerstein, Fred
uid: testuser
userPassword:: <hash>
telephoneNumber: +49 (69) 98765-1234
mail: fred.feuerstein@mail.de
departmentNumber: 76543
~~~

## Questions
- where is the ldap admin (Manager) located in the above directory?
- uid string?
- security? ldapsearch returns roles, hashed pwd without authentication
- implementing links
- schema: choose or create a new schema to define class (posix, ...) - choose a simple one - what is the default  - inet.org.person
- need function/query that searches the role tree and returns all roles this user belongs to
- 
