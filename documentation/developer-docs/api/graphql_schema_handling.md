
# hasura schema handling

## export schema

- source: <https://hasura.io/docs/1.0/graphql/manual/schema/export-graphql-schema.html#using-graphqurl>

  ```console
  sudo npm install -g apollo
  apollo schema:download --endpoint https://demo.itsecorg.de/api/v1/graphql --header 'X-Hasura-Admin-Secret: xxx'
  ```

## designing graphql apis

- source: <https://www.graphql-tools.com/docs/introduction/>

### separate business logic from graphql

Separate business logic from the schema. As Dan Schafer covered in his talk, GraphQL at Facebook, it's a good idea to treat GraphQL as a thin API and routing layer. This means that your actual business logic, permissions, and other concerns should not be part of your GraphQL schema. For large apps, we suggest splitting your GraphQL server code into 4 components: Schema, Resolvers, Models, and Connectors, which each handle a specific part of the work.

### create mock data from schema only (without implementing resolvers)

see <https://www.graphql-tools.com/docs/mocking/>
