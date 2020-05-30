# Changes the word 'lizard' to 'wizard' in fortigate.cfg
# Created by alf

fin = open("/home/isosample/sample-configs/fortinet_demo/fortigate.cfg", "rt")
data = fin.read()
data = data.replace('lizard', 'wizard')
fin.close()
fout = open("/home/isosample/sample-configs/fortinet_demo/fortigate.cfg", "wt")
fout.write(data)
fout.close()
