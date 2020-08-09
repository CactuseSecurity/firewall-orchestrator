# How to install hasura

Setup: <https://docs.hasura.io/1.0/graphql/manual/deployment/docker/index.html>

NB: depends on docker role!

- edit docker-run.sh as follows:

  ```console
  \#! /bin/bash
      docker run -d --net=host -p 8080:8080 \\
             \-e HASURA_GRAPHQL_DATABASE_URL=postgres://dbadmin:st8chel@localhost:5432/fworchdb \\
             \-e HASURA_GRAPHQL_ENABLE_CONSOLE=true \\
             \-e HASURA_GRAPHQL_ADMIN_SECRET=st8chelt1er \\
             hasura/graphql-engine:v1.0.0
  ```

- startup via

  ```console
  docker-run.sh
  ```

d) connect to <http://localhost:8080/console>

e) configure hasura manually once in the graphical gui

f) install hasura cli (see <https://docs.hasura.io/1.0/graphql/manual/hasura-cli/install-hasura-cli.html>) curl -L <https://github.com/hasura/graphql-engine/raw/master/cli/get.sh> | bash

g) export hasura metadata
```console
hasura init --directory DIR --endpoint <http://localhost:8080> --admin-secret XXX
cd DIR
hasura metadata export
```
add migrations/metadata.yaml to fworch sources

h) import metadata again when installing fworch (is there a way to do this automatially?)
```console
hasura init --directory DIR --endpoint <http://localhost:8080> --admin-secret XXX
cd DIR
hasura metadata apply
```
i) use <http://json2html.com/#getstarted> to convert json results from api calls to html tables?

Hasura-graphql-engine - Framework to provide graphql for Postgresql databases Hasura: <https://github.com/hasura/graphql-engine/blob/master/architecture/live-queries.md> access gui: <http://localhost:8080/console/api-explorer> (oder <http://localhost:8080/console>)
