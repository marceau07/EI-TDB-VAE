-- ===========================================================================
--  Prépare la base de l'application sur le service PostgreSQL du poste.
--
--  Exécuté par « db.cmd installer », connecté en superutilisateur postgres.
--  Idempotent : le relancer ne recrée rien et ne restaure jamais par-dessus
--  une base qui contient déjà des données.
--
--  Variable optionnelle :
--    dump   nom d'une sauvegarde produite par pg_dump, relatif au dossier
--           courant : db.cmd se place dans sauvegardes\ avant l'appel, ce qui
--           evite tout echappement de chemin, espaces compris.
-- ===========================================================================

\set ON_ERROR_STOP on
\set QUIET on
-- Les messages affiches restent en ASCII : la console Windows n'est pas en UTF-8.

-- Rôle applicatif. Mot de passe de développement, repris dans
-- appsettings.Development.json ; à changer sur un serveur partagé.
SELECT 'CREATE ROLE eivae LOGIN PASSWORD ''eivae'''
 WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'eivae') \gexec

-- template0 et UTF8 explicites : les données contiennent des accents, et
-- template1 hérite de l'encodage choisi à l'installation du cluster.
SELECT 'CREATE DATABASE eivae OWNER eivae ENCODING ''UTF8'' TEMPLATE template0'
 WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'eivae') \gexec

\echo 'Role et base eivae prets.'

-- Même utilisateur, même hôte : psql réutilise le mot de passe déjà saisi.
\c eivae

SELECT EXISTS (
    SELECT FROM information_schema.tables
     WHERE table_schema = 'public' AND table_name = 'parcours'
) AS deja_peuplee \gset

\if :deja_peuplee
    \echo 'La base contient deja des donnees : aucune restauration.'
\elif :{?dump}
    \echo 'Restauration de la sauvegarde' :dump
    -- Les objets restaurés doivent appartenir au rôle applicatif, pas à postgres.
    SET ROLE eivae;
    -- La sauvegarde recale ses sequences par des SELECT setval(...) dont le
    -- resultat n'interesse personne : on l'envoie au peripherique nul.
    \o NUL
    \i :dump
    \o
    RESET ROLE;
    \echo 'Restauration terminee.'
\else
    \echo 'Aucune sauvegarde fournie : l''application chargera les referentiels au premier demarrage.'
\endif
