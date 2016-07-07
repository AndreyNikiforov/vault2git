using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using System.Collections.Specialized;
using System.Collections;
using System.IO;
using System.Text; 
using Vault2Git.Lib;

namespace Vault2Git.CLI
{
    static class Program
    {

        class Params
        {
            public int Limit { get; protected set; }
            public int RestartLimit { get; protected set; }
            public bool UseConsole { get; protected set; }
            public bool UseCapsLock { get; protected set; }
            public bool SkipEmptyCommits { get; protected set; }
            public bool IgnoreLabels { get; protected set; }
            public bool Verbose { get; protected set; }
            public bool ForceFullFolderGet { get; protected set; }
            public bool Pause { get; protected set; }
            public string Paths { get; protected set; }
            public string Work { get; protected set; }
            public IEnumerable<string> Branches;
            public IEnumerable<string> Errors;

            protected Params()
            {
            }

            private const string _limitParam = "--limit=";
            private const string _restartLimitParam = "--restart-limit=";
            private const string _branchParam = "--branch=";
            private const string _pathsParam = "--paths=";
            private const string _workParam = "--work=";

            public static Params InitialParse(string[] args )
            {
               var p = new Params();
               foreach (var o in args)
               {
                  if (o.StartsWith(_pathsParam))
                  {
                     p.Paths = o.Substring(_pathsParam.Length);
                     break;
                  }
               }
               return p;
            }

            public static Params Parse(string[] args, IEnumerable<string> gitBranches)
            {
                var errors = new List<string>();
                var branches = new List<string>();

                var p = new Params();
                foreach(var o in args) {
                    if (o.Equals("--console-output"))
                        p.UseConsole = true;
                    else if (o.Equals("--caps-lock"))
                        p.UseCapsLock = true;
                    else if (o.Equals("--skip-empty-commits"))
                        p.SkipEmptyCommits = true;
                    else if (o.Equals("--ignore-labels"))
                       p.IgnoreLabels = true;
                    else if (o.Equals("--verbose"))
                       p.Verbose = true;
                    else if (o.Equals("--ForceFullFolderGet"))
                       p.ForceFullFolderGet = true;
                    else if (o.Equals("--pause"))
                       p.Pause = true;
                    else if (o.Equals("--help"))
                    {
                        errors.Add("Usage: vault2git [options]");
                        errors.Add("options:");
                        errors.Add("   --help                  This screen");
                        errors.Add("   --console-output        Use console output (default=no output)");
                        errors.Add("   --verbose               Output detailed messages");
                        errors.Add("   --pause                 Pause just before commit so local state may be checked");
                        errors.Add("   --caps-lock             Use caps lock to stop at the end of the cycle with proper finalizers (default=no caps-lock)");
                        errors.Add("   --branch=<branch>       Process only one branch from config. Branch name should be in git terms. Default=all branches from config");
                        errors.Add("   --limit=<n>             Max number of versions to take from Vault for each branch. Default all versions");
                        errors.Add("   --restart-limit=<n>     Max number of commits to search back in git for restart point for each branch. Default 20 commits. -ve value forces a start from first Vault revision");
                        errors.Add("   --ForceFullFolderGet    Every change set gets entire folder structure. Required for shared file updates to be picked up. Otherwise such changes will only be picked up when the entire folder is retrieved due to a subsequent changeset which necessitates a whole folder retrieval.");
                        errors.Add("   --skip-empty-commits    Do not create empty commits in Git");
                        errors.Add("   --ignore-labels         Do not create Git tags from Vault labels");
                        errors.Add("   --paths=<paths>         paths to override setting in .config");
                        errors.Add("   --work=<WorkingFolder>  WorkingFolder to override setting in .config. --work=. is most common");
                    }
                    else if (o.StartsWith(_limitParam))
                     {
                           var l = o.Substring(_limitParam.Length);
                           var max = 0;
                           if (int.TryParse(l, out max))
                              p.Limit = max;
                           else
                              errors.Add(string.Format("Incorrect limit ({0}). Use integer.", l));
                     }
                    else if (o.StartsWith(_restartLimitParam))
                    {
                       var l = o.Substring(_restartLimitParam.Length);
                       var max = 0;
                       if (int.TryParse(l, out max))
                          p.RestartLimit = max;
                       else
                          errors.Add(string.Format("Incorrect restart limit ({0}). Use integer.", l));
                    }
                    else if (o.StartsWith(_branchParam))
                     {
                        var b = o.Substring(_branchParam.Length);
                        if (gitBranches.Contains(b))
                           branches.Add(b);
                        else
                           errors.Add(string.Format("Unknown branch {0}. Use one specified in .config", b));
                     }
                     else if (o.StartsWith(_pathsParam))
                     {
                        continue;
                     }
                     else if (o.StartsWith(_workParam))
                     {
                        p.Work = o.Substring(_workParam.Length);
                     }
                     else
                        errors.Add(string.Format("Unknown option {0}", o));
                }
                p.Branches = 0 == branches.Count() 
                    ? gitBranches 
                    : branches;
                p.Errors = errors;    
                return p;
            }
        }

