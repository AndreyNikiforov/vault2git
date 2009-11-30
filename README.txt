vault2git

Purpose

	Convert history from vault repo into git repo

Building

	1. Download vault Client API for your version of Vault (you have to use version matching your Vault server); 
	   other option is to copy *.dll from Vault client installation
	2. Replace Vault2GitLib/libs/*.dll with your files
	3. Recompile
	
Running first time (trunk)

	0. install git (msysgit is the easiest way); Configure it (email, name etc)
	1. Create empty folder and init git repo in it. This is your git repo and vault working folder.
	2. Place .gitignore into your working folder. The one from vault2git may be a good starting point.
	3. create initial commit, e.g. git commit --allow-empty --message="inital commit"
	4. Change .config file to match your setup. Set only trunk mapping first (in Convertor.Paths key)
	5. run vault2git.exe <maxNumberOfRevisionsToProcess>; use CapsLock to cancel the process

How it works

	1. v2g will get versions from vault into working folder one by one and commit each into git. vault bindings will be stripped from
           *.sln, *.csproj, *.vdproj files. 
	2. Commit message will be taken from vault. Info about vault revision and paths will be appended to the commit message:
           [git-vault-id] VaultRepo$/VaultPath@VaultRevision/VaultTrxNumber
	3. Dates of the files and commits will not be preserved.
	4. Author names will be preserved and company domain will be appended.
	5. Branches will not be created automatically.

Running for branches

	1. Using Vault client, find revision# of trunk after which branch was created. (1234 as example)
	2. find git commit matching that revision#: git log --grep="@1234" (abc1234 as example)
	3. create branch in git from that commit, e.g. git branch MyBranchName acb1234
	4. switch to that branch: git checkout MyBranchName
	5. commit into branch to reset rev#: git commit --allow-empty --message="[git-vault-id] VaultRepo$/ValueBranchPath@0/0"
	6. add mapping for new branch into vault2git.exe.config
	7. run vault2git.exe <maxNumberOfRevisionsToProcess>; use CapsLock to cancel the process; it will process mappings according to .config

Incremenatal updates

	When run again, vault2git first checks for [git-vault-id] tag in latest git commit, parses revision number (after @)
	and pulls vault data above found number.
	
Additional actions

	Every 200 git commits, garbage collection is executed.
	
	After processing is complete, finalization executes git update-server-info, which update data in git repo for 
	dumb server (e.g. read-only http sharing with iis)

Last Updated: 2009-11-29
Author: Andrey Nikiforov
Location: github.com/nikiforov/vault2git
