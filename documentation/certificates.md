# Certificates in FWO

FWO creates a private internal certificate authority (CA) during installation.
Its private key is retained only on the middleware server at
`/usr/local/fworch/etc/secrets/ca/`; do not copy or back it up outside
the protected installation secrets.

Each installation names its CA `fworch Internal CA <16 hexadecimal characters>`,
derived from the CA's own public key, so that two installations never issue
certificates under the same subject. An installation created before 9.5.0 keeps the
plain `fworch Internal CA` it was created with: the subject of an existing CA is read
back and reused on every run. A changed subject would replace the anchor everything
outside the installer already trusts - the installer reissues the certificates it
signed itself, but nothing re-imports the new CA into a browser or trust store holding
the old one, and a run that does not reach every host leaves the rest with a
certificate the new CA never signed. See *Trusting the internal CA in a browser* below
for what a shared name means for a browser. If the CA certificate is present but
cannot be parsed, the installer stops and names the file rather than replacing the
anchor its issued certificates depend on.

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
verification uses; never reference the issuer copy from another host.

Alongside it every host receives `/etc/fworch/fworch-trust-bundle.crt`
(`internalca_trust_bundle`), which holds the internal CA plus any additional issuer
configured as `internalca_peer_ca_certificate`. Clients that load
`tls_ca_certificate` - the importer, the customizing scripts and the GraphQL and
LDAP clients - validate FWO endpoints against it, and so does `ldap.conf`; see
*administrator managed certificates* below for why the two files are kept apart.

The installer writes the system-wide LDAP client configuration (`/etc/ldap/ldap.conf`
on Debian, `/etc/openldap/ldap.conf` on RedHat) on the middleware host, pointing
`TLS_CACERT` at the trust bundle and setting `TLS_REQCERT demand`, so an administrator
running `ldapsearch` by hand verifies the connection. The installer's own
`ldapsearch`/`ldapmodify` calls pass the same settings explicitly through
`fwo_ldap_tls_opts`, defined once in `inventory/group_vars/all.yml` (the `fwo_`
prefix keeps it clear of the same-named fact that the released 5.4.1 and 6.4.9
OpenLDAP upgrade files still set to `TLS_REQCERT=never`). Every TLS-enabled
installation verifies, the retained-certificate case included: the anchor is the
bundle rather than the internal CA alone precisely so that a customer-managed slapd
certificate can be validated too, and `roles/internalCA` refuses an upgrade that
keeps one it cannot build to an anchor in that bundle. The Ansible `ldap_*`
modules take the same decision through `ldap_verify_certs` and
`ldap_module_ca_path`, since those tasks carry the Manager bind password.

The middleware service does not use libldap, so it validates separately, and from
9.5.0 it does validate: an LDAP server certificate is accepted when the host already
trusts it, or when it was issued by the FWO internal CA, and is rejected otherwise.
Earlier versions accepted every certificate. **This can affect an existing external
directory**: if your AD or LDAP server presents a self-signed certificate, or one
from a CA the middleware host does not trust, authentication against it will now
fail with a rejection logged under `LdapTls`. Install the issuing CA into the
middleware host's trust store before upgrading.

The **address** matters as much as the issuer: a host name mismatch is rejected outright and
is never rescued by the internal CA, so the certificate has to carry the address the
connection is configured with - as an `IP:` entry when that address is an IP literal, since a
`DNS:` entry holding the same digits does not match it. This validation is the middleware's
own and is not identical to what `libldap` does for the installer's `ldapsearch` calls: the
middleware additionally accepts anything the host itself trusts, so a certificate the
installer's own calls reject can still satisfy the middleware, and both reject a name
mismatch.

