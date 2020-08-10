#!/bin/bash

if [ -e /usr/bin/docker ]
then
	if [ "$(docker ps -a -q)" != "" ]
	then
		# TODO: find out why the following does not work on debian (using backtick notation for the time being):
		# docker stop "$(docker ps -a -q)"
		# docker rm "$(docker ps -a -q)"

		docker stop `docker ps -a -q` 
		docker rm `docker ps -a -q`
	fi
fi
