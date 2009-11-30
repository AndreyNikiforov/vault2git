using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Windows.Forms;
using Vault2Git.Lib;

namespace Vault2Git.CLI
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        //[STAThread]
        static void Main(string[] args)
        {
            //get count from param
            if (args.Count() != 1)
            {
                Console.WriteLine("usage: vault2git <maxVersion2ProcessForEachBranch>");
                return;
            }
            int maxVersion;
            if (!int.TryParse(args[0], out maxVersion))
            {
                Console.WriteLine("maxVersion2ProcessForEachBranch should be int");
                return;
            }
            //get configuration for branches
            var paths = ConfigurationManager.AppSettings["Convertor.Paths"];
            var pathPairs = paths.Split(';');

            var processor = new Vault2Git.Lib.Processor()
                                {
                                    WorkingFolder = ConfigurationManager.AppSettings["Convertor.WorkingFolder"],
                                    GitCmd = ConfigurationManager.AppSettings["Convertor.GitCmd"],
                                    GitDomainName = ConfigurationManager.AppSettings["Git.DomainName"],
                                    VaultServer = ConfigurationManager.AppSettings["Vault.Server"],
                                    VaultRepository = ConfigurationManager.AppSettings["Vault.Repo"],
                                    VaultUser = ConfigurationManager.AppSettings["Vault.User"],
                                    VaultPassword = ConfigurationManager.AppSettings["Vault.Password"],
                                    Progress = ShowProgress
                                };

            foreach (var pair in pathPairs)
            {
                var pairParts = pair.Split('~');
                if (processor.Pull(pairParts[0], pairParts[1], maxVersion))
                    break;
            }
#if DEBUG
            Console.WriteLine("Press ENTER");
            Console.ReadLine();
#endif
        }

        static bool ShowProgress(long version, int ticks)
        {
            var timeSpan = TimeSpan.FromMilliseconds(ticks);
            if (Processor.ProgressSpecialVersionInit == version)
                Console.WriteLine("init took {0}", timeSpan);
            else if (Processor.ProgressSpecialVersionGc == version)
                Console.WriteLine("gc took {0}", timeSpan);
            else if (Processor.ProgressSpecialVersionFinalize == version)
                Console.WriteLine("finalization took {0}", timeSpan);
            else
                Console.WriteLine("processing version {0} took {1}", version, timeSpan);
            return Console.CapsLock; //cancel flag
        }
    }
}
