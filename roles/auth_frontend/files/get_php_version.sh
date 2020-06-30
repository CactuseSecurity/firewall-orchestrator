#!/bin/bash
ver=$(php --version | head -1 | cut -d " " -f 2 | cut -d "-" -f 1)
major_ver=$(echo "$ver" | cut -d "." -f 1)
minor_ver=$(echo "$ver" | cut -d "." -f 2)
echo -n "${major_ver}.${minor_ver}"
