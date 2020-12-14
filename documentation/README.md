# Documentation Firewall Orchestrator

## architecture

###
Edit architecture diagram at https://xfer.cactus.de/index.php/f/18376

## Product port list

    fworchtest@devsrv2:~/firewall-orchestrator$ sudo netstat -tulpen
    Active Internet connections (only servers)
    Proto Recv-Q Send-Q Local Address           Foreign Address         State       User       Inode      PID/Program name    
    tcp        0      0 127.0.0.1:5000          0.0.0.0:*               LISTEN      60320      2783551    456927/FWO.Ui       
    tcp        0      0 127.0.0.1:5001          0.0.0.0:*               LISTEN      60320      2783546    456927/FWO.Ui       
    tcp        0      0 0.0.0.0:8080            0.0.0.0:*               LISTEN      0          2747498    453210/graphql-engi 
    tcp        0      0 127.0.0.53:53           0.0.0.0:*               LISTEN      101        20385      752/systemd-resolve 
    tcp        0      0 0.0.0.0:22              0.0.0.0:*               LISTEN      0          25300      926/sshd: /usr/sbin 
    tcp        0      0 0.0.0.0:60344           0.0.0.0:*               LISTEN      1003       2804048    458979/python3      
    tcp        0      0 127.0.0.1:8888          0.0.0.0:*               LISTEN      60320      2803725    458848/FWO.Middlewa 
    tcp        0      0 127.0.0.1:5432          0.0.0.0:*               LISTEN      113        2678510    445585/postgres     
    tcp        0      0 0.0.0.0:636             0.0.0.0:*               LISTEN      0          2736285    451690/slapd        
    tcp        0      0 0.0.0.0:514             0.0.0.0:*               LISTEN      0          2675207    445202/rsyslogd     
    tcp6       0      0 ::1:5000                :::*                    LISTEN      60320      2783552    456927/FWO.Ui       
    tcp6       0      0 ::1:5001                :::*                    LISTEN      60320      2783550    456927/FWO.Ui       
    tcp6       0      0 :::80                   :::*                    LISTEN      0          2766781    455166/apache2      
    tcp6       0      0 :::22                   :::*                    LISTEN      0          25302      926/sshd: /usr/sbin 
    tcp6       0      0 :::443                  :::*                    LISTEN      0          2766789    455166/apache2      
    tcp6       0      0 :::636                  :::*                    LISTEN      0          2736286    451690/slapd        
    tcp6       0      0 :::514                  :::*                    LISTEN      0          2675208    445202/rsyslogd     
    tcp6       0      0 :::9443                 :::*                    LISTEN      0          2766785    455166/apache2      
    udp        0      0 127.0.0.1:323           0.0.0.0:*                           0          28234      1260/chronyd        
    udp        0      0 127.0.0.53:53           0.0.0.0:*                           101        20384      752/systemd-resolve 
    udp6       0      0 ::1:323                 :::*                                0          28235      1260/chronyd        
    fworchtest@devsrv2:~/firewall-orchestrator$ 
 
