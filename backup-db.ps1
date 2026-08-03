# Hace un backup completo de la base de datos MySQL (instalada directo en
# Windows, no en Docker) a un archivo .sql con la fecha y hora en el nombre.
#
# Antes de usar esto por primera vez, hace falta crear mysql-backup.cnf
# con la contrasena (instrucciones aparte, no van en este archivo).
#
# Uso manual:
#   .\backup-db.ps1

$ErrorActionPreference = "Stop"

# Ruta al mysqldump.exe de tu instalacion de MySQL Server (no el de MySQL
# Workbench). Si en algun momento cambia la version instalada, ajustar aca.
$mysqldump = "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqldump.exe"

$carpetaBackups = Join-Path $PSScriptRoot "backups"
New-Item -ItemType Directory -Force -Path $carpetaBackups | Out-Null

$fecha = Get-Date -Format "yyyy-MM-dd_HH-mm"
$archivo = Join-Path $carpetaBackups "odonto_backup_$fecha.sql"
$credenciales = Join-Path $PSScriptRoot "mysql-backup.cnf"
$nombreBaseDatos = "odonto"

if (-not (Test-Path $credenciales)) {
    Write-Host "Falta el archivo mysql-backup.cnf con las credenciales. Revisa las instrucciones de configuracion." -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $mysqldump)) {
    Write-Host "No encontre mysqldump.exe en $mysqldump. Ajusta la variable `$mysqldump al principio del script." -ForegroundColor Red
    exit 1
}

Write-Host "Generando backup en $archivo ..."

& $mysqldump --defaults-extra-file="$credenciales" $nombreBaseDatos > $archivo

if ($LASTEXITCODE -eq 0 -and (Test-Path $archivo) -and (Get-Item $archivo).Length -gt 0) {
    Write-Host "Backup OK: $archivo" -ForegroundColor Green
}
else {
    Write-Host "ERROR: el backup no se genero bien." -ForegroundColor Red
    if (Test-Path $archivo) { Remove-Item $archivo }
    exit 1
}

# -- Para que corra solo, todos los dias --------------------------------
# Una sola vez, desde PowerShell (como administrador si te lo pide):
#
#   schtasks /create /tn "Backup Odonto SaaS" /tr "powershell.exe -ExecutionPolicy Bypass -File E:\odontologos\backup-db.ps1" /sc daily /st 02:00
#
# Eso crea una tarea programada de Windows que corre este script todos los
# dias a las 2:00 AM (con la compu prendida). Para verla o borrarla mas
# adelante: abri "Programador de tareas" de Windows y busca "Backup Odonto SaaS".
