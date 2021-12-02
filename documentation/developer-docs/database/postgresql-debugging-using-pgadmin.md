
# to debug postgresql stored procedures in plgsql


## using pgadmin

the following was tested with Ubuntu 20.04


- on the backend (db-server) side: 
  - install debug package (here for postgresql v12)

          sudo apt-get install postgresql-12-pldebugger

  - edit postgresql.conf (e.g. /etc/postgresql/12/main/postgresql.conf) to allow debugging and add the following line:

          shared_preload_libraries = 'plugin_debugger'


  - restart postresql service

          sudo systemctl restart postgresql

- on the clinet side:
  - install pgadmin

          sudo apt install pgadmin4

  - to add the debug extension start pgadmin and run in query editor 

          CREATE EXTENSION pldbgapi;

  - select a stored procedure you wish to debug and select Object - Debugging - Debug

## using vscode

see <https://data-nerd.blog/2020/02/06/postgresql-extension-for-vscode/>

but there are currently no debugging options available
