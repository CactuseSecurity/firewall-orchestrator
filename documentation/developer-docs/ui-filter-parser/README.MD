# parse filter line

## automatically generating parser from grammar
- using [ANTLR v4](https://www.antlr.org/download.html)
- <https://github.com/antlr/antlr4/tree/master/runtime/CSharp>
- simple example grammar is defined [here](/design/UI/ui-filter-grammar.g4)

## use hasura competitor prisma

- <https://www.prisma.io/>

## use graphql only
- <https://dev.to/mgustus/filtering-graphql-query-using-typescript-and-typeorm-2l49>


### schema extension

```graphql
enum Operator {
  AND
  OR
}

enum Operation {
  EQ
  IN
  LIKE
  GE
}

input Filter {
  op: Operation!
  values: [String!]!
  field: String!
  relationField: String
}

input FiltersExpression {
  operator: Operator!
  filters: [Filter!]
  childExpressions: [FiltersExpression!]
}

extend type Query {
  listRulesDetails(filters: FiltersExpression): [management!]!
}
```

### sample query with filters
```graphql

query listRulesDetails($managementId: [Int!], $deviceId: [Int!], $ruleSrcName: [String!], $ruleSrcIp: [cidr!], $limit: Int, $offset: Int) {
  management(where: {mgm_id: {_in: $managementId}}, order_by: {mgm_name: asc}) {
    mgm_id
    mgm_name
    devices(where: {dev_id: {_in: $deviceId}}, order_by: {dev_name: asc}) {
      dev_id
      dev_name
      rules(limit: $limit, offset: $offset, where: {active: {_eq: true}, rule_src: {_in: $ruleSrcName}, rule_disabled: {_eq: false}, rule_froms: {object: {obj_ip: {_in: $ruleSrcIp}}}}, order_by: {rule_num_numeric: asc}) {
        rule_uid
      }
    }
  }
}
```