FWO's own internal OpenLDAP is unaffected while its certificate comes from the internal CA.
If an upgrade retains an administrator-managed certificate there instead, the installer
checks it on all three counts, rather than completing and leaving every login broken. Both
installer preflight plays - `common` and `internalCA` - are `any_errors_fatal`, so a
refusal aborts the run for every host before the database is migrated and before any
service is reconfigured, on a distributed installation too, where only the middleware host
reaches these checks. What has already happened at that point is the switch of the web
endpoints to the maintenance site, which `roles/common` performs earlier in every upgrade:
the endpoints stay on it until the certificate is corrected and the upgrade re-run. One
that does not cover `openldap_server` fails the run naming the missing address. One
retained while `internalca_peer_ca_certificate` is still at its default fails naming the
issuer, because the internal CA alone cannot validate it. And one that cannot be built to
an anchor in the trust bundle from the certificate file itself fails quoting what OpenSSL
said - a leaf whose issuing intermediate the file does not carry, or a leaf from a root
other than the one configured, both land here. The peer CA is asked for
even when the issuing CA already happens to be a trust anchor on the middleware host: the
installer's own LDAP calls read the bundle and not the host trust store, and naming the root
as the peer CA is additive, since it is installed beside the internal CA and replaces
nothing. Both the intermediate and the second-root case are fixable without reissuing
anything - append the intermediate to the certificate file slapd serves, or name both roots
in the one file `internalca_peer_ca_certificate` points at, which is read as a PEM bundle.

The default key algorithm is P-256 EC (`internalca_key_type: ECC`,
`internalca_key_curve: secp256r1`). For environments with legacy TLS clients,
TLS-inspection appliances, or hardware that requires RSA, set
`internalca_key_type: RSA`; it uses RSA-3072 (`internalca_key_size`) instead.
Choose the algorithm before the first installation. Changing it later requires
rotating the CA and all issued certificates with the reset procedure below.

The Hasura GraphQL API proxy requires a client certificate issued by this CA by
default (`graphql_api_requires_client_certificate: true`). UI, middleware,
importer, and installer integration tests present their local client identity.
Set it to `false` only while performing a staged migration.

The middleware REST API deliberately does not require a client certificate. It
is the endpoint external callers use to exchange username and password for a
JWT, so demanding an FWO-issued client identity there would make it unusable
for its purpose. It is still served over TLS with an internal CA certificate,
so callers should verify it against the CA trust anchor described above.

## Host names come from the inventory

Every name FWO addresses itself under is derived from `inventory/hosts.yml`, and that
is the only file to edit when an installation has to use a particular name. Renaming
the host there moves all of these at once:

| Name | Built from | Used for |
| --- | --- | --- |
| `api_uri` | `api_network_listening_ip_address` | middleware and UI GraphQL connections, API integration tests |
| `middleware_uri` | `middleware_hostname` | UI, importer, `api-docs` availability test |
| UI endpoint | `ui_hostname`, `ui_server_name` | browsers, UI availability test |

All three are validated against the trust bundle, so each has to appear in the
subjectAltName of the certificate its Apache endpoint serves. `roles/internalCA`
collects exactly these variables when it issues that certificate, so an FWO-issued
certificate always covers them however the host is named.

An **administrator-managed certificate is the case that needs attention**: FWO cannot
add names to it, so the configuration has to be brought to the certificate rather than
the other way round. On a single-host installation, one line does it:

```yaml
# /etc/fworch/fwo-install-settings.yml
fwo_endpoint_hostname: fwo.example.com
```

That file lives outside the git repository, so it survives `git pull` and a fresh clone,
and it applies to every clone on the host - including a `--tags certificates` renewal run
by a second administrator, which therefore reissues for the same names as the original
install. See *host-wide installer settings* in
`documentation/installer/install-advanced.md`.

The alternative, and the only option when the components are spread over several hosts,
is to name the hosts in `inventory/hosts.yml` themselves:

```yaml
all:
  hosts:
    fwo.example.com:
      ip_address: 10.1.1.81
      ansible_connection: local
  children:
    frontends:
      hosts: { fwo.example.com: }
    # ... the same host in every other group
```

Keep such an edit as a local git commit and upgrade with `git pull --rebase`, or the next
upgrade will either conflict with it or discard it.

