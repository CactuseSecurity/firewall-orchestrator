using System;
using Eto.Forms;

namespace Firewall_Orchestrator.Desktop
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            new Application(Eto.Platform.Detect).Run(new MainForm());        
        }
    }
}