using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Firewall_Orchestrator
{
    class DbConnection
    {
        // Server URL
        private const string ServerURI = "https://demo.itsecorg.de/api/v1/graphql";

        // Http/s Client
        private readonly HttpClient Client;

        public DbConnection()
        {
            // Erlaube alle Zertifikate // ENTFERNEN SOBALD SERVER GÜLTIGES ZERTIFIKAT HAT
            //ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

            // Neuer Http/s Client
            Client = new HttpClient();
        }

        public async Task<string> TestQuery()
        {
            //string ExampleQuery = "{ device { dev_id dev_name stm_dev_typ { dev_typ_name dev_typ_version } management { mgm_id mgm_name} rules(where: { active: { _eq: true}, rule_disabled: { _eq: false} }, order_by: { rule_num: asc}) { rule_num rule_id rule_uid rule_froms { object { obj_name } } rule_tos { object { obj_name } } rule_services { service { svc_name svc_id } } rule_action rule_track } } }";  
            // { management { dev_typ_id hide_in_gui do_not_import force_initial_import importer_hostname last_import_md5_complete_config last_import_md5_objects last_import_md5_rules last_import_md5_users config_path mgm_comment mgm_create mgm_id mgm_name mgm_update ssh_hostname ssh_port ssh_user } }

            // Query aufgebaut wie folgt { "query" : " 'query' ", "variables" : { 'variables' } } mit 'query' für die zu versendende Query und 'variables' für die dazugehörigen Variablen
            string Query = @"{ ""query"": "" 
                query listRules
                {
                    rule(where: { active: { _eq: true}, rule_disabled: { _eq: false} }, order_by: { rule_num: asc}) {
                    rule_num
                    rule_src
                    rule_dst
                    rule_svc
                    rule_action
                    rule_track
                }
            } "", "" variables "" : {} }";

            // Neuer Http-Body der die Query enthält
            StringContent content = new StringContent(Query, Encoding.UTF8);
            // Alle Standard-Header entfernen
            content.Headers.Clear();
            // Inhaltstypheader hinzufügen
            content.Headers.Add("content-type", "application/json");
            // Authorisierungsheader hinzufügen
            content.Headers.Add("x-hasura-admin-secret", "st8chelt1er");
#if DEBUG
            // Zeitmessung Start
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
#endif
            // Http-Packet mit Query und Headern versenden und Antwort des Server empfangen
            HttpResponseMessage response = await Client.PostAsync(ServerURI, content);
#if DEBUG
            // Zeitmessung Stop
            stopwatch.Stop();
            Debug.WriteLine("Server Response Time: " + stopwatch.ElapsedMilliseconds + "ms");
#endif
            // Antwort zu string konvertieren
            string responseString = await response.Content.ReadAsStringAsync();

            // Antwort zurückgeben
            return responseString;
        }
    }
}
