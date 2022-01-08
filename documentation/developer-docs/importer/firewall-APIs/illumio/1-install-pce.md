# installing on-prem system

install guide: <https://github.com/johnwesterman/illumio_core>

## download software
- login to illumio support portal and navigate to: <https://support.illumio.com/software/download.html#pce_software/download/21.2.2>
- download core & ui rpms and follow instructions on <https://docs.illumio.com/core/21.2/Content/LandingPages/Guides/pce-install-upgrade.htm>

```console
    /opt/illumio-pce/illumio-pce-env setup --generate-cert
   
  Illumio PCE Runtime Setup (new configuration)

  # The Fully Qualified Domain Name (FQDN) of this PCE.
  # 
  # For example, pce.mycompany.com

  pce_fqdn: illumio.cactus.de

  # Optional message to display to users on the login page to the PCE web console.
  # 
  # Leave blank if you do not want to configure a login banner.

  login_banner: cactus test system

  # The hostname or IP address of the first node of the PCE cluster.

  service_discovery_fqdn: illumio.cactus.de # auto

  # The public IP addresses associated with the PCE FQDN.
  # 
  # The PCE will automatically program firewall rules with these addresses
  # on VENs to allow outbound connections to the PCE.
  # 
  # If cluster load balancing is performed by an L4 load balancer,
  # then these addresses will be the VIP(s) of the virtual servers.

  cluster_public_ips:
    cluster_fqdn:
    - 10.0.2.15 # auto

  # The node type for this PCE node.
  # 
  # Supported values:
  #   core
  #   data0
  #   data1
  #   citus_coordinator
  #   citus_worker
  #   snc0

  node_type: snc0

  datacenter: dc1 # default

  # The port used for HTTPS connections to the PCE.
  # 
  # If cluster load balancing is performed by an L4 load balancer,
  # then the load balancer must be configured to forward this port.

  front_end_https_port: 8443 # default

  # The port used for the PCE web console and API.
  # 
  # If this parameter is not defined (left empty), the PCE will use the
  # port defined for the 'front_end_https_port' parameter.
  # 
  # Illumio security best practice is to configure a separate HTTPS
  # port for the PCE web console and API.

  front_end_management_https_port: 

  # The port used for persistent connections between VENs and the PCE
  # event service.
  # 
  # If cluster load balancing is performed by an L4 load balancer,
  # then the load balancer must be configured to forward this port
  # and its idle connection timeout must be set to 20 minutes.

  front_end_event_service_port: 8444 # default

  # The full path to the RSA private key that matches the public certificate.
  # 
  # The private key must be PEM encoded in PKCS#5 format without a password.

  web_service_private_key: /var/lib/illumio-pce/cert/server.key

  # The full path to a X.509 public certificate used for TLS.
  # 
  # The certificate must match the PCE FQDN.
  # The certificate must support both Server and Client authentication.
  # The file must contain the server certificate (first) and all
  # intermediate CAs needed to establish the chain of trust.
  # The certificates must be PEM encoded.

  web_service_certificate: /var/lib/illumio-pce/cert/server.crt

  # The full path to the trusted root certificate bundle.

  trusted_ca_bundle: /etc/ssl/certs/ca-bundle.crt # default

  # The source email address used when sending email from the PCE.

  email_address: illumio@cactus.de

  # The display name used when sending email from the PCE.

  email_display_name: noreply # default

  # A 16 byte key used to encrypt service discovery traffic.
  # 
  # The key must be base64 encoded and must be the same on all nodes in
  # the cluster.

  service_discovery_encryption_key: ******** # auto

  # The SMTP relay used by the PCE to send email; for example, to send
  # invitations and notifications.
  # 
  # If no port is specified, the default of 587 is used.

  smtp_relay_address: 127.0.0.1:587 # default

  # Specifies the root directory to use for data storage. Defaults to persistent_data_root if not defined.


  # Specifies the root directory to use for data storage. Defaults to persistent_data_root/traffic_datastore if not defined.


  # The output format for audit and traffic events written to syslog.
  # 
  # Supported values:
  #   json
  #   cef
  #   leef

  syslog_event_export_format: json # default

  # Allow weaker ciphers, include CBC, for older clients.  WARNING: The use of this feature decreases the security of the system, and should only be used with full understanding of the ramifications.

  insecure_tls_weak_ciphers_enabled: true # default

Wrote configuration /etc/illumio-pce/runtime_env.yml
```

Check config
```console
[root@illumio ~]# sudo -u ilo-pce illumio-pce-env check
Checking PCE runtime environment.
Warning: Found 1 warning in PCE runtime environment
 1: Maximum possible log usage (8.42G) could eventually exceed 25% of partition size (5.15G).
OK
[root@illumio ~]# 
```

setup up aliases:

    alias ctl='sudo -u ilo-pce /opt/illumio-pce/illumio-pce-ctl'
    alias ctldb='sudo -u ilo-pce /opt/illumio-pce/illumio-pce-db-management'
    alias ctlenv='sudo -u ilo-pce /opt/illumio-pce/illumio-pce-env'

Start service:

```console
[tim@illumio ~]$ sudo -u ilo-pce /usr/bin/illumio-pce-ctl start --runlevel 1
Reading /etc/illumio-pce/runtime_env.yml.
Checking PCE runtime environment.
Warning: Found 1 warning in PCE runtime environment
 1: Maximum possible log usage (8.42G) could eventually exceed 25% of partition size (5.15G).
OK
Starting Illumio Runtime                         STARTING 6.06s
[tim@illumio ~]$ 
             

[tim@illumio ~]$ sudo -u ilo-pce /usr/bin/illumio-pce-ctl status -svw
Checking Illumio Runtime 
...
Illumio Runtime System                           RUNNING [1] 2.33s
[tim@illumio ~]$ 
```
Start master backend and ui

```console
ctldb setup
ctl set-runlevel 5
ctl status -svw
ctl cluster-status
```

Create UI user:

    ctldb create-domain --user-name tmp@cactus.de --full-name 'tim' --org-name 'Cactus'

Create API user:

    ctldb create-domain --user-name apiuser@cactus.de --full-name 'api'


