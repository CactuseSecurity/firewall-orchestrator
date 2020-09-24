# Documentation Firewall Orchestrator

## architecture

###
Edit architecture diagram at https://xfer.cactus.de/index.php/f/18376

## Product port list

    tim@ubu204test:/var/log/fworch$ sudo netstat -tulpen
    Active Internet connections (only servers)
    Proto Recv-Q Send-Q Local Address           Foreign Address         State       User       Inode      PID/Program name    
    tcp        0      0 127.0.0.1:5000          0.0.0.0:*               LISTEN      60320      135908     32792/FWO           
    tcp        0      0 127.0.0.1:5001          0.0.0.0:*               LISTEN      60320      135890     32792/FWO           
    tcp        0      0 0.0.0.0:8080            0.0.0.0:*               LISTEN      0          78103      17565/graphql-engin 
    tcp        0      0 127.0.0.53:53           0.0.0.0:*               LISTEN      101        19727      631/systemd-resolve 
    tcp        0      0 0.0.0.0:22              0.0.0.0:*               LISTEN      0          21947      689/sshd: /usr/sbin 
    tcp        0      0 127.0.0.1:8888          0.0.0.0:*               LISTEN      60320      56337      8577/FWO_Auth_Serve 
    tcp        0      0 127.0.0.1:5432          0.0.0.0:*               LISTEN      112        35891      5196/postgres       
    tcp        0      0 0.0.0.0:636             0.0.0.0:*               LISTEN      0          60655      9200/slapd          
    tcp6       0      0 ::1:5000                :::*                    LISTEN      60320      135909     32792/FWO           
    tcp6       0      0 ::1:5001                :::*                    LISTEN      60320      135906     32792/FWO           
    tcp6       0      0 :::80                   :::*                    LISTEN      0          145701     33813/apache2       
    tcp6       0      0 :::22                   :::*                    LISTEN      0          21958      689/sshd: /usr/sbin 
    tcp6       0      0 :::443                  :::*                    LISTEN      0          145709     33813/apache2       
    tcp6       0      0 :::9443                 :::*                    LISTEN      0          145705     33813/apache2       
    tcp6       0      0 :::636                  :::*                    LISTEN      0          60656      9200/slapd          
    udp        0      0 127.0.0.53:53           0.0.0.0:*                           101        19726      631/systemd-resolve 
    udp        0      0 10.0.2.15:68            0.0.0.0:*                           100        1688204    629/systemd-network 
    tim@ubu204test:/var/log/fworch$ 
