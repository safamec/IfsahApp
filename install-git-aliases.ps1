Write-Host "Installing Git Super-Alias Pack..." -ForegroundColor Green
 
# 1️⃣ Visual Log (GitKraken-style)
git config --global alias.lg "log --all --graph --decorate --color --abbrev-commit --date=short --pretty=format:'%C(bold blue)%h%C(reset) %C(cyan)%an%C(reset) %C(green)(%ad)%C(reset) %C(magenta)%d%C(reset)%n    %C(auto)%s'"
 
# 2️⃣ Quick Status & Branch Info
git config --global alias.st "status -sb"
git config --global alias.br "branch"
git config --global alias.ba "branch -a"
git config --global alias.brr "branch --show-current"
 
# 3️⃣ Checkout / Switch / Create
git config --global alias.co "checkout"
git config --global alias.sw "switch"
git config --global alias.nb "checkout -b"
 
# 4️⃣ Add / Commit / Amend
git config --global alias.cm "commit -m"
git config --global alias.ca "commit -a -m"
git config --global alias.cam "commit --amend --no-edit"
 
# 5️⃣ Pull / Push / Fetch
git config --global alias.pl "pull --rebase"
git config --global alias.ps "push"
git config --global alias.fp "fetch --prune"
 
# 6️⃣ Diff / Log Helpers
git config --global alias.df "diff"
git config --global alias.dc "diff --cached"
git config --global alias.lol "log --oneline --graph --all --decorate --color"
 
# 7️⃣ Undo / Reset / Revert
git config --global alias.unstage "reset HEAD --"
git config --global alias.last "log -1 HEAD"
git config --global alias.rh "reset --hard"
git config --global alias.rhm "reset --hard HEAD~1"
git config --global alias.unp "reset --soft HEAD~1"
 
# 8️⃣ Stash / Pop / List
git config --global alias.s "stash"
git config --global alias.sa "stash apply"
git config --global alias.sl "stash list"
git config --global alias.sp "stash pop"
 
# 9️⃣ Quick Aliases for Daily Commands
git config --global alias.gl "lg"
git config --global alias.gcm "commit -m"
git config --global alias.gco "checkout"
git config --global alias.gp "push"
 
# 🔥 Extras for Productivity
git config --global alias.cleanall "clean -fdx"
git config --global alias.visual "lg"
 
Write-Host "✅ Git Super-Alias Pack Installed Successfully!" -ForegroundColor Green
Write-Host "Use 'git lg' for a visual log, 'git st' for status, 'git nb <branch>' to create a branch, etc." -ForegroundColor Cyan