using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using FWO.Logging;

namespace FWO.ApiClient.Queries
{
    public class ConfigQueries : Queries
    {
        public static readonly string getLanguages;
        public static readonly string getAllTexts;
        public static readonly string getTextsPerLanguage;

        static ConfigQueries()
        {
            try
            {
                getLanguages = File.ReadAllText(QueryPath + "config/getLanguages.graphql");
                getAllTexts = File.ReadAllText(QueryPath + "config/getTexts.graphql");
                getTextsPerLanguage = File.ReadAllText(QueryPath + "config/getTextsPerLanguage.graphql");
            }
            catch (Exception exception)
            {
                Log.WriteError("Initialize ConfigQueries", "Api ConfigQueries could not be loaded.", exception);
                Environment.Exit(-1);
            }
        }
    }
}
