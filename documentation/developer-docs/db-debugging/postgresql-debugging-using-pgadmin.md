
# to debug postgresql stored procedures in plgsql


## using pgadmin

the following was tested with Ubuntu 20.04

- install pgadmin

      sudo apt install pgadmin4

- install debug package (here for postgresql v12)

      sudo apt-get install postgresql-12-pldebugger

- edit postgresql.conf to allow debugging and add the following line:

      shared_preload_libraries = 'plugin_debugger'


- restart postresql service

      sudo systemctl restart postgresql

- to add the debug extension start pgadmin and run in query editor 

      CREATE EXTENSION pldbgapi;

- select a stored procedure you wish to debug and select Object - Debugging - Debug

## using vscode

see <https://data-nerd.blog/2020/02/06/postgresql-extension-for-vscode/>
