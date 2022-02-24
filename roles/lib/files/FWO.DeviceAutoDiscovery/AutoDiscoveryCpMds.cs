using FWO.Api.Data;
using FWO.ApiClient;
using FWO.Logging;
using FWO.Rest.Client;
using System.Net;
using RestSharp;
using FWO.Api.Data;
using FWO.ApiClient;
using FWO.Logging;
using FWO.Rest.Client;

/*
proxy_string = { "http"  : args.proxy, "https" : args.proxy }
offset = 0
use_object_dictionary = 'false'
base_url = 'https://' + args.hostname + ':' + args.port + '/web_api/'
ssl_verification = getter.set_ssl_verification(args.ssl)

with open(args.password_file, 'r') as file:
    apiuser_pwd = file.read().replace('\n', '')

xsid = getter.login(args.user, apiuser_pwd, args.hostname, args.port, args.domain, ssl_verification, proxy_string, debug=args.debug)

api_versions = getter.api_call(base_url, 'show-api-versions', {}, xsid, ssl_verification, proxy_string)
api_version = api_versions["current-version"]
api_supported = api_versions["supported-versions"]
v_url = getter.set_api_url(base_url,args.version,api_supported,args.hostname)

v_url = 'https://' + args.hostname + ':' + args.port + '/web_api/'
if args.version != "off":
    v_url += 'v' + args.version + '/'

logger = logging.getLogger(__name__)

xsid = getter.login(args.user, apiuser_pwd, args.hostname, args.port, '', ssl_verification, proxy_string)

if args.debug == "1" or args.debug == "3":
    debug = True
else:
    debug = False

# todo: only show active devices (optionally with a switch)
domains = getter.api_call (v_url, 'show-domains', {}, xsid, ssl_verification, proxy_string)
gw_types = ['simple-gateway', 'simple-cluster', 'CpmiVsClusterNetobj', 'CpmiGatewayPlain', 'CpmiGatewayCluster', 'CpmiVsxClusterNetobj']
parameters =  { "details-level" : "full" }

if domains['total']== 0:
    logging.debug ("no domains found, adding dummy domain.")
    domains['objects'].append ({ "name": "", "uid": "" }) 

    # fetching gateways for non-MDS management:
    obj = domains['objects'][0]
    obj['gateways'] = getter.api_call(v_url, 'show-gateways-and-servers', parameters, xsid, ssl_verification, proxy_string)

    if 'objects' in obj['gateways']:
        for gw in obj['gateways']['objects']:
            if 'type' in gw and gw['type'] in gw_types and 'policy' in gw:
                if 'access-policy-installed' in gw['policy'] and gw['policy']['access-policy-installed'] and "access-policy-name" in gw['policy']:
                    logging.debug ("standalone mgmt: found gateway " + gw['name'] + " with policy" + gw['policy']['access-policy-name'])
                    gw['package'] = getter.api_call(v_url, 
                        "show-package", 
                        { "name" : gw['policy']['access-policy-name'], "details-level": "full" }, 
                        xsid, ssl_verification, proxy_string)
    else:
        logging.warning ("Standalone WARNING: did not find any gateways in stand-alone management")
    logout_result = getter.api_call(v_url, 'logout', {}, xsid, ssl_verification, proxy_string)

else: # visit each domain and fetch layers
    for obj in domains['objects']:
        domain_name = obj['name']
        logging.debug ("MDS: searchig in domain " + domain_name)
        xsid = getter.login(args.user, apiuser_pwd, args.hostname, args.port, domain_name, ssl_verification, proxy_string)
        obj['gateways'] = getter.api_call(v_url, 'show-gateways-and-servers', parameters, xsid, ssl_verification, proxy_string)
        if 'objects' in obj['gateways']:
            for gw in obj['gateways']['objects']:
                if 'type' in gw and gw['type'] in gw_types and 'policy' in gw:
                    if 'access-policy-installed' in gw['policy'] and gw['policy']['access-policy-installed'] and "access-policy-name" in gw['policy']:
                        api_call_str = "show-package name " + gw['policy']['access-policy-name'] + ", logged in to domain " + domain_name
                        logging.debug ("MDS: found gateway " + gw['name'] + " with policy: " + gw['policy']['access-policy-name'])
                        logging.debug ("api call: " + api_call_str)
                        try:
                            tmp_pkg_name = getter.api_call(v_url, 'show-package', { "name" : gw['policy']['access-policy-name'], "details-level": "full" }, xsid, ssl_verification, proxy_string)
                        except:
                            tmp_pkg_name = "ERROR while trying to get package " + gw['policy']['access-policy-name']
                        gw['package'] = tmp_pkg_name
        else:
            logging.warning ("Domain-WARNING: did not find any gateways in domain " + obj['name'])
        logout_result = getter.api_call(v_url, 'logout', {}, xsid, ssl_verification, proxy_string)

# now collect only relevant data and copy to new dict
domains_essential = []
for obj in domains['objects']:
    domain = { 'name':  obj['name'], 'uid': obj['uid'] }
    gateways = []
    domain['gateways'] = gateways
    if 'objects' in obj['gateways']:
        for gw in obj['gateways']['objects']:
            if 'policy' in gw and 'access-policy-name' in  gw['policy']:
                gateway = { "name": gw['name'], "uid": gw['uid'], "access-policy-name": gw['policy']['access-policy-name'] }
                layers = []
                if 'package' in gw:
                    if 'access-layers' in gw['package']:
                        found_domain_layer = False
                        for ly in gw['package']['access-layers']:
                            if 'firewall' in ly and ly['firewall']:
                                if 'parent-layer' in ly:
                                    found_domain_layer = True 
                        for ly in gw['package']['access-layers']:
                            if 'firewall' in ly and ly['firewall']:
                                if 'parent-layer' in ly:
                                    layer = { "name": ly['name'], "uid": ly['uid'], "type": "domain-layer", "parent-layer": ly['parent-layer'] }
                                elif domains['total']==0:
                                    layer = { "name": ly['name'], "uid": ly['uid'], "type": "local-layer" }
                                elif found_domain_layer:
                                    layer = { "name": ly['name'], "uid": ly['uid'], "type": "global-layer" }
                                else:   # in domain context, but no global layer exists
                                    layer = { "name": ly['name'], "uid": ly['uid'], "type": "stand-alone-layer" }
                                layers.append(layer)
                gateway['layers'] = layers
                gateways.append(gateway)
            domain['gateways'] = gateways
    domains_essential.append(domain)
devices = {"domains": domains_essential }


##### output ########
if args.format == 'json':
    print (json.dumps(devices, indent=3))

elif args.format == 'table':
    # compact print in FWO UI input format
    colsize_number = 35
    colsize = "{:"+str(colsize_number)+"}"
    table = ""
    heading_list = ["Domain/Management", "Gateway", "Policy String"]

    # add table header:
    for heading in heading_list:
        table += colsize.format(heading)
    table += "\n"
    x = 0
    while x <  len(heading_list) * colsize_number:
        table += '-'
        x += 1
    table += "\n"

    # print one gateway/policy per line
    for dom in devices['domains']:
        if 'gateways' in dom:
            for gw in dom['gateways']:
                table += colsize.format(dom["name"])
                table += colsize.format(gw['name'])
                if 'layers' in gw:
                    found_domain_layer = False
                    layer_string = '<undefined>'
                    for ly in gw['layers']:
                        if 'parent-layer' in ly:
                            found_domain_layer = True 
                    for ly in gw['layers']:
                        if ly['type'] == 'stand-alone-layer' or ly['type'] == 'local-layer':
                            layer_string = ly["name"]
                        elif found_domain_layer and ly['type'] == 'domain-layer':
                            domain_layer = ly['name']
                        elif found_domain_layer and ly['type'] == 'global-layer':
                            global_layer = ly['name']
                        else:
                            logging.warning ("found unknown layer type")
                    if found_domain_layer:
                        layer_string = global_layer + '/' + domain_layer
                    table += colsize.format(layer_string)
                table += "\n"
        else:
            table += colsize.format(dom["name"])
        table += "\n"  # empty line between domains for readability

    print (table)

plan:
    - we probably need to add a new class for checkpoint in parallel to the Adom class? Use inheritance and a base class?



*/
namespace FWO.DeviceAutoDiscovery
{
    public class AutoDiscoveryCpMds : AutoDiscoveryBase
    {
        public AutoDiscoveryCpMds(Management mgm, APIConnection apiConn) : base(mgm, apiConn) { }

