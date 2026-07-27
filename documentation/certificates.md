# Certificates in FWO

FWO creates a private internal certificate authority (CA) during installation.
Its private key is retained only on the middleware server at
`/usr/local/fworch/etc/secrets/ca/`; do not copy or back it up outside
the protected installation secrets.

The `internalCA` role issues a separate client certificate on every FWO host,
an Apache server certificate on API, middleware, and UI hosts, and a separate
OpenLDAP server certificate on the middleware host. All FWO hosts trust only
the CA's public certificate through their operating-system CA store. This
makes normal TLS chain and hostname verification possible between FWO
components without accepting each host's self-signed leaf certificate.

Consumers that do not use the operating-system trust store need to name a CA
file explicitly. The issuer copy under `/usr/local/fworch/etc/secrets/ca/` is
root-only and exists on the middleware server alone, so every host also
receives a world-readable copy at `/etc/fworch/fworch-internal-ca.crt`
(`internalca_ca_certificate_local`). That is the path Apache client
verification, `ldap.conf`, the importer, and the customizing scripts use;
never reference the issuer copy from another host.

The default key algorithm is P-256 EC (`internalca_key_type: ECC`,
`internalca_key_curve: secp256r1`). For environments with legacy TLS clients,
TLS-inspection appliances, or hardware that requires RSA, set
`internalca_key_type: RSA`; it uses RSA-3072 (`internalca_key_size`) instead.
Choose the algorithm before the first installation. Changing it later requires
rotating the CA and all issued certificates.

The Hasura API proxy requires a client certificate issued by this CA by default
(`api_require_client_certificate: true`). UI, middleware, importer, and
installer integration tests present their local client identity. Set it to
`false` only while performing a staged migration. To use an administrator
managed Apache certificate, set `internalca_issue_apache_certificate: false`
and provide `internalca_apache_certificate` and
`internalca_apache_private_key`; the role then leaves those files untouched.
Set `internalca_peer_ca_certificate` to the path of the issuing root CA
certificate on every FWO client host. This explicit trust anchor is used by
the importer, customizing scripts, and .NET GraphQL clients. When an upgrade
retains a customer-managed API certificate without this setting, FWO stops
before deploying clients that would trust the unrelated internal CA.

During an upgrade, the role preserves a customer-managed Apache or OpenLDAP
certificate/key pair, identified by the certificate and key belonging together
(matching public-key fingerprints, checked for any key algorithm). It replaces
anything FWO issued itself: the historical self-signed certificate, recognised
by FWO's legacy organisation attribute, and any leaf whose issuer is the
internal CA, so that FWO-issued certificates stay renewable.

Apache server identities are stored at
`/usr/local/fworch/etc/secrets/apache/server.{crt,key}`. The private
key is `0640` and owned by root. Client identities are stored in
`/usr/local/fworch/etc/secrets/client/client.{crt,key}` and are readable
only by the FWO service account.

On upgrade, the role reuses the existing CA and issued identities. Issued
certificates are valid for `internalca_certificate_validity_days` (825) and are
re-signed automatically by any installer run that happens within
`internalca_renewal_threshold_days` (30) of expiry, so a host that is upgraded
at least once a year never expires. To rotate a certificate ahead of that
schedule, remove the affected certificate from
`/usr/local/fworch/etc/secrets/ca/issued/<host>/` and rerun the installer.
Rotating the CA is a deliberate maintenance operation: replace the CA on every
FWO host before issuing replacement leaves, otherwise existing internal
connections will stop trusting each other.

FWO's shared GraphQL client now uses normal platform TLS validation. Ensure
that `api_uri`, `middleware_uri`, and `ui_hostname` resolve to a name or IP
contained in the certificates' SANs. The defaults cover the inventory host
names, `localhost`, `127.0.0.1`, and the FWO endpoint defaults; distributed
deployments should set these inventory values to their real DNS names before
installation.

Scripts that talk to the API must present the client identity, for example:

```
curl --request POST \
    --cert /etc/fworch/secrets/client/client.crt \
    --key /etc/fworch/secrets/client/client.key \
    --cacert /etc/fworch/fworch-internal-ca.crt \
    --url https://localhost:9443/api/v1/graphql \
    --header 'content-type: application/json' \
    --data '{"query":"query { management {mgm_name} }"}'
```

The client private key is readable only by the FWO service account, so run such
commands as that user or as root.
