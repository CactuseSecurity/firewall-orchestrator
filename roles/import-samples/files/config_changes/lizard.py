# Changes the word 'wizard' to 'lizard' in fortigate.cfg
# Created by alf

fin = open("/home/isosample/sample-configs/fortinet_demo/fortigate.cfg", "rt")
data = fin.read()
data = data.replace('wizard', 'lizard')
fin.close()
fout = open("/home/isosample/sample-configs/fortinet_demo/fortigate.cfg", "wt")
fout.write(data)
fout.close()
