@echo off
setlocal EnableExtensions
rem ===========================================================================
rem  Pilote la base de donnees de l'application sur le service PostgreSQL
rem  du poste (postgresql-x64-17, port 5432, demarrage automatique).
rem
rem  Fichier .cmd et non script PowerShell : la strategie d'execution de
rem  PowerShell ne s'y applique pas.
rem
rem  Premiere utilisation :   db installer
rem    cree le role et la base "eivae", puis restaure la derniere sauvegarde
rem    trouvee dans le dossier sauvegardes\. Le mot de passe demande est celui
rem    du superutilisateur "postgres", choisi a l'installation de PostgreSQL.
rem
rem  Usage :  db installer | status | psql | info | sauvegarder | start | stop
rem
rem  EIVAE_PGBIN et EIVAE_PGPORT permettent de viser une autre installation.
rem ===========================================================================

if defined EIVAE_PGBIN (set "PGBIN=%EIVAE_PGBIN%") else (set "PGBIN=%ProgramFiles%\PostgreSQL\17\bin")
if defined EIVAE_PGPORT (set "PORT=%EIVAE_PGPORT%") else (set "PORT=5432")
set "SERVICE=postgresql-x64-17"
set "BASE=eivae"
set "ROLE=eivae"
set "RACINE=%~dp0"

if not exist "%PGBIN%\psql.exe" (
  echo.
  echo PostgreSQL 17 est introuvable dans %PGBIN%.
  echo Installez-le depuis une console ouverte en administrateur :
  echo.
  echo     winget install PostgreSQL.PostgreSQL.17
  echo.
  exit /b 1
)

set "ACTION=%~1"
if "%ACTION%"=="" set "ACTION=status"

if /i "%ACTION%"=="installer"   goto :installer
if /i "%ACTION%"=="status"      goto :status
if /i "%ACTION%"=="psql"        goto :psql
if /i "%ACTION%"=="info"        goto :info
if /i "%ACTION%"=="sauvegarder" goto :sauvegarder
if /i "%ACTION%"=="start"       goto :start
if /i "%ACTION%"=="stop"        goto :stop

echo Action inconnue : %ACTION%
echo Usage : db installer ^| status ^| psql ^| info ^| sauvegarder ^| start ^| stop
exit /b 1

rem ---------------------------------------------------------------------------
:repond
"%PGBIN%\pg_isready.exe" -h 127.0.0.1 -p %PORT% -U postgres -d postgres -q
exit /b %ERRORLEVEL%

rem ---------------------------------------------------------------------------
:installer
call :repond
if errorlevel 1 (
  echo Le serveur PostgreSQL ne repond pas sur le port %PORT%.
  echo Lancez "db start" depuis une console ouverte en administrateur.
  exit /b 1
)

rem  La plus recente sauvegarde : les noms sont dates, le tri decroissant suffit.
set "DUMP="
for /f "delims=" %%F in ('dir /b /o-n "%RACINE%sauvegardes\eivae-*.sql" 2^>nul') do if not defined DUMP set "DUMP=%%F"

echo.
echo Connexion en superutilisateur "postgres".
echo Le mot de passe demande est celui choisi a l'installation de PostgreSQL.
echo.

if not defined DUMP goto :installer_sans_sauvegarde

rem  psql resout \i par rapport au dossier courant : on s'y place et on ne
rem  transmet que le nom du fichier, ce qui evite tout echappement de chemin.
echo Sauvegarde trouvee : sauvegardes\%DUMP%
echo.
pushd "%RACINE%sauvegardes"
"%PGBIN%\psql.exe" -h 127.0.0.1 -p %PORT% -U postgres -d postgres -v "dump=%DUMP%" -f "%RACINE%tools\installer-base.sql"
set "CODE=%ERRORLEVEL%"
popd
if not "%CODE%"=="0" goto :installer_echec
goto :installer_succes

