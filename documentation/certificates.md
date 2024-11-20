# Certificates in FWO

The expected paths for keys and certificates are /etc/apache2/ssl/server.key and /etc/apache2/ssl/server.crt respectivly. If you want to change them, use these names and paths. Make sure server.key has these permissions

```
-rw-r----- 1 root root
```

After the change restart apache2

```
 sudo systemctl restart apache2
```

## Change Root Certificate

Copy root cert to

```
/usr/local/share/ca-certificates/
```

and update

```
sudo update-ca-certificates
```