Leaving the shipped `localhost` in place while the vhost serves a certificate issued
for `fwo.example.com` makes every FWO client reject its own API: the URL says
`localhost`, the certificate says something else, and a name mismatch fails validation
before the chain is even considered.

An upgrade that keeps an administrator-managed certificate does not get that far.
`roles/internalCA` reads the subjectAltName of the certificate it is about to retain and
stops the run in its first minute if any of the three names above is missing from it,
naming the missing names, what the certificate does cover, and `fwo_endpoint_hostname`
as the remedy. A wildcard entry counts for the one label it covers, so a certificate
issued as `*.example.com` satisfies `fwo.example.com`. A certificate with no
subjectAltName at all never passes: the common name alone has not been accepted by
either OpenSSL or .NET for years.

The middleware cannot start without its first API query, so this shows up as a `503`
from its Apache reverse proxy. It does not wait for the API indefinitely: after a
bounded startup budget it logs the endpoint it addressed and what to check, then exits
with code 78 so systemd reports and restarts it. `journalctl -u fworch-middleware` is
therefore where the cause is, in both directions - a service that keeps restarting with
that message has a certificate or configuration problem, one that never gets that far
has no API at all.

Two addresses deliberately do **not** follow the inventory name, because they are
internal and their services only listen on loopback on a single-host installation:

- `middleware_native_listener_address` - the address the middleware's own web server
  binds to, and the address its reverse proxy forwards to. Kestrel treats any host
  name other than `localhost` as a wildcard bind, so a routable name here would
  publish the unauthenticated, plain-HTTP middleware port on every interface.
- `fworch_db_connect_host` - the address clients open PostgreSQL connections to.
  PostgreSQL listens on loopback and `pg_hba.conf` grants `127.0.0.0/8` and `::1/128`
  only, unless `distributed_install` is set.

To use an administrator
managed Apache certificate, set `internalca_issue_apache_certificate: false`
and provide `internalca_apache_certificate` and
`internalca_apache_private_key`; the role then leaves those files untouched.
Set `internalca_peer_ca_certificate` to the path of the issuing root CA
certificate on every FWO client host. When an upgrade retains a customer-managed
API certificate without this setting, FWO stops before deploying clients that
would trust the unrelated internal CA.

When the customer-managed leaf was issued by one or more intermediate CAs, set
`internalca_peer_ca_intermediate_certificates` to a PEM bundle containing those
certificates in leaf-to-root order, without the root itself. The file is needed
only on hosts serving an FWO Apache endpoint. The installer verifies that the
leaf chains through this bundle to `internalca_peer_ca_certificate` before it
restarts Apache, and configures Apache to send the intermediates to every TLS
client. For example:

```
-e internalca_peer_ca_certificate=/etc/ssl/certs/customer-root.pem \
-e internalca_peer_ca_intermediate_certificates=/etc/ssl/certs/customer-intermediates.pem
```

An upgrade also retains an existing `SSLCertificateChainFile` reference from an
FWO Apache vhost when the new variable is not supplied explicitly.

The setting is a global input, but it is applied **per host**: only a host that
actually keeps a customer-managed certificate serves the intermediates and has its
leaf verified against them. A host in the same distributed installation whose leaf
FWO issues keeps no chain file and is verified against the internal CA, so one
endpoint can stay customer-managed while another is FWO-issued. If several endpoints
carry certificates from different customer roots, concatenate those roots into the
single file `internalca_peer_ca_certificate` points at: it is read as a PEM bundle
and every certificate in it becomes a trust anchor.

Both `internalca_peer_ca_certificate` and
`internalca_peer_ca_intermediate_certificates` must be **PEM** files. OpenSSL also
reads DER, but FWO's own TLS clients read the trust bundle as PEM only, so a DER
certificate would be silently ignored at run time; the installer therefore rejects
one. Convert it first:

```
openssl x509 -inform DER -in customer-root.der -out customer-root.pem
```

