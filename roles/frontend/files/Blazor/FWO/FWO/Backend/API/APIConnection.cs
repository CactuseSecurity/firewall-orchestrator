using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace FWO
{
    public class APIConnection
    {
        // Server URL
        private const string ServerURI = "https://localhost/api/v1/graphql";

        // Http/s Client
        private readonly HttpClient Client;

        public APIConnection()
        {
            // Allow all certificates // REMOVE IF SERVER GOT VALID CERTIFICATE
            //ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

            // New http/s client
            Client = new HttpClient();
        }

        //Query is structured as follow: { "query" : " 'query' ", "variables" : { 'variables' } } with 'query' as query to send and 'variables' as corresponding variables
        public async Task<string> SendQuery(string Query, string Variables = "", string OperationName = "")
        {
            // New http-body containing the query
            StringContent content = new StringContent("{ \"query\": \"" + Query + "\" , \"variables\" : { " + Variables + " } }", Encoding.UTF8);
            // Remove all standard headers
            content.Headers.Clear();
            // Add content header
            content.Headers.Add("content-type", "application/json");
            // Add auth header
            content.Headers.Add("x-hasura-admin-secret", "st8chelt1er");
#if DEBUG
            // Start time measurement
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
#endif
            HttpResponseMessage response;
            string responseString;

            try
            {
                // Send http-packet with query and header. Receive answer
                response = await Client.PostAsync(ServerURI, content);
            }
            catch (Exception e)
            {
                return "";
                //TODO: Server can not be reached
            }
#if DEBUG
            // Stop time measurement
            stopwatch.Stop();
            Debug.WriteLine("Query Server Response Time: " + stopwatch.ElapsedMilliseconds + "ms");
#endif

            try
            {
                // Convert answer to string
                responseString = await response.Content.ReadAsStringAsync();
            }
            catch (Exception e)
            {
                return "";
                //TODO: Answer can not be converted to string
            }

            // Return answer
            return responseString;

            //TODO: https://www.youtube.com/watch?v=4XlA2WDXyTo
        }

        public static async Task<string> Test()
        {
            // Server URL
            // const string ServerURI = "https://demo.itsecorg.de/api/v1/graphql";

            // Erlaube alle Zertifikate // ENTFERNEN SOBALD SERVER GÜLTIGES ZERTIFIKAT HAT
            // ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

            // Neuer Http Client
            HttpClient client = new HttpClient();

            // Query aufgebaut wie folgt { "query" : " 'query' ", "variables" : { 'variables' } } mit 'query' für die zu versendende Query und 'variables' für die dazugehörigen Variablen
            string Query = "{ \"query\":\" { device { dev_id dev_name stm_dev_typ { dev_typ_name dev_typ_version } management { mgm_id mgm_name} rules(where: {active: {_eq: true}, rule_disabled: {_eq: false}}, order_by: {rule_num: asc}) { rule_num rule_id rule_uid rule_froms { object { obj_name } } rule_tos { object { obj_name } } rule_services { service { svc_name svc_id } } rule_action rule_track } }} \", \" variables \" : {} }";

            // Neuer Http-Body der die Query enthält
            StringContent content = new StringContent(Query, Encoding.UTF8);
            // Alle Standard-Header entfernen
            content.Headers.Clear();
            // Inhaltstypheader hinzufügen
            content.Headers.Add("content-type", "application/json");
            // Authorisierungsheader hinzufügen
            content.Headers.Add("x-hasura-admin-secret", "st8chelt1er");

            // Zeitmessung Start
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            // Http-Packet mit Query und Headern versenden und Antwort des Server empfangen
            HttpResponseMessage response = await client.PostAsync(ServerURI, content);

            // Antwort zu string konvertieren
            string responseString = await response.Content.ReadAsStringAsync();

            // Zeitmessung Stop
            stopwatch.Stop();

            // Antwort ausgeben
            Console.WriteLine(responseString);
            // Zeitmessungsausgabe
            Console.WriteLine("\nZeit für Abfrage: " + stopwatch.Elapsed.ToString());

            return responseString;

            Console.ReadLine();
        }
    }
}
