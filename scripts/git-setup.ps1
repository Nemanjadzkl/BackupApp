param(
    [string]$RemoteUrl = ""
)

# Preskoči ako nije instaliran git
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Error "Git nije instaliran ili nije u PATH. Instaliraj ga pre pokretanja skripte."
    exit 1
}

# Idi u folder gde se nalazi skripta (treba da bude u root projekta)
Set-Location -Path (Split-Path -Parent $MyInvocation.MyCommand.Definition)

# Inicijalizacija repoa (ako već nema)
if (-not (git rev-parse --is-inside-work-tree 2>$null)) {
    git init
    Write-Host "Git inicijalizovan."
} else {
    Write-Host "Već je git repozitorij."
}

# Dodaj sve fajlove i commit (ako ima izmena)
git add -A
try {
    git commit -m "Initial commit" -q
    Write-Host "Commit napravljen."
} catch {
    Write-Host "Nema novih fajlova za commit ili commit neuspešan: $($_.Exception.Message)"
}

# Ako je prosleđen remote URL, dodaj i pushuj
if ($RemoteUrl -and $RemoteUrl.Trim().Length -gt 0) {
    if (-not (git remote get-url origin 2>$null)) {
        git remote add origin $RemoteUrl
        Write-Host "Remote origin dodat: $RemoteUrl"
    } else {
        Write-Host "Remote origin već postoji."
    }

    git branch -M main
    git push -u origin main
    Write-Host "Push-ovano na origin/main."
} else {
    Write-Host "Nije prosleđen remote URL. Ako želiš da push-uješ na udaljeni repo, koristi:"
    Write-Host "  git remote add origin <URL>"
    Write-Host "  git branch -M main"
    Write-Host "  git push -u origin main"
}