To go back to an FWO-issued certificate, remove the customer certificate from the
vhost and return `internalca_peer_ca_certificate` to its default. The next installer
run reduces the trust bundle to the internal CA and removes the retired issuer from
the operating-system trust store again.

That issuer is **added to** FWO's trust configuration, not substituted for the
internal CA. The installer concatenates both into a trust bundle at
`/etc/fworch/fworch-trust-bundle.crt` (`internalca_trust_bundle`), and that is the
path written to `fworch.json` as `tls_ca_certificate` - the anchor set the importer,
the customizing scripts, the GraphQL and LDAP clients, and the integration tests
validate FWO endpoints against. It also installs the configured peer CA as an
FWO-owned operating-system trust anchor on every host, for platform-validated
clients such as the middleware client. Adding rather than replacing matters because
retention is decided per host: in a distributed installation one Apache endpoint can
keep a customer-managed certificate while another still serves an internal CA one,
and a single anchor could not cover both.

Do not confuse the bundle with `internalca_ca_certificate_local`
(`/etc/fworch/fworch-internal-ca.crt`), which stays the internal CA alone. That one
is what Apache verifies *client* certificates against, and adding a customer issuer
there would let it mint FWO client identities.

The middleware and UI read the trust anchors once and reuse them, but reload them
when the file changes, so replacing the bundle alone - a rotated CA, or a peer
CA added after the fact - takes effect without restarting those services. A
renewed *client identity* still requires a restart, which the installer performs:
`roles/internalCA` restarts the middleware, the UI and slapd, and reloads Apache,
whenever it reissued the identity that service holds. That happens on a plain
installer run and on a `--tags certificates` run alike, so a certificate-only
renewal does not leave services on the previous certificate.

During an upgrade, the role preserves a customer-managed Apache or OpenLDAP
certificate/key pair, identified by the certificate and key belonging together
(matching public-key fingerprints, checked for any key algorithm). It replaces
anything FWO issued itself: the historical self-signed certificate, recognised
by FWO's legacy organisation attribute, and any leaf whose issuer is the
internal CA, so that FWO-issued certificates stay renewable.

If an installed certificate or private key is present but cannot be parsed, the
installer stops and names the file instead of guessing. The usual cause is a
passphrase-protected private key, which FWO cannot read and therefore cannot
recognise as customer-managed; without this check the endpoint would silently
be repointed at an FWO-issued certificate. Remove the passphrase, or set
`internalca_issue_apache_certificate: false` to keep the role away from that
identity entirely.

If the leaf, intermediate bundle, and configured root do not form one chain, the
installer stops before changing the Apache vhost. An `unable to get local issuer
certificate` error from a client can therefore also mean that Apache was not
given the required intermediate bundle; it does not necessarily mean that the
client is missing the root CA.

Apache server identities are stored at
`/usr/local/fworch/etc/secrets/apache/server.{crt,key}`. The private
key is `0640` and owned by root. Client identities are stored in
`/usr/local/fworch/etc/secrets/client/client.{crt,key}` and are readable
only by the FWO service account.

On upgrade, the role reuses the existing CA and issued identities. Issued
certificates are valid for `internalca_certificate_validity_days` (825) and are
re-signed automatically by any installer run that happens within
`internalca_renewal_threshold_days` (30) of expiry, so a host that is upgraded
at least once a year never expires.

An identity is also re-signed, whatever its remaining validity, as soon as it
stops covering a name the installation asks it for - an endpoint renamed in
`inventory/hosts.yml`, an `fwo_endpoint_hostname` set or changed, or an upgrade
to a release that derives an endpoint name differently. Expiry alone would not
notice any of these: the certificate is still valid, only for the wrong name, and
every FWO client verifying that endpoint would fail on a host name mismatch until
the certificate was removed by hand. The integration tests assert the same
property from the other side, checking the served certificate against the
endpoints `fworch.json` and the inventory actually address.

