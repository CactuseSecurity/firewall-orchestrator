using FWO.Logging;
using FWO.Api.Client;
using FWO.Config.Api;
using System.Diagnostics; 

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Class handling the Data Import
    /// </summary>
    public class DataImportBase
    {
        /// <summary>
        /// Api Connection
        /// </summary>
        protected readonly ApiConnection apiConnection;

        /// <summary>
        /// Global Config
        /// </summary>
        protected GlobalConfig globalConfig;

        /// <summary>
        /// Import File
        /// </summary>
        protected string importFile { get; set; } = "";


        /// <summary>
        /// Constructor for Data Import
        /// </summary>
        public DataImportBase(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

        /// <summary>
        /// Read the Import Data File
        /// </summary>
        protected void ReadFile(string filepath)
        {
            try
            {
                importFile = File.ReadAllText(filepath).Trim();
            }
            catch (Exception)
            {
                Log.WriteError("Read file", $"File could not be read from {filepath}.");
                throw;
            }
        }

        /// <summary>
        /// Execute the Data Import Script
        /// </summary>
        protected async Task<bool> RunImportScript(string importScriptFile)
        {
            try
            {
                if(File.Exists(importScriptFile))
                {
                    ProcessStartInfo start = new ProcessStartInfo()
                    {
                        FileName = importScriptFile,
                        Arguments = "", // args,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    };
                    Process? process = Process.Start(start);
                    StreamReader? reader = process?.StandardOutput;
                    string? result = reader?.ReadToEnd();
                    process?.WaitForExit(); 
                    process?.Close();
                    return true;
                }
            }
            catch (Exception Exception)
            {
                Log.WriteError("Run Import Script", $"File {importScriptFile} could not be executed.", Exception);
            }
            return false;
        }



        private void run_cmd(string cmd, string args)
        {
            // full path of python interpreter 
            string python = @"C:\Continuum\Anaconda\python.exe"; 

            // python app to call 
            string myPythonApp = "sum.py"; 

            // dummy parameters to send Python script 
            int x = 2; 
            int y = 5; 

            // Create new process start info 
            ProcessStartInfo myProcessStartInfo = new ProcessStartInfo(python); 

            // make sure we can read the output from stdout 
            myProcessStartInfo.UseShellExecute = false; 
            myProcessStartInfo.RedirectStandardOutput = true; 

            // start python app with 3 arguments  
            // 1st arguments is pointer to itself,  
            // 2nd and 3rd are actual arguments we want to send 
            myProcessStartInfo.Arguments = myPythonApp + " " + x + " " + y; 

            Process myProcess = new Process(); 
            myProcess.StartInfo = myProcessStartInfo; 

            Console.WriteLine("Calling Python script with arguments {0} and {1}", x,y); 
            myProcess.Start(); 

            StreamReader myStreamReader = myProcess.StandardOutput; 
            string myString = myStreamReader.ReadLine(); 
            myProcess.WaitForExit(); 
            myProcess.Close(); 
            Console.WriteLine("Value received from script: " + myString); 
        }


        private void run_cmd1(string cmd, string args)
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = cmd;
            start.Arguments = args;
            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            using (Process process = Process.Start(start))
            {
                using (StreamReader reader = process.StandardOutput)
                {
                    string result = reader.ReadToEnd();
                    Console.Write(result);
                }
            }
        }
    }
}
