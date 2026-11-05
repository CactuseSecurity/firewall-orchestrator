# How Hasura is installed

The installer runs the Hasura GraphQL engine as a Podman container on every host in the
`apiserver` group. Docker is not used any more (it was replaced by Podman in 9.0.16).

## Roles

- `roles/podman` installs the Podman runtime and disables the rootless Podman user socket.
- `roles/api` (`tasks/hasura-install.yml`) installs and starts Hasura and applies the
  metadata. `site.yml` runs `podman` before `api`.

## Steps of `roles/api/tasks/hasura-install.yml`

1. Download the Hasura CLI (`api_hasura_version`) to `{{ fworch_home }}/api/bin/hasura` and
   verify it against `api_hasura_cli_sha256`.
2. Write the Hasura environment (database URL, admin secret, JWT secret, proxy settings) to
   `{{ fworch_secrets_dir }}/hasura.env` (`api_env_file`). The variables are described in
   `hasura-documentation.md`.
3. Pull the image `api_hasura_image`, pinned by `api_hasura_image_digest`, and create the
   container `fworch-api` (`api_container_name`) with host networking, so Hasura listens on
   `api_local_listening_ip_address:api_port` (127.0.0.1:8080).
4. Generate a systemd unit for the container (`podman generate systemd --new`) and install it
   as `/etc/systemd/system/fworch-hasura-api.service` (`api_service_name`). Startup ordering
   and the restart behaviour are added as a drop-in in `fworch-hasura-api.service.d`, so they
   survive the regeneration of the unit.
5. Start the service, wait for the port, check that the running server reports
   `api_hasura_version`, and import the metadata from `roles/api/files/replace_metadata.json`
   through the metadata API.

The pins (`api_hasura_version`, `api_hasura_image_digest`, `api_hasura_cli_sha256`) live in
`inventory/group_vars/apiserver.yml` and have to be updated together.

## Upgrades

On an upgrade the container is recreated from the configured image. If the Hasura CLI or the
image cannot be downloaded (e.g. no access to GitHub or docker.io), the installer keeps the
installed container and upgrades the metadata only. `-e api_keep_existing_hasura_on_upgrade=true`
requests this explicitly.

## Operating the container

```console
sudo systemctl status fworch-hasura-api
sudo systemctl restart fworch-hasura-api
sudo podman ps --all
sudo podman logs fworch-api
```

The Hasura console is served on the local endpoint (`http://127.0.0.1:8080/console`) and
needs the admin secret from `{{ fworch_secrets_dir }}/hasura_admin_pwd`. To work with the
metadata by hand, use the Hasura CLI against the same endpoint:

```console
hasura init --directory DIR --endpoint http://127.0.0.1:8080 --admin-secret XXX
cd DIR
hasura metadata export
```