To rotate a certificate ahead of the renewal schedule without renaming anything,
remove the affected certificate from
`/usr/local/fworch/etc/secrets/ca/issued/<host>/` and rerun the installer.

To replace the internal CA and every FWO-managed identity in one installer run, use
the one-shot reset switch during an upgrade:

```
./scripts/run-playbook-with-sudo.sh site.yml \
  -e installation_mode=upgrade \
  -e internalca_reset_certificates=true
```

The switch creates a new CA key and certificate, new client identities on every FWO
host, and new Apache and OpenLDAP server identities wherever FWO manages them. It
also replaces the self-signed Apache and OpenLDAP certificates installed by FWO
versions before 9.5.0. A matching certificate/key pair from any other issuer is
customer-managed and is left unchanged; its configured root and intermediate bundle
remain in use as well. An enabled maintenance vhost is repointed from the historical
`/etc/apache2/ssl/server.*` paths to the reconciled identity before Apache reloads.

Run the complete `site.yml` upgrade against the full installation rather than limiting
the run by host or tag, because every participating host must receive the new trust
anchor and identities together. Do not put `internalca_reset_certificates: true` in
`/etc/fworch/fwo-install-settings.yml`: it is an imperative switch, and leaving it
enabled rotates the CA again on every upgrade. The installer refuses a run that finds
it in that file and names the `-e` form instead. Browsers and external trust stores do
not learn the new anchor automatically; replace their previous
`fworch Internal CA ...` certificate with the newly displayed public CA after the run.

Plan the run as a maintenance window. The new trust anchor is installed on every host
early in the run, while the endpoints still serve the leaves the old CA signed, and
internal TLS between FWO components only settles again once the run has reissued those
leaves and restarted their consumers. The maintenance web site covers this for browser
traffic; scheduled importers and API clients see failing handshakes until the run
finishes.

If a reset run is interrupted - an unreachable host, an aborted preflight - rerun the
same command **with the switch still set**. The installer records the subject of every
CA it retires under `/usr/local/fworch/etc/secrets/ca/retired-ca-names`, so a host that
missed the reset is still recognised as carrying an FWO-issued identity and is brought
onto the new CA on its next run, with or without the switch. Only an internal CA that
could not be read at all leaves that record incomplete, because there is then no subject
to record; the run says so explicitly. Every host in that run is still reissued, but a
host it did not reach keeps a leaf whose issuer a later upgrade cannot place, and its
Apache and OpenLDAP identities would be taken for administrator-managed certificates
there - move both out of the paths the installed vhost and
`/etc/ldap/certs/<domain>/` (`/etc/openldap/certs/<domain>/` on RedHat) name, so FWO
issues them again.

Every installer run checks how much validity the CA itself has left, because no
issued certificate can outlive the CA that signed it. Once the CA has less time
remaining than `internalca_certificate_validity_days` (825), newly issued
certificates are silently cut short to the CA's expiry date, and the "upgrade at
least once a year and nothing expires" property no longer holds. The run then
prints a warning. Within `internalca_ca_expiry_critical_days` (90) of expiry it
stops instead, since anything it issued would be near-worthless; set
`internalca_ignore_ca_expiry: true` to complete a run anyway, which does not make
the certificates any more valid. `internalca_ca_expiry_warning_days` (365) adds a
second, earlier warning threshold independent of the leaf validity.

FWO's shared GraphQL and middleware clients validate servers against the CA
configured as `tls_ca_certificate`. Ensure that `api_uri`, `middleware_uri`,
and `ui_hostname` resolve to a name or IP contained in the certificates' SANs.
The defaults cover the inventory host names, `localhost`, `127.0.0.1`, and the
FWO endpoint defaults; distributed deployments should set these inventory
values to their real DNS names before installation.

On a UI host the certificate also covers the names Apache serves as
`ServerName` and `ServerAlias`, taken from `ui_server_name` and
`ui_server_alias`. `ui_server_alias` may list several space-separated names,
and each gets its own SAN entry. The demo values these two variables ship with
are listed in `internalca_placeholder_host_names` and are deliberately left out
of the certificate, so an installation that never replaced them does not assert
a name it has no claim to. Replace them with the real names before installing,
or extend that list if your installation carries other placeholders.

