poc﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using GraphQL;
using GraphQL.Types;

namespace TestKonsole
{
    class Program
    {
        private static async Task Main()
        {
            // Server URL
            const string ServerURI = "https://demo.itsecorg.de/api/v1/graphql";

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
            Console.ReadLine();
        }
    }
}
