```
sudo apt install cloc
cat cloc-fwo.sh 
    #!/usr/bin/env bash
    git clone --depth 1 "$1" temp-linecount-repo && printf "('temp-linecount-repo' will be deleted automatically)\n\n\n" && cloc temp-linecount-repo --exclude-ext=C && rm -rf temp-linecount-repo

chmod 755 cloc-fwo.sh 
date
    Wed Dec  9 18:22:59 UTC 2020

./cloc-fwo.sh ssh://git@github.com/tpurschke/firewall-orchestrator.git
    Cloning into 'temp-linecount-repo'...
    remote: Enumerating objects: 872, done.
    remote: Counting objects: 100% (872/872), done.
    remote: Compressing objects: 100% (785/785), done.
    remote: Total 872 (delta 120), reused 381 (delta 42), pack-reused 0
    Receiving objects: 100% (872/872), 2.65 MiB | 1.24 MiB/s, done.
    Resolving deltas: 100% (120/120), done.
    ('temp-linecount-repo' will be deleted automatically)


         630 text files.
         621 unique files.                                          
         112 files ignored.

    github.com/AlDanial/cloc v 1.74  T=1.41 s (371.3 files/s, 61839.1 lines/s)
    -------------------------------------------------------------------------------
    Language                     files          blank        comment           code
    -------------------------------------------------------------------------------
    C++                              1              0              0          25894
    PHP                             95            615            810          12728
    Perl                            15           1054           1927          10315
    SQL                             31            530           2286           7445
    YAML                            71            584            348           5689
    C#                              98            914            460           5026
    Markdown                        72            879              0           2665
    Python                          16            330            274           2008
    GraphQL                         67             66            113           1867
    Bourne Shell                    17             66             82            557
    JavaScript                       4             71             57            330
    CSS                              6             54             30            260
    JSON                             9              1              0            193
    MSBuild script                  14             49              0            180
    Razor                            3             10              0            123
    HTML                             1              2              0             29
    ANTLR Grammar                    1              0              1              4
    XML                              1              0              0              1
    -------------------------------------------------------------------------------
    SUM:                           522           5225           6388          75314
    -------------------------------------------------------------------------------
tim@acantha:~/dev/tmp$ date
Wed 30 Jun 2021 05:11:49 PM CEST
tim@acantha:~/dev/tmp$ cloc-fwo.sh https://github.com/CactuseSecurity/firewall-orchestrator.git
Cloning into 'temp-linecount-repo'...
remote: Enumerating objects: 904, done.
remote: Counting objects: 100% (904/904), done.
remote: Compressing objects: 100% (820/820), done.
remote: Total 904 (delta 125), reused 428 (delta 40), pack-reused 0
Receiving objects: 100% (904/904), 1.98 MiB | 8.10 MiB/s, done.
Resolving deltas: 100% (125/125), done.
('temp-linecount-repo' will be deleted automatically)


     738 text files.
     716 unique files.                                          
     161 files ignored.

github.com/AlDanial/cloc v 1.82  T=0.28 s (2080.3 files/s, 208752.4 lines/s)
-------------------------------------------------------------------------------
Language                     files          blank        comment           code
-------------------------------------------------------------------------------
Perl                            14           1012           1827          10110
YAML                            82            676            336           8913
SQL                             62            703           2568           8651
C#                             124           1467            757           8255
Markdown                        99           1180              0           3694
GraphQL                         97             74            132           2586
Python                          18            298            211           1752
Razor                           41             58              6           1164
Bourne Shell                    17             63             81            541
SVG                              1              0              3            540
JSON                             8              3              0            252
CSS                              4             27             11            152
MSBuild script                  10             31              0            127
HTML                             1              2              0             29
ANTLR Grammar                    1              0              1              4
JavaScript                       1              0              0              3
XML                              1              0              0              1
-------------------------------------------------------------------------------
SUM:                           581           5594           5933          46774
-------------------------------------------------------------------------------
tim@acantha:~/dev/tmp$
```
