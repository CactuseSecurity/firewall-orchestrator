from random import randint

from netaddr import IPNetwork


def createTenantTestData(tenantTopIpNet, numberOfTenantNetworks, tenantId):
	code = ""
	i = 0
	mask = 0
	tenantTopIpNetTypecasted = IPNetwork(tenantTopIpNet)
	# numberOfPossibleIPs = tenantTopIpNet.num_addresses
	numberOfPossibleIPs = tenantTopIpNetTypecasted.size

	while i< numberOfTenantNetworks:
		i += 1
		# mask = randint(28, 32)
		mask = 32
		randomIndex = randint(0,numberOfPossibleIPs-1)
		randomIp = tenantTopIpNetTypecasted[randomIndex]
		code += "insert into tenant_network (tenant_id, tenant_net_ip) values ({tenantId}, '{randomIp}/{mask}');\n".format(tenantId=tenantId, randomIp=randomIp, mask=mask)
	
	return code


if __name__ == '__main__':

	print(createTenantTestData(IPNetwork('10.0.0.0/12'), 1000, 6))
