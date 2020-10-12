# all a developer needs to know ;-)

## tools & apps

- jiffy time tracking: android app, see play store
- nextcloud talk app: android app, see play store, alerts via beeps!
- 

## git ettiquete
- changes in documemtation dir can be done directly without pull-request
- all other changes need to be reviewed via pull-request


## avoiding git commits while testing: copy sources without commit 

```shell
tim@acantha:~/bin$ cat fwo-cp-debian.sh 
ssh -p 10222 localhost "rm -rf firewall-orchestrator && mkdir firewall-orchestrator" && scp -r -P 10222 ~/dev/tpur-fwo/firewall-orchestrator/* localhost:firewall-orchestrator
tim@acantha:~/bin$ 
```
