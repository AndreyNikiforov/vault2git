using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using VaultClientIntegrationLib;
using VaultClientOperationsLib;
using VaultLib;
using System.Xml.XPath;
using System.Xml;

namespace Vault2Git.Lib
{
    public class Processor
    {
        /// <summary>
        /// path to git.exe
        /// </summary>
        public string GitCmd;

        /// <summary>
        /// path where conversion will take place. If it not already set as value working folder, it will be set automatically
        /// </summary>
        public string WorkingFolder;

        public string VaultServer;
        public string VaultUser;
        public string VaultPassword;
        public string VaultRepository;

        public string GitDomainName;

        public int GitGCInterval = 200;

        //callback
        public Func<long, int, bool> Progress;

        //git commands
        private const string _gitVersionCmd = "version";
        private const string _gitGCCmd = "gc --auto";
        private const string _gitFinalizer = "update-server-info";
        private const string _gitAddCmd = "add --all .";
        private const string _gitLastCommitInfoCmd = "log -1";
        private const string _gitCommitCmd = @"commit --quiet --allow-empty --all --date=""{2}"" --author=""{0} <{0}@{1}>"" -F -";
        private const string _gitCheckoutCmd = "checkout -f {0}";
        private const string _gitBranchCmd = "branch";

        //constants
        private const string VaultTag = "[git-vault-id]";

        /// <summary>
        /// version number reported to <see cref="Progress"/> when init is complete
        /// </summary>
        public const int ProgressSpecialVersionInit = 0;
        
        /// <summary>
        /// version number reported to <see cref="Progress"/> when git gc is complete
        /// </summary>
        public const int ProgressSpecialVersionGc = -1;

        /// <summary>
        /// version number reported to <see cref="Progress"/> when finalization finished (e.g. logout, unset wf etc)
        /// </summary>
        public const int ProgressSpecialVersionFinalize = -2;
        
        public bool Pull(string vaultRepoPath, string gitBranch, long limitCount)
        {
            var ticks = vaultLogin();
            try
            {
                long currentVaultVersion = 0;
                ticks += Init(vaultRepoPath, gitBranch, ref currentVaultVersion);

                //get vaultVersions
                IDictionary<long, VaultVersionInfo> vaultVersions = new SortedList<long, VaultVersionInfo>();

                ticks += this.vaultPopulateInfo(vaultRepoPath, vaultVersions);

                //report init
                if (null != Progress)
                    if (Progress(ProgressSpecialVersionInit, ticks))
                        return true;

                var counter = 0;
                foreach (var version in vaultVersions.Where(p => p.Key > currentVaultVersion))
                {
                    //get vault version
                    ticks = vaultGet(vaultRepoPath, version.Key, version.Value.TrxId);
                    //change all sln files
                    Directory.GetFiles(
                        WorkingFolder,
                        "*.sln",
                        SearchOption.AllDirectories)
                        //remove temp files created by vault
                        .Where(f => !f.Contains("~"))
                        .ToList()
                        .ForEach(f => ticks += removeSCCFromSln(f));
                    //change all csproj files
                    Directory.GetFiles(
                        WorkingFolder,
                        "*.csproj",
                        SearchOption.AllDirectories)
                        //remove temp files created by vault
                        .Where(f => !f.Contains("~"))
                        .ToList()
                        .ForEach(f => ticks += removeSCCFromCSProj(f));
                    //change all vdproj files
                    Directory.GetFiles(
                        WorkingFolder,
                        "*.vdproj",
                        SearchOption.AllDirectories)
                        //remove temp files created by vault
                        .Where(f => !f.Contains("~"))
                        .ToList()
                        .ForEach(f => ticks += removeSCCFromVDProj(f));
                    //get vault version info
                    var info = vaultVersions[version.Key];
                    //commit
                    ticks += gitCommit(info.Login, this.GitDomainName, buildCommitMessage(vaultRepoPath, version.Key, info), info.TimeStamp);
                    if (null != Progress)
                        if (Progress(version.Key, ticks))
                            return true;
                    counter++;
                    //call gc
                    if (0 == counter % GitGCInterval)
                    {
                        ticks = gitGC();
                        if (null != Progress)
                            if (Progress(ProgressSpecialVersionGc, ticks))
                                return true;
                    }
                    //check if limit is reached
                    if (counter >= limitCount)
                        break;
                }

            }
            finally
            {
                //complete
                ticks = vaultFinalize(vaultRepoPath);
                ticks += vaultLogout();
                //finalize git (update server info for dumb clients
                ticks += gitFinalize();
                if (null != Progress)
                    Progress(ProgressSpecialVersionFinalize, ticks);
            }
            return false;
        }


