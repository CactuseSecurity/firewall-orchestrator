# python debugging

importer files end up in different directories during installation process (not the same as in the source/installer code). For debugging in order use something like:

`sudo ln -s /home/tim/dev/tpur-fwo-june/firewall-orchestrator/roles/importer/files/importer /usr/local/fworch/importer`

or the following in python code

`sys.path.append(r"/home/tim/dev/tpur-fwo-june/firewall-orchestrator/roles/importer/files/importer")`
