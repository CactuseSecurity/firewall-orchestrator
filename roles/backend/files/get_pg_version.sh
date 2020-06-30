#!/bin/bash
ver=$(psql --version | cut -d " " -f 3)
major_ver=$(echo "$ver" | cut -d "." -f 1)
minor_ver=$(echo "$ver" | cut -d "." -f 2)
# return only major version from pg 10 onwards
if [ $((major_ver * 1)) -gt 9 ]
then
   ver=$major_ver
fi
echo -n "$ver"
