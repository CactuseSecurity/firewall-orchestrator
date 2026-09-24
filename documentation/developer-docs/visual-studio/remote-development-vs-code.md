# remote development using remote-ssh

This enables us to develop, run and debug everything remotely on a single linux machine of our choice.

See <https://code.visualstudio.com/docs/remote/ssh> for instructions how to set this up.

After setup we need to
- clone our fork repo on the remote machine
- install fworch
- stop the services we want to debug (middleware, ui)
- start the services remotely in vs (code) for debugging

## Reaching the remote instance from your browser and debugger

Forward the **native** service ports, not the reverse proxy ports: 5000 (middleware) and
8880 (UI). Alternatively keep using the Apache endpoints and import the FWO CA
certificate of the test machine into your browser, otherwise every request fails on an
unknown issuer.

The API is only reachable through the Apache vhost, which requires a client certificate.
For local debugging you can talk to Hasura directly instead - it listens on 127.0.0.1:8080
on the remote machine, so tunnel that port and point your local `fworch.json` at it:

```json
  "api_uri": "http://127.0.0.1:8080/v1/graphql",
```

Note the missing `/api/`: that prefix belongs to the Apache vhost, not to Hasura, and the
tunnelled port is 8080 rather than 9443.

**Development hosts only.** A plain `http://` `api_uri` presents no client certificate and
checks no server certificate, so it switches off the API endpoint protection entirely - the
middleware and the UI log a warning when they open an API connection with one. Never leave it in the
`fworch.json` of an installation that is used by anybody else.
