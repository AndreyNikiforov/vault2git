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
	3. Create initial commit, e.g. git commit --allow-empty --message="initial commit"
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
	7. Git tags are generated from Vault labels. Each label is prefixed with Vault Transaction ID. Duplicates (e.g. coming from Shared
	         folder) are ignored. Use '--ignore-labels' argument to disable this functionality.

Incremental updates

	When ran again, vault2git first checks for [git-vault-id] tag in latest git commit, parses revision number (after @)
	and pulls Vault data above found number only.

Additional actions

	Every 200 git commits, Git garbage collection is executed.

	After processing is complete, finalization executes git update-server-info, which updates data in Git repo for
	dumb server (e.g. read-only http sharing with iis)

Vault labels conversion
	Vault labels support a lot more characters than git tags. For compatibility, all non alphanumeric characters for
	git tags are replaced with "_". Git tag can only be created if the related git commit exists. Vault labels
	comments are added as git tag comments.

Last Updated: 2011-04-06
Author: Andrey Nikiforov
Contributor: Jevgeni Zelenkov ( github.com/jzelenkov )
Location: github.com/AndreyNikiforov/vault2git

## Brian Reiter: Changes Since Fork ###
- Update for Vault 6 Pro API
- catch UnauthorizedAccessException (race condition?) attempting to delete nonexistent file
- catch and handle exception thrown if vault directory name changed during commit history
- remove vault 4 DLLs.
 