        /// <summary>
        /// removes Source control refs from sln files
        /// </summary>
        /// <param name="filePath">path to sln file</param>
        /// <returns></returns>
        private static int removeSCCFromSln(string filePath)
        {
            var ticks = Environment.TickCount;
            var lines = File.ReadAllLines(filePath).ToList();
            //scan lines 
            var searchingForStart = true;
            var beginingLine = 0;
            var endingLine = 0;
            var currentLine = 0;
            foreach(var line in lines)
            {
                var trimmedLine = line.Trim();
                if (searchingForStart)
                {
                    if (trimmedLine.StartsWith("GlobalSection(SourceCodeControl)"))
                    {
                        beginingLine = currentLine;
                        searchingForStart = false;
                    }
                } else
                {
                    if (trimmedLine.StartsWith("EndGlobalSection"))
                    {
                        endingLine = currentLine;
                        break;
                    }
                }
                currentLine++;
            }
            //removing lines
            if (beginingLine >0 & endingLine > 0)
            {
                lines.RemoveRange(beginingLine, endingLine - beginingLine + 1);
                File.WriteAllLines(filePath, lines.ToArray());
            }
            return Environment.TickCount - ticks;
        }

        /// <summary>
        /// removes Source control refs from csProj files
        /// </summary>
        /// <param name="filePath">path to sln file</param>
        /// <returns></returns>
        public static int removeSCCFromCSProj(string filePath)
        {
            var ticks = Environment.TickCount;
            var doc = new XmlDocument();
            try
            {
                doc.Load(filePath);
                while (true)
                {
                    var nav = doc.CreateNavigator().SelectSingleNode("//*[starts-with(name(), 'Scc')]");
                    if (null == nav)
                        break;
                    nav.DeleteSelf();
                }
                doc.Save(filePath);
            }
            catch
            {
                Console.WriteLine("Failed for {0}", filePath);
                throw;
            }
            return Environment.TickCount - ticks;
        }

