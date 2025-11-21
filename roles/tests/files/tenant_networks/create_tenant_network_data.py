from random import randint

from netaddr import IPNetwork


def create_tenant_test_data(tenantTopIpNet, numberOfTenantNetworks, tenantId):
	code = ""
	i = 0
	mask = 0
	tenantTopIpNetTypecasted = IPNetwork(tenantTopIpNet)
	numberOfPossibleIPs = tenantTopIpNetTypecasted.size

	while i< numberOfTenantNetworks:
		i += 1
		mask = 32
		randomIndex = randint(0,numberOfPossibleIPs-1)
		randomIp = tenantTopIpNetTypecasted[randomIndex]
		code += "insert into tenant_network (tenant_id, tenant_net_ip) values ({tenantId}, '{randomIp}/{mask}');\n".format(tenantId=tenantId, randomIp=randomIp, mask=mask)
	
	return code


if __name__ == '__main__':

	print(create_tenant_test_data(IPNetwork('10.0.0.0/12'), 1000, 6))
