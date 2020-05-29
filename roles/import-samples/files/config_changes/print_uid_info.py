# Print all information about an edit to a file
# Doubts: what if "edit x" appears in a comment or sth similar
# Created by alf

def print_uid_info(uid = 72):
    import fnmatch


# Adjust number to the uid you want to print out
# uid = 72
record_flag = 0
fin = open("/home/isosample/sample-configs/fortinet_demo/fortigate.cfg", "rt")
fout = open("/home/isosample/sample-configs/fortinet_demo/deleteme.txt", "wt")
for line in fin:
    if fnmatch.filter([line], '*edit {}*'.format(uid)):
        record_flag = 1
    if record_flag == 1:
        fout.write(line)
        # print(line,end='')
        if fnmatch.filter([line], '*next*'):
            record_flag = 0
fin.close()
fout.close()
