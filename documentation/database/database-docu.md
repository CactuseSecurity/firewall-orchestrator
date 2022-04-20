
# Database instructions

## preparations

- install new machine running latest ubuntu or debian
- allow for disk space triple the size of the DB (e.g. 500 GB for a 140 GB DB) - especially needed for the vacuum step at the end
- allow for 16 GB of RAM and 4 CPUs
- install basics: apt install sudo net-tools ansible git ssh
- add user to sudoers (e.g. bei editing /etc/group) and reboot
- setup ssh ssh-keygen -b 4096 cat .ssh/id_rsa.pub >>.ssh/authorized_keys
- make ssh available in virtual box: choose NAT networking - advanced - add port forwarding for ssh from port e.g. 2222 to 22 (see <http://ask.xmodulo.com/access-nat-guest-from-host-virtualbox.html>)
- ssh to guest
- cp ssh pub key to user's authorized_keys on repo server
- get repo from git:
~~~console
  github: git clone git@github.com:CactuseSecurity/firewall-orchestrator.git
~~~
- install product:see <https://github.com/CactuseSecurity/firewall-orchestrator/blob/master/documentation/installer/server-install.md>
- allow for disk space triple the size of the DB (e.g. 500 GB for a 140 GB DB) - especially needed for the vacuum step at the end
- restore db backup:
~~~console
psql -c "create database fworchdb"
psql -d fworchdb -f "database-dump"
~~~
### Optimization

- allow for disk space triple the size of the DB (e.g. 500 GB for a 140 GB DB) - especially needed for the vacuum step at the end
- transform database tables to auto cascade thru all tables when deleting unwanted managements: see database/sql/fworch-change-to-delete-cascade.sql
- start this shell script as a dbadmin to remove all unwanted managements: see scripts/remove-2-year-old-devices.sh
- stop all db activity before the next command: time psql -d fworchdb -c "vacuum full verbose"

### wait for connection termination

```
SELECT pg_terminate_backend(pg_stat_activity.pid)
FROM pg_stat_activity
WHERE pg_stat_activity.datname = '[Database to copy]'
AND pid <> pg_backend_pid();
```

### quick and dirty debugging

```
...$ sudo su - postgres
postgres-# \c fworchdb
fworchdb-# \dt
fworchdb-# \x
fworchdb-# SELECT * FROM my_table;
```