Scripts that talk to the API must present the client identity, for example:

```
curl --request POST \
    --cert /etc/fworch/secrets/client/client.crt \
    --key /etc/fworch/secrets/client/client.key \
    --cacert /etc/fworch/fworch-trust-bundle.crt \
    --url https://localhost:9443/api/v1/graphql \
    --header 'content-type: application/json' \
    --data '{"query":"query { management {mgm_name} }"}'
```

The client private key is readable only by the FWO service account, so run such
commands as that user or as root.

## Trusting the internal CA in a browser

A browser opening the FWO UI has to trust the internal CA, unless that host serves an
administrator-managed certificate whose issuer the browser already trusts. Import the
public CA certificate every host carries at `/etc/fworch/fworch-internal-ca.crt`
(`internalca_ca_certificate_local`) - never the issuer copy under
`/usr/local/fworch/etc/secrets/ca/`, which sits beside the private key. In Firefox:
**Settings → Privacy & Security → Certificates → View Certificates → Authorities →
Import**, then tick *Trust this CA to identify websites*. Where an endpoint serves an
administrator-managed certificate, trusting the internal CA is necessary but not
sufficient: that certificate's own issuing CA has to be trusted in addition.

**A stale anchor from an earlier installation cannot be clicked through.** Every FWO
installation created before 9.5.0 issued its CA under the same subject,
`CN=fworch Internal CA`. A browser still holding such an anchor - from a test system,
or from an installation that has since been rebuilt - selects it by issuer name for the
new server certificate, finds the signature does not verify, and reports

```
SEC_ERROR_BAD_SIGNATURE - Peer's certificate has an invalid signature
```

Firefox offers no *accept the risk* bypass for a bad signature, so the page stays
unreachable until the old authority is removed: **View Certificates → Authorities**,
delete every `fworch Internal CA` entry, then import the current one. The
per-installation suffix keeps new installations apart from each other, but it cannot
repair an anchor that a browser has already imported.

## Importing the client certificate into Firefox

Firefox imports client identities from a PKCS#12 file. Run the following as
root or as the FWO service account in the client certificate directory. OpenSSL
prompts for an export password; retain it because Firefox requests it during
the import.

This exports **the host's own client identity** - the same one the middleware, the UI
and the importer present to the API. A browser holding it cannot be told apart from the
platform in the API logs, and it cannot be revoked without revoking the platform's own
access. Prefer issuing a separate client certificate from the internal CA for a person,
and use the export below only as a stop-gap.

```
cd /usr/local/fworch/etc/secrets/client
umask 077
openssl pkcs12 -export \
  -out client.p12 \
  -inkey client.key \
  -in client.crt
# after the file has been transferred through an approved secure channel
rm client.p12
```

`umask 077` matters: without it `client.p12` is created world-readable within its
directory, and the export password is the only thing protecting the private key.

Import `client.p12` from Firefox's **Settings → Privacy & Security →
Certificates → View Certificates → Your Certificates → Import**.

## Private key requirement

FWO restarts Apache during installation and upgrades, and Apache must also start unattended after a host reboot. Therefore, `server.key` must be an unencrypted private key: Apache cannot prompt for a passphrase during these operations.

Keep the original encrypted key in a secure backup location, then provide Apache with an unencrypted copy owned by `root:root` and mode `0640`. Do not store the key passphrase in FWO configuration or Ansible variables. The installer checks this requirement before restarting Apache and stops with remediation guidance when an existing key cannot be read without a passphrase.

After the change restart apache2

The Guardicore provisioning scripts load these three TLS paths from the local
`fworch.json`. When they run on another host, pass `--fwo-ca-cert`,
`--fwo-client-cert`, and `--fwo-client-key` explicitly. The certificate and key
options must always be supplied together.
