#!/bin/bash


if [ -e /usr/bin/docker ]
then
	if [ "$(docker ps -a -q)" != "" ]
	then
		docker stop "$(docker ps -a -q)"
		docker rm "$(docker ps -a -q)"
	fi
fi