        /// <summary>
        /// removes Source control refs from vdProj files
        /// </summary>
        /// <param name="filePath">path to sln file</param>
        /// <returns></returns>
        private static int removeSCCFromVDProj(string filePath)
        {
            var ticks = Environment.TickCount;
            var lines = File.ReadAllLines(filePath).ToList();
            File.WriteAllLines(filePath, 
                lines.Where(l => !l.Trim().StartsWith(@"""Scc")).ToArray()
                );
            return Environment.TickCount - ticks;
        }

        private int vaultPopulateInfo(string repoPath, IDictionary<long, VaultVersionInfo> info)
        {
            var ticks = Environment.TickCount;

            foreach (var i in ServerOperations.ProcessCommandVersionHistory(repoPath,
                                                                            1,
                                                                            VaultDateTime.Parse("2000-01-01"),
                                                                            VaultDateTime.Parse("2020-01-01"),
                                                                            0))
                info.Add(i.Version, new VaultVersionInfo()
                                        {
                                            TrxId = i.TxID,
                                            Comment = i.Comment,
                                            Login = i.UserLogin,
                                            TimeStamp = i.TxDate.GetDateTime()
                                        });
            return Environment.TickCount - ticks;
        }

        private int vaultGet(string repoPath, long version, long txId)
        {
            var ticks = Environment.TickCount;
            //apply version to the repo folder
            GetOperations.ProcessCommandGetVersion(
                repoPath,
                Convert.ToInt32(version),
                new GetOptions()
                {
                    MakeWritable = MakeWritableType.MakeAllFilesWritable,
                    Merge = MergeType.OverwriteWorkingCopy,
                    OverrideEOL = VaultEOL.None,
                    //remove working copy does not work -- bug http://support.sourcegear.com/viewtopic.php?f=5&t=11145
                    PerformDeletions = PerformDeletionsType.RemoveWorkingCopy,
                    SetFileTime = SetFileTimeType.Current,
                    Recursive = true
                });

            //now process deletions, moves, and renames (due to vault bug)
            var allowedRequests = new int[] {
                9,  //delete
                12, //move
                15  //rename
            };
            foreach (var item in ServerOperations.ProcessCommandTxDetail(txId).items
                .Where(i => allowedRequests.Contains(i.RequestType)))
            {
                //delete file
                var pathToDelete = Path.Combine(this.WorkingFolder, item.ItemPath1.Substring(repoPath.Length + 1));
                //Console.WriteLine("delete {0} => {1}", item.ItemPath1, pathToDelete);
                if (File.Exists(pathToDelete))
                    File.Delete(pathToDelete);
                if (Directory.Exists(pathToDelete))
                    Directory.Delete(pathToDelete, true);
            }
            return Environment.TickCount - ticks;
        }
        
        struct VaultVersionInfo
        {
            public long TrxId;
            public string Comment;
            public string Login;
            public DateTime TimeStamp;
        }

        private int Init(string vaultRepoPath, string gitBranch, ref long currentVersion)
        {
            //set working folder
            var ticks = setVaultWorkingFolder(vaultRepoPath);
            //checkout branch
            string[] msgs;
            for (int tries = 0;; tries++)
            {
                ticks += runGitCommand(string.Format(_gitCheckoutCmd, gitBranch), string.Empty, out msgs);
                //confirm current branch (sometimes checkout failed)
                string currentBranch;
                ticks += this.gitBranch(out currentBranch);
                if (gitBranch.Equals(currentBranch, StringComparison.OrdinalIgnoreCase))
                    break;
                if (tries > 5)
                    throw new Exception("cannot switch");
            }
            //get info
            ticks += gitLog(out msgs);
            //get vault version
            currentVersion = getVaultVersionFromGitLogMessage(msgs);
            return ticks;
        }

        private int vaultFinalize(string vaultRepoPath)
        {
            //unset working folder
            return unSetVaultWorkingFolder(vaultRepoPath);
        }

        private int gitCommit(string vaultLogin, string gitDomainName, string vaultCommitMessage, DateTime commitTimeStamp)
        {
            string[] msgs;
            var ticks = runGitCommand(_gitAddCmd, string.Empty, out msgs);
            ticks += runGitCommand(
                string.Format(_gitCommitCmd, vaultLogin, gitDomainName, string.Format("{0:s}", commitTimeStamp)),
                vaultCommitMessage,
                out msgs
                );
            return ticks;
        }

        private int gitBranch(out string currentBranch)
        {
            string[] msgs;
            var ticks = runGitCommand(_gitBranchCmd, string.Empty, out msgs);
            currentBranch = msgs.Where(s => s.StartsWith("*")).First().Substring(1).Trim();
            return ticks;
        }

        private string buildCommitMessage(string repoPath, long version, VaultVersionInfo info)
        {
            //parse path repo$RepoPath@version/trx
            var r = new StringBuilder(info.Comment);
            r.AppendLine();
            r.AppendFormat("{4} {0}{1}@{2}/{3}", this.VaultRepository, repoPath, version, info.TrxId, VaultTag);
            r.AppendLine();
            return r.ToString();
        }

        private long getVaultVersionFromGitLogMessage(string[] msg)
        {
            //get last string
            var stringToParse = msg.Last();
            //search for version tag
            var versionString = stringToParse.Split(new string[] {VaultTag}, StringSplitOptions.None).LastOrDefault();
            if (null == versionString)
                return 0;
            //parse path reporepoPath@version/trx
            //get version/trx part
            var versionTrxTag = versionString.Split('@').LastOrDefault();
            if (null == versionTrxTag)
                return 0;

            //get version
            long version = 0;
            long.TryParse(versionTrxTag.Split('/').First(), out version);
            return version;
        }

        private int gitLog(out string[] msg)
        {
            return runGitCommand(_gitLastCommitInfoCmd, string.Empty, out msg);
        }

        private int gitGC()
        {
            string[] msg;
            return runGitCommand(_gitGCCmd, string.Empty, out msg);
        }

        private int gitFinalize()
        {
            string[] msg;
            return runGitCommand(_gitFinalizer, string.Empty, out msg);
        }

        private int setVaultWorkingFolder(string repoPath)
        {
            var ticks = Environment.TickCount;
            ServerOperations.SetWorkingFolder(repoPath, this.WorkingFolder, true);
            return Environment.TickCount - ticks;
        }

        private int unSetVaultWorkingFolder(string repoPath)
        {
            var ticks = Environment.TickCount;
            //remove any assignment first
            //it is case sensitive, so we have to find how it is recorded first
            var exPath = ServerOperations.GetWorkingFolderAssignments()
                .Cast<DictionaryEntry>()
                .Select(e => e.Key.ToString())
                .Where(e => repoPath.Equals(e, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (null != exPath)
                ServerOperations.RemoveWorkingFolder(exPath);
            return Environment.TickCount - ticks;
        }
        private int runGitCommand(string cmd, string stdInput, out string[] stdOutput)
        {
            return runGitCommand(cmd, stdInput, out stdOutput, null);
        }
        private int runGitCommand(string cmd, string stdInput, out string[] stdOutput, IDictionary<string, string> env)
        {
            var ticks = Environment.TickCount;

            var pi = new ProcessStartInfo(GitCmd, cmd)
                         {
                             WorkingDirectory = WorkingFolder,
                             UseShellExecute = false,
                             RedirectStandardOutput = true,
                             RedirectStandardInput = true
                         };
            //set env vars
            if (null != env)
                foreach (var e in env)
                    pi.EnvironmentVariables.Add(e.Key, e.Value);
            using (var p = new Process()
                               {
                                   StartInfo = pi
                               })
            {
                p.Start();
                p.StandardInput.Write(stdInput);
                p.StandardInput.Close();
                var msgs = new List<string>();
                while (!p.StandardOutput.EndOfStream)
                    msgs.Add(p.StandardOutput.ReadLine());
                stdOutput = msgs.ToArray();
                p.WaitForExit();
                if (p.ExitCode > 0)
                    throw new Exception(string.Format("ERROR git {0} exited with code {1}", cmd, p.ExitCode));

            }
            return Environment.TickCount - ticks;
        }

        private int vaultLogin()
        {
            var ticks = Environment.TickCount;
            ServerOperations.client.LoginOptions.URL = string.Format("http://{0}/VaultService", this.VaultServer);
            ServerOperations.client.LoginOptions.User = this.VaultUser;
            ServerOperations.client.LoginOptions.Password = this.VaultPassword;
            ServerOperations.client.LoginOptions.Repository = this.VaultRepository;
            ServerOperations.Login();
            ServerOperations.client.MakeBackups = false;
            ServerOperations.client.AutoCommit = false;
            ServerOperations.client.Verbose = true;
            return Environment.TickCount - ticks;
        }
        private int vaultLogout()
        {
            var ticks = Environment.TickCount;
            ServerOperations.Logout();
            return Environment.TickCount - ticks;
        }

    }
}
