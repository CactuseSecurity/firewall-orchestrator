# how to set hasura permissions

- got to graphiql https://demo.itsecorg.de/api/console/data/schema/public/tables/device/permissions
- choose a role and add "Row select permissions"

     {"dev_id":{"_in":"X-Hasura-Visible-Devices"}}
     