        private static bool _useCapsLock = false;
        private static bool _useConsole = false;
        private static bool _ignoreLabels = false;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        //[STAThread]
        static void Main(string[] args)
        {
            string paths = null;
            string workingFolder = null;
            System.Configuration.Configuration configuration = null;

            Console.WriteLine("Vault2Git -- converting history from Vault repositories to Git");
            System.Console.InputEncoding = System.Text.Encoding.UTF8;

            // First look for Config file in the current directory - allows for repository-based config files
            string configPath = System.IO.Path.Combine(Environment.CurrentDirectory, "Vault2Git.exe.config");
            if (File.Exists(configPath))
            {
               System.Configuration.ExeConfigurationFileMap configFileMap = new ExeConfigurationFileMap();
               configFileMap.ExeConfigFilename = configPath;

               configuration = ConfigurationManager.OpenMappedExeConfiguration(configFileMap, ConfigurationUserLevel.None);
            }
            else
            {
               // Get normal exe file config. 
               // This is what happens by default when using ConfigurationManager.AppSettings["setting"] 
               // to access config properties
            #if DEBUG
               string applicationName = Environment.GetCommandLineArgs()[0];
            #else 
               string applicationName = Environment.GetCommandLineArgs()[0]+ ".exe";
            #endif

               configPath = System.IO.Path.Combine(Environment.CurrentDirectory, applicationName);
               configuration = ConfigurationManager.OpenExeConfiguration(configPath);
            }

            // Get access to the AppSettings properties in the chosen config file
            AppSettingsSection appSettings = (AppSettingsSection)configuration.GetSection("appSettings");

            Console.WriteLine("Using config file " + configPath );
           
            // Get Paths parameter first as its required for validation of branches parameter
            var paramInitial = Params.InitialParse(args);
            paths = paramInitial.Paths; 

            if (paths == null)
            {
               //get configuration for branches
               paths = appSettings.Settings["Convertor.Paths"].Value;
            }

            var pathPairs = paths.Split(';')
                .ToDictionary(
                pair =>
                    pair.Split('~')[1], pair => pair.Split('~')[0]
                    );

            //parse rest of params
            var param = Params.Parse(args, pathPairs.Keys);

            //get count from param
            if (param.Errors.Count() > 0)
            {
                foreach (var e in param.Errors)
                    Console.WriteLine(e);
                return;
            }

            Console.WriteLine("   use Vault2Git --help to get additional info");

           _useConsole = param.UseConsole;
            _useCapsLock = param.UseCapsLock;
            _ignoreLabels = param.IgnoreLabels;
            workingFolder = param.Work;

            if (workingFolder == null)
            {
               workingFolder = appSettings.Settings["Convertor.WorkingFolder"].Value;
            }

            // check working folder ends with trailing slash
            if (workingFolder.Last() != '\\')
            {
                workingFolder += '\\';
            }

            if (param.Verbose) 
            {
               Console.WriteLine("WorkingFolder = {0}", workingFolder );
               Console.WriteLine("GitCmd = {0}", appSettings.Settings["Convertor.GitCmd"].Value);
               Console.WriteLine("GitDomainName = {0}", appSettings.Settings["Git.DomainName"].Value);
               Console.WriteLine("VaultServer = {0}", appSettings.Settings["Vault.Server"].Value);
               Console.WriteLine("VaultRepository = {0}", appSettings.Settings["Vault.Repo"].Value);
               Console.WriteLine("VaultUser = {0}", appSettings.Settings["Vault.User"].Value );
               Console.WriteLine("VaultPassword = {0}", appSettings.Settings["Vault.Password"].Value);
               Console.WriteLine("Converterpaths = {0}\n", appSettings.Settings["Convertor.Paths"].Value);
            }

            var processor = new Vault2Git.Lib.Processor()
                                {
                                    WorkingFolder = workingFolder,
                                    GitCmd = appSettings.Settings["Convertor.GitCmd"].Value,
                                    GitDomainName = appSettings.Settings["Git.DomainName"].Value,
                                    VaultServer = appSettings.Settings["Vault.Server"].Value,
                                    VaultRepository = appSettings.Settings["Vault.Repo"].Value,
                                    VaultUser = appSettings.Settings["Vault.User"].Value,
                                    VaultPassword = appSettings.Settings["Vault.Password"].Value,
                                    Progress = ShowProgress,
                                    SkipEmptyCommits = param.SkipEmptyCommits,
                                    Verbose = param.Verbose,
                                    Pause = param.Pause,
                                    ForceFullFolderGet= param.ForceFullFolderGet
                                };


            processor.Pull
                (
                    pathPairs.Where(p => param.Branches.Contains(p.Key))
                    , 0 == param.Limit ? 999999999 : param.Limit
                    , 0 == param.RestartLimit ? 20 : param.RestartLimit
                );

            if (!_ignoreLabels)
                processor.CreateTagsFromLabels();

#if DEBUG
                        Console.WriteLine("Press ENTER");
                        Console.ReadLine();
#endif
        }

        static bool ShowProgress(long version, int ticks)
        {
            var timeSpan = TimeSpan.FromMilliseconds(ticks);
            if (_useConsole)
            {

                if (Processor.ProgressSpecialVersionInit == version)
                    Console.WriteLine("init took {0}", timeSpan);
                else if (Processor.ProgressSpecialVersionGc == version)
                    Console.WriteLine("gc took {0}", timeSpan);
                else if (Processor.ProgressSpecialVersionFinalize == version)
                    Console.WriteLine("finalization took {0}", timeSpan);
                else if (Processor.ProgressSpecialVersionTags == version)
                    Console.WriteLine("tags creation took {0}", timeSpan);
                else
                    Console.WriteLine("processing version {0} took {1}", version, timeSpan);
            }

            return _useCapsLock && Console.CapsLock; //cancel flag
        }
    }
}
