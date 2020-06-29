# pgjwt
PostgreSQL implementation of [JSON Web Tokens](https://jwt.io/)

## Dependencies

This code requires the pgcrypto extension, included in most
distribution's "postgresql-contrib" package.  The tests require the
pgtap extension to run.

## Install

You will require sudo privledges and the postgres development
libraries for your operating system in most cases.  For ubuntu on
postgres 9.5 for example, you can install them with:

    sudo apt-get install postgresql-server-dev-9.5

Clone the repository and then run:

    'make install'

This creates a new extension that can be installed with 'CREATE
EXTENSION pgjwt;'

To run the tests install pgtap and run 'pg_prove test/test.sql'.
Another approach is to use the docker based test runner, but running
'./test.sh'.  This will require you have docker installed.

## Usage

Create a token.  The first argument must be valid json, the second argument any text:

    => select sign('{"sub":"1234567890","name":"John Doe","admin":true}', 'secret');
                                                                            sign
    -------------------------------------------------------------------------------------------------------------------------------------------------------
     eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiYWRtaW4iOnRydWV9.TJVA95OrM7E2cBab30RMHrHDcEfxjoYZgeFONFh7HgQ

Verify a token:

    => select * from verify('eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiYWRtaW4iOnRydWV9.TJVA95OrM7E2cBab30RMHrHDcEfxjoYZgeFONFh7HgQ', 'secret');
               header            |                       payload                       | valid
    -----------------------------+-----------------------------------------------------+-------
     {"alg":"HS256","typ":"JWT"} | {"sub":"1234567890","name":"John Doe","admin":true} | t

Algorithm
---------

sign() and verify() take an optional algorithm argument that can be
'HS256', 'HS384' or 'HS512'.  The default is 'HS256':

    => select sign('{"sub":"1234567890","name":"John Doe","admin":true}', 'secret', 'HS384'),


## TODO

* public/private keys when pgcrypto gets *_verify() functions

* SET ROLE and key lookup helper functions
