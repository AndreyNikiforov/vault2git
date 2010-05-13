vault2git

Purpose

	Convert history from Vault repo into Git repo

Building

	1. Download Vault Client API for your version of Vault (you have to use version matching your Vault server); 
	   other option is to copy *.dll from Vault client installation
	2. Replace Vault2GitLib/libs/*.dll with your files
	3. Recompile
	
Setting up (simple way)

	0. Install Git (msysgit is the easiest way); Configure it (email, name etc)
	1. Create empty folder and init Git repo in it. This is your Git repo and Vault working folder.
	2. Place .gitignore into your working folder. The one from vault2git may be a good starting point.
	3. Create initial commit, e.g. git commit --allow-empty --message="inital commit"
	4. Create branches from initial commit
	5. Change .config file to match your setup.
	6. run vault2git.exe.

How it works

	1. vault2git gets changesets from Vault into working folder one by one and commits each into Git. Vault bindings are stripped from
           *.sln, *.csproj, *.vdproj files. 
	2. Commit message is taken from Vault. Info about Vault revision and paths are appended to the commit message:
           [git-vault-id] VaultRepo$/VaultPath@VaultRevision/VaultTrxNumber
	3. Dates of the files are not preserved.
	4. Commit dates are preserved
	5. Author names are preserved and company domain (from .config) is appended.
	6. Empty commits are converted too

Incremenatal updates

	When ran again, vault2git first checks for [git-vault-id] tag in latest git commit, parses revision number (after @)
	and pulls Vault data above found number only.
	
Additional actions

	Every 200 git commits, Git garbage collection is executed.
	
	After processing is complete, finalization executes git update-server-info, which updates data in Git repo for 
	dumb server (e.g. read-only http sharing with iis)

Last Updated: 2010-05-13
Author: Andrey Nikiforov
Location: github.com/nikiforov/vault2git