        private static bool containsDomainLayer(List<CpAccessLayer> layers)
        {
            foreach (CpAccessLayer layer in layers)
            {
                if (layer.ParentLayer != "")
                    return true;
            }
            return false;
        }

        public override async Task<List<Management>> Run()
        {
            List<Management> discoveredDevices = new List<Management>();
            string ManagementType = "";
            Log.WriteAudit("Autodiscovery", $"starting discovery for {superManagement.Name} (id={superManagement.Id})");

            if (superManagement.DeviceType.Name == "Check Point")
            {
                // List<Domain> customDomains = new List<Domain>();
                Log.WriteDebug("Autodiscovery", $"discovering CP domains & gateways");

                CheckPointClient restClientCP = new CheckPointClient(superManagement);

                RestResponse<CpSessionAuthInfo> sessionResponse = await restClientCP.AuthenticateUser(superManagement.ImportUser, superManagement.Password, superManagement.ConfigPath);
                if (sessionResponse.StatusCode == HttpStatusCode.OK && sessionResponse.IsSuccessful && sessionResponse?.Data?.SessionId != "")
                {
                    // need to serialize here
                    string? sessionId = sessionResponse?.Data?.SessionId;
                    Log.WriteDebug("Autodiscovery", $"successful CP Manager login, got SessionID: {sessionId}");
                    // need to use @ verbatim identifier for special chars in sessionId
                    RestResponse<CpDomainHelper> domainResponse = await restClientCP.GetDomains(@sessionId);
                    if (domainResponse.StatusCode == HttpStatusCode.OK && domainResponse.IsSuccessful)
                    {
                        List<Domain> domainList = domainResponse?.Data?.DomainList;
                        if (domainList.Count == 0)
                        {
                            Log.WriteDebug("Autodiscovery", $"found no domains - assuming this is a standard management, adding dummy domain with empty name");
                            domainList.Add(new Domain { Name = "" });
                            ManagementType = "stand-alone";
                        }
                        else
                            ManagementType = "MDS";

                        foreach (Domain domain in domainList)
                        {
                            Log.WriteDebug("Autodiscovery", $"found domain '{domain.Name}'");

                            Management currentManagement = new Management
                            {
                                Name = superManagement.Name + "__" + domain.Name,
                                ImporterHostname = superManagement.ImporterHostname,
                                Hostname = superManagement.Hostname,
                                ImportUser = superManagement.ImportUser,
                                PrivateKey = superManagement.PrivateKey,
                                Password = superManagement.Password,
                                Port = superManagement.Port,
                                ImportDisabled = false,
                                ForceInitialImport = true,
                                HideInUi = false,
                                ConfigPath = domain.Name,
                                DebugLevel = superManagement.DebugLevel,
                                SuperManagerId = superManagement.Id,
                                DeviceType = new DeviceType { Id = 9 },
                                Devices = new Device[] { }
                            };
                            // string domainName
                            // now handling the original management (if it was just a simple management)
                            if (domain.Name == "") // set some settings identical to "superManager", so that no new manager is created
                            {
                                currentManagement.Name = superManagement.Name;
                                currentManagement.ConfigPath = "";
                                currentManagement.SuperManagerId = null;
                                //currentManagement.DeviceType = currentManagement.DeviceType;
                            }

                            RestResponse<CpSessionAuthInfo> sessionResponsePerDomain =
                                    await restClientCP.AuthenticateUser(superManagement.ImportUser, superManagement.Password, domain.Name);

                            if (sessionResponsePerDomain.StatusCode == HttpStatusCode.OK &&
                                sessionResponsePerDomain.IsSuccessful &&
                                sessionResponsePerDomain?.Data?.SessionId != "")
                            {
                                // need to serialize here
                                string? sessionIdPerDomain = sessionResponsePerDomain?.Data?.SessionId;
                                Log.WriteDebug("Autodiscovery", $"successful CP manager login, got SessionID: {sessionId}");
                                List<CpDevice> devList = await restClientCP.GetGateways(@sessionIdPerDomain);

                                // add devices to currentManagement
                                foreach (CpDevice cpDev in devList)
                                {
                                    if (cpDev.Package.CpAccessLayers.Count < 1)
                                    {
                                        // Log.WriteWarning("AutoDiscovery", $"did not find any layers");
                                        continue;
                                    }

                                    if (cpDev.CpDevType != "checkpoint-host")   // leave out the management host?!
                                    {
                                        string globalLayerName = "";
                                        string localLayerName = "";
                                        bool packageContainsDomainLayer = containsDomainLayer(cpDev.Package.CpAccessLayers);
                                        foreach (CpAccessLayer layer in cpDev.Package.CpAccessLayers)
                                        {
                                            if (layer.Type != "access-layer") // only dealing with access layers, ignore the rest
                                                continue;

                                            if (layer.ParentLayer != "")      // this is a domain layer
                                            {
                                                localLayerName = layer.Name;
                                                layer.LayerType = "domain-layer";
                                                Log.WriteDebug("Autodiscovery", $"found domain layer with link to parent layer '{layer.ParentLayer}'");
                                            }
                                            else if (ManagementType == "stand-alone")
                                            {
                                                localLayerName = layer.Name;
                                                layer.LayerType = "local-layer";
                                                Log.WriteDebug("Autodiscovery", $"found stand-alone layer '{layer.Name}'");
                                            }
                                            else if (packageContainsDomainLayer)
                                            {   // this must the be global layer
                                                layer.LayerType = "global-layer";
                                                globalLayerName = layer.Name;
                                                Log.WriteDebug("Autodiscovery", $"found global layer '{layer.Name}'");
                                            }
                                            else
                                            { // in domain context, but no global layer exists
                                                layer.LayerType = "stand-alone-layer";
                                                Log.WriteDebug("Autodiscovery", $"found stand-alone layer in domain context '{layer.Name}'");
                                            }
                                            // TODO: this will contstantly overwrite local layer name if more than one exists, the last one wins!
                                        }
                                        Device dev = new Device
                                        {
                                            Name = cpDev.Name,
                                            LocalRulebase = localLayerName,
                                            GlobalRulebase = globalLayerName,
                                            Package = cpDev.Package.Name,
                                            DeviceType = new DeviceType { Id = 9 } // CheckPoint GW
                                        };
                                        currentManagement.Devices = currentManagement.Devices.Append(dev).ToArray();
                                    }
                                    sessionResponsePerDomain = await restClientCP.DeAuthenticateUser(@sessionIdPerDomain);
                                }
                            }
                            discoveredDevices.Add(currentManagement);
                        }
                        Log.WriteDebug("Autodiscovery", $"found a total of {domainList.Count} domains");
                    }
                    else
                        Log.WriteWarning("AutoDiscovery", $"error while getting domain list: {domainResponse.ErrorMessage}");

                    sessionResponse = await restClientCP.DeAuthenticateUser(@sessionId);
                    if (sessionResponse.StatusCode == HttpStatusCode.OK)
                        Log.WriteDebug("Autodiscovery", $"successful CP Manager logout");
                    else
                        Log.WriteWarning("Autodiscovery", $"error while logging out from CP Manager: {sessionResponse.ErrorMessage}");
                }
                else
                {
                    string errorTxt = $"error while logging in to CP Manager: {sessionResponse.ErrorMessage} ";
                    if (sessionResponse?.Data?.SessionId == "")
                        errorTxt += "could not authenticate to CP manager - got empty session ID";
                    Log.WriteWarning("AutoDiscovery", errorTxt);
                    throw new Exception(errorTxt);
                }
            }
            return await GetDeltas(discoveredDevices);
        }
    }
}