:installer_sans_sauvegarde
"%PGBIN%\psql.exe" -h 127.0.0.1 -p %PORT% -U postgres -d postgres -f "%RACINE%tools\installer-base.sql"
if errorlevel 1 goto :installer_echec
goto :installer_succes

:installer_echec
echo.
echo L'installation a echoue. Si le mot de passe est refuse, voir la section
echo "Base de donnees" du README.
exit /b 1

:installer_succes
echo.
echo Installation terminee.
call :info
goto :fin

rem ---------------------------------------------------------------------------
:status
sc query %SERVICE% | find "RUNNING" >nul
if errorlevel 1 (echo Service %SERVICE% : arrete) else (echo Service %SERVICE% : demarre, lancement automatique avec Windows)
call :repond
if errorlevel 1 (
  echo Le serveur ne repond pas sur le port %PORT%.
  echo Lancez "db start" depuis une console ouverte en administrateur.
  goto :fin
)
call :info
goto :fin

rem ---------------------------------------------------------------------------
:psql
set "PGPASSWORD=eivae"
"%PGBIN%\psql.exe" -h 127.0.0.1 -p %PORT% -U %ROLE% -d %BASE%
goto :fin

rem ---------------------------------------------------------------------------
:info
echo.
echo Chaine de connexion :
echo   Host=127.0.0.1;Port=%PORT%;Database=%BASE%;Username=%ROLE%;Password=eivae
echo Deja renseignee dans src\EiVae.Api\appsettings.Development.json
echo.
set "PGPASSWORD=eivae"
set "SQLTMP=%TEMP%\eivae-info.sql"
>"%SQLTMP%" echo SELECT 'certifications', count(*) FROM certifications
>>"%SQLTMP%" echo UNION ALL SELECT 'blocs de competences', count(*) FROM blocs_competences
>>"%SQLTMP%" echo UNION ALL SELECT 'dossiers', count(*) FROM parcours
>>"%SQLTMP%" echo UNION ALL SELECT 'intervenants', count(*) FROM intervenants
>>"%SQLTMP%" echo UNION ALL SELECT 'factures', count(*) FROM factures
>>"%SQLTMP%" echo UNION ALL SELECT 'alertes ouvertes', count(*) FROM alertes WHERE "ResolueLe" IS NULL;
"%PGBIN%\psql.exe" -h 127.0.0.1 -p %PORT% -U %ROLE% -d %BASE% -tA -F " : " -f "%SQLTMP%" 2>nul
if errorlevel 1 (
  echo La base "%BASE%" n'est pas encore installee, ou pas encore initialisee :
  echo lancez "db installer", puis demarrez l'application.
)
del "%SQLTMP%" >nul 2>&1
set "PGPASSWORD="
exit /b 0

rem ---------------------------------------------------------------------------
:sauvegarder
for /f %%D in ('powershell -NoProfile -Command "Get-Date -Format yyyy-MM-dd-HHmm"') do set "JOUR=%%D"
if not exist "%RACINE%sauvegardes" mkdir "%RACINE%sauvegardes"
set "PGPASSWORD=eivae"
"%PGBIN%\pg_dump.exe" -h 127.0.0.1 -p %PORT% -U %ROLE% -d %BASE% --no-owner --no-privileges --encoding=UTF8 -f "%RACINE%sauvegardes\eivae-%JOUR%.sql"
if errorlevel 1 (echo La sauvegarde a echoue.) else (echo Sauvegarde ecrite : sauvegardes\eivae-%JOUR%.sql)
goto :fin

rem ---------------------------------------------------------------------------
:start
net start %SERVICE%
if errorlevel 1 echo Demarrer un service Windows demande une console ouverte en administrateur.
goto :fin

:stop
net stop %SERVICE%
if errorlevel 1 echo Arreter un service Windows demande une console ouverte en administrateur.
goto :fin

rem ---------------------------------------------------------------------------
:fin
endlocal
exit /b 0
