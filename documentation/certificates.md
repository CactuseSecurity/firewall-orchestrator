# Certificates in FWO

The expected paths for keys and certificates are /etc/apache2/ssl/server.key and /etc/apache2/ssl/server.crt respectively. If you want to change them, use these names and paths. Make sure server.key has these permissions

```
-rw-r----- 1 root root
```

## Private key requirement

FWO restarts Apache during installation and upgrades, and Apache must also start unattended after a host reboot. Therefore, `server.key` must be an unencrypted private key: Apache cannot prompt for a passphrase during these operations.

Keep the original encrypted key in a secure backup location, then provide Apache with an unencrypted copy owned by `root:root` and mode `0640`. Do not store the key passphrase in FWO configuration or Ansible variables. The installer checks this requirement before restarting Apache and stops with remediation guidance when an existing key cannot be read without a passphrase.

After the change restart apache2

```
 sudo systemctl restart apache2
```


## Change Roor Certificate

Copy root cert to

```
/usr/local/share/ca-certificates/
```

and update

```
sudo update-ca-certificates
```
