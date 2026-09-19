# Pilotage VAE — EI Groupe

Système de pilotage du service VAE : base de données unique, moteur d'alertes,
suivi des parcours de la faisabilité à la certification, référentiel des
certifications synchronisé avec France Compétences, et interfaces de saisie.

Le tableau de bord n'est que la couche de visualisation. Tous les calculs
métier — grille tarifaire, alertes, délais, charge, marge — sont faits côté
serveur, pour qu'un chiffre présenté en direction soit exactement celui que
voit l'AAP dans son plan d'action.

---

## Sommaire

- [Architecture](#architecture)
- [Démarrage](#démarrage)
- [Grilles tarifaires](#grilles-tarifaires)
- [Étapes du parcours](#étapes-du-parcours)
- [Moteur d'alertes](#moteur-dalertes)
- [Notifications](#notifications)
- [Dossier candidat](#dossier-candidat)
- [Intégrations](#intégrations)
- [Reprise des données](#reprise-des-données)
- [Déploiement](#déploiement)
- [Ce qui reste à arbitrer](#ce-qui-reste-à-arbitrer)

---

## Architecture

```
EiVae.slnx
├── src/
│   ├── EiVae.Domain/           entités, règles métier, calculs — sans dépendance technique
│   │   ├── Entities/           candidat, parcours, certification, intervenant, grille…
│   │   └── Services/           TarificationService, MoteurAlertes, AffectationService,
│   │                           SharePointLinkBuilder
│   ├── EiVae.Infrastructure/   EF Core + PostgreSQL, importeurs, amorçage
│   │   ├── Integrations/
│   │   │   ├── FranceCompetences/   import RNCP quotidien (XML v4.1, lecture en flux)
│   │   │   └── FranceVae/           client interop + import CSV du back-office
│   │   ├── Seeding/            grilles, paramètres, reprise des données Excel
│   │   └── Migrations/         schéma versionné
│   └── EiVae.Api/              ASP.NET Core Minimal API + interface web
│       ├── Endpoints/          parcours, intervenants, certifications, imports, référentiel
│       ├── Services/           agrégations du tableau de bord, tâche de fond
│       └── wwwroot/            interface (HTML/CSS/JS sans dépendance ni build)
├── tests/EiVae.Tests/          56 tests : tarification, alertes, SharePoint, classification
├── seed/                       certifications.json + reprise.json
└── tools/                      extraction France Compétences et reprise Excel (Python)
```

**Pile technique** : .NET 10, ASP.NET Core, EF Core 10, PostgreSQL 17.
L'interface est en JavaScript sans framework ni étape de build : elle se
déploie en copiant `wwwroot`.

**Huit briques fonctionnelles**, telles que définies dans les règles de gestion :
candidats · parcours VAE · certifications · ressources humaines VAE ·
financement et facturation · pédagogie et LMS · qualité et conformité ·
pilotage et reporting. Au-dessus, un moteur d'alertes transverse.

---

## Démarrage

### Prérequis

- [.NET SDK 10](https://dotnet.microsoft.com/download) ou supérieur
- PostgreSQL 15 ou supérieur

Sur ce poste, le SDK .NET 10 et les runtimes .NET / ASP.NET Core 10 sont
installés pour la machine entière : `dotnet` fonctionne directement.


### Base de données

L'application utilise le **service PostgreSQL 17** du poste
(`postgresql-x64-17`, port 5432), qui démarre automatiquement avec Windows.

**Première utilisation** — une seule fois, depuis le dossier du projet :

```
.\db.cmd installer
```

Le script demande le mot de passe du superutilisateur `postgres` — celui choisi
à l'installation de PostgreSQL — puis crée le rôle et la base `eivae` et
restaure la sauvegarde la plus récente du dossier `sauvegardes\`. Sans
sauvegarde, la base reste vide et l'application charge elle-même les
référentiels et la reprise des données (`seed\`) au premier démarrage.
La commande est idempotente : la relancer ne restaure jamais par-dessus des
données existantes.

Ensuite :

```
.\db.cmd status        REM état du service et volumétrie de la base
.\db.cmd sauvegarder   REM exporte la base dans sauvegardes\eivae-AAAA-MM-JJ.sql
.\db.cmd psql          REM console SQL sur la base eivae
```

`db.cmd` est un fichier `.cmd`, pas un script PowerShell : la stratégie
d'exécution de PowerShell ne s'y applique pas.

**Mot de passe `postgres` perdu ?** Depuis une console ouverte en
administrateur : dans `C:\Program Files\PostgreSQL\17\data\pg_hba.conf`,
remplacez temporairement `scram-sha-256` par `trust` sur les lignes `127.0.0.1`
et `::1`, redémarrez le service (`net stop postgresql-x64-17` puis
`net start postgresql-x64-17`), fixez un nouveau mot de passe avec
`psql -U postgres -c "ALTER USER postgres PASSWORD 'nouveau'"`, puis
**rétablissez `scram-sha-256`** et redémarrez à nouveau le service.

> **Pourquoi pas une installation « portable » ?** L'application Claude pour
> Windows est un paquet MSIX : tout ce qu'elle écrit dans `%LOCALAPPDATA%` est
> redirigé vers son propre conteneur
> (`%LOCALAPPDATA%\Packages\Claude_…\LocalCache\Local\`), invisible depuis une
> console ordinaire. Une première version de ce projet avait installé
> PostgreSQL et un SDK .NET à cet endroit ; ils n'étaient utilisables que depuis
> l'application. Les données en ont été extraites dans
> `sauvegardes\eivae-2026-08-21.sql`.

**Sur un serveur**, même principe : PostgreSQL installé en service, puis la
chaîne de connexion fournie hors du dépôt :

```bash
dotnet user-secrets --project src/EiVae.Api set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=eivae;Username=eivae;Password=motdepasse"
```


### Lancement

```bash
dotnet run --project src/EiVae.Api
```

Le schéma est créé et les référentiels chargés au premier démarrage.
L'application écoute sur `http://localhost:5280` ; la documentation de l'API est
servie sur `/api/doc`.

### Tests

```bash
dotnet test
```

---

## Grilles tarifaires

**Un parcours est facturé à la grille en vigueur au jour de son démarrage.**
La revalorisation du 15 juin 2026 ne s'applique donc qu'aux parcours démarrés à
partir de cette date ; les parcours antérieurs restent à la grille 2025 jusqu'à
leur clôture, y compris pour les heures réalisées après cette date.

C'est la règle qui protège la cohérence entre le devis signé, les prestations
délivrées et la facture — exigence de la Caisse des Dépôts sur les parcours
financés par le CPF.

| | Grille 2025 | Grille 2026 |
|---|---|---|
| Période | jusqu'au 14/06/2026 | à partir du 15/06/2026 |
| Forfait architecture | 300 € | **350 €** |
| Accompagnement individuel | 70 €/h | **75 €/h** |
| Accompagnement collectif | 35 €/h | **40 €/h** |
| Compléments formatifs | 25 €/h | **30 €/h** |
| Frais de jury | 350 € | 350 € |
| Plafond mobilisable | 5 850 € | 5 850 € |

Plafonds horaires inchangés : 30 h individuel, 20 h collectif, 70 h de
compléments formatifs pour une certification visée en totalité.

**La date de démarrage** (`Parcours.DateDebutParcours`) est calculée à la
première valeur disponible — parcours validé, dépôt de faisabilité, recueil des
besoins, demande — et reste modifiable à la main. La grille est ensuite **figée
sur le dossier** : une revalorisation ultérieure ne le suit pas.

Les montants se calculent toujours sur les **heures réellement prescrites**.
Les plafonds ne sont jamais appliqués d'office : un dépassement produit un
signalement, à charge du service d'arbitrer.

Une nouvelle grille s'ajoute depuis l'interface (Règles de gestion → Ajouter une
grille) ; la précédente est automatiquement close la veille de la date d'effet.

---

## Étapes du parcours

| Rang | Étape |
|---|---|
| E1 | Demande reçue |
| E2 | Qualification |
| E3 | Recueil des besoins |
| E4 | Financement |
| E5 | Faisabilité |
| E6 | Recevabilité validée |
| E7 | Parcours validé |
| E8 | Accompagnement |
| E9 | Préparation jury |
| E10 | Jury |
| E11 | Post-jury |
| E12 | Clôturé |

Le financement se sécurise avant la faisabilité, et la recevabilité précède la
validation du parcours. Les rangs sont stockés en base : la migration
`EvolutionsOperationnelles` a renuméroté les dossiers, l'historique des étapes et
les pièces exigibles.

---

## Moteur d'alertes

Les règles sont des **données** : elles se créent, se modifient, se désactivent
et se suppriment depuis **Règles de gestion → Règles d'alerte**, sans
redéploiement. Chaque règle choisit un type de condition et le paramètre :

| Condition | Paramètres |
|---|---|
| Délai depuis un jalon | jalon de départ, jalon dont l'absence prolonge l'alerte, seuil, borne haute, jours ouvrés |
| Échéance proche | jalon futur, nombre de jours |
| Financement non sécurisé | — |
| Aucune facture émise | — |
| Acteur non affecté | AAP, accompagnateur ou gestionnaire |
| Consommation des heures | pourcentage des heures prescrites |
| Statut secondaire | statut surveillé |
| Consentement RGPD manquant | — |

Toutes peuvent être bornées à une plage d'étapes. Règles livrées :

| Code | Sévérité | Règle | Fondement |
|---|---|---|---|
| R01 | Critique | Premier RDV pédagogique au-delà de 8 jours ouvrés | France VAE |
| R02 | Critique | Financement non sécurisé, de la faisabilité à la préparation du jury | Caisse des Dépôts |
| R03 | Critique | Dossier sans évolution depuis plus de 45 jours | interne |
| R04 | Vigilance | Dossier ralenti, 21 à 45 jours sans mouvement | interne |
| R05 | Critique | Recevabilité au-delà de 2 mois | réglementaire |
| R06 | Critique | Jury non organisé 3 mois après dépôt | réglementaire |
| R07 | Vigilance | Jury programmé dans moins de 30 jours | interne |
| R08 | Vigilance | Enveloppe d'heures consommée à 85 % | cohérence devis / prestations |
| R09 | Critique | Parcours au-delà de 12 mois | objectif national 6 à 8 mois |
| R10 | Vigilance | Parcours long, 8 à 12 mois | objectif national |
| R11 | Vigilance | Pas de recueil des besoins 60 jours après la demande | interne |
| R12 | Critique | Prestation réalisée non facturée | interne |
| R13 | Vigilance | Accompagnement sans accompagnateur affecté | interne |
| R14 | Vigilance | Dossier actif sans AAP référent | interne |
| R15 | Vigilance | Dossier en attente du candidat | interne |

Les seuils réglés auparavant dans les paramètres de gestion ont été repris dans
les règles par la migration. Le code d'une règle ne change pas : il identifie
les alertes déjà levées.

Les alertes sont **historisées** : une alerte qui cesse d'être vraie est close,
pas supprimée. Une règle supprimée clôt ses alertes ouvertes.

**Résoudre une alerte.** Le bouton *Résoudre*, sur la fiche candidat comme dans
le plan d'action, propose l'action qui lève la règle : saisir le jalon manquant,
sécuriser le financement, émettre la facture, affecter l'intervenant, ajuster
l'enveloppe d'heures, lever le statut d'attente, consigner un contact. Les
alertes qui ne se lèvent qu'en faisant avancer le dossier (durée de parcours) se
reportent. Chaque résolution est tracée dans le journal d'audit.

---

## Notifications

| Événement | Destinataires |
|---|---|
| Création d'un candidat | membres des rôles abonnés — *Coordination pédagogique* par défaut |
| Affectation d'un AAP référent | l'AAP affecté |
| Affectation d'un accompagnateur | l'accompagnateur affecté |
| Recevabilité validée (étape E6 atteinte ou date de recevabilité saisie), avec le lien vers la fiche Word | membres des rôles abonnés — *Digital learning* par défaut |
| Recevabilité validée sans accompagnateur : accompagnateur à attribuer | membres des rôles abonnés — *Coordination pédagogique* par défaut |
| Parcours prescrit (étape « Parcours validé » ou date de validation saisie) | membres des rôles abonnés — *Administratif & finance* par défaut |

Les abonnements se règlent dans la matrice des droits, colonnes *Notifié*, et
les personnes se rattachent aux rôles dans **Règles de gestion → Utilisateurs**.
Un rôle abonné sans utilisateur reçoit la notification à son nom. Un import CSV
produit une notification récapitulative plutôt qu'une par candidat. Chaque
événement n'est notifié qu'une fois par dossier.

Le lien vers la fiche Word pointe sur le fichier dans SharePoint quand le
dossier y est synchronisé ; sinon sur son téléchargement depuis l'application.

Les notifications se consultent dans **Opérationnel → Notifications**. L'envoi
par courriel est optionnel ; il s'active en renseignant un serveur SMTP :

```json
"Notifications": {
  "UrlApplication": "https://vae.groupe-ei.fr",
  "Smtp": {
    "Hote": "smtp.office365.com", "Port": 587, "Ssl": true,
    "Utilisateur": "pilotage-vae@groupe-ei.fr", "MotDePasse": "…",
    "Expediteur": "pilotage-vae@groupe-ei.fr"
  }
}
```

Le mot de passe se place dans les secrets utilisateur ou une variable
d'environnement (`Notifications__Smtp__MotDePasse`), jamais dans le fichier.
Chaque courriel porte un lien direct vers la fiche (`#fiche-123`).

---

## Dossier candidat

À la création d'un candidat — saisie, qualification d'une demande du site ou
import — l'application crée le dossier `NOM_Prenom_Certification` et y dépose
`Fiche_candidat_NOM_Prenom.docx` : identité, demande, certification, acteurs,
parcours prescrit, jalons et jury, financements, factures, consentement, notes.
La fiche est **régénérée à chaque modification du dossier** : saisie, jalons,
séance, financement, facture, affectation, consentement, résolution d'alerte. Le bouton *Créer ou mettre à jour le
dossier* de la fiche régénère la fiche Word ; *Fiche Word* la télécharge.

L'emplacement se règle par `DossiersCandidats:Racine`. Sur le serveur, indiquez
le dossier **Candidats de la bibliothèque SharePoint synchronisée par OneDrive** :
les dossiers créés remontent alors dans SharePoint, sans permission Graph à
demander. En développement, ils sont créés dans `dossiers-candidats/` à la racine
du projet (exclu de Git : données personnelles).

Le nom suit le gabarit SharePoint, pour que le lien profond de la fiche ouvre ce
même dossier. Il est figé à la première génération : renommer le candidat ne
déplace pas son dossier.

---

## Intégrations

### France Compétences — opérationnelle

Export officiel du RNCP publié quotidiennement sur data.gouv.fr, en accès libre
et sans authentification. Le fichier XML pèse environ 470 Mo décompressé : il est
lu en flux, jamais chargé en mémoire.

L'import ne touche **que** les champs dont France Compétences est la source de
vérité : intitulé, niveau, blocs de compétences, certificateurs, voies d'accès
dont la VAE, composition du jury VAE, codes NSF/ROME/Formacode, dates
réglementaires, statistiques.

Les champs internes — abrégé du service, domaine EI, statut interne, durée
habituelle, particularités, contact certificateur, habilitations, modules
EI Académie — ne sont **jamais** écrasés. C'est cette séparation qui rend la
synchronisation nocturne sûre.

Synchronisation automatique à 4 h 30, ou manuelle depuis Intégrations.

### France VAE — portée limitée par l'API

L'API d'interopérabilité existe (`/interop/v1`, JWT Keycloak) mais, d'après le
dépôt public de la plateforme, elle est **destinée aux certificateurs** et
n'expose pour les candidatures qu'une lecture unitaire :
`GET /interop/v1/candidatures/{id}`. **Il n'existe pas de route de listing
permettant à un AAP de récupérer le flux de ses candidatures entrantes.**

Conséquence : le connecteur sert à rafraîchir un dossier dont l'identifiant est
déjà connu. L'alimentation du flux entrant passe par **l'import de l'export du
back-office** — analyse des en-têtes, correspondance des colonnes proposée
automatiquement, simulation avant écriture. C'est la voie praticable
aujourd'hui.

Configuration : section `FranceVae` (`Jeton`, ou `TokenUrl` + `ClientId` +
`ClientSecret` pour un renouvellement automatique).

### Site groupe-ei.fr — opérationnelle

Le formulaire poste sur `POST /api/public/candidature` avec l'en-tête
`X-EiVae-Secret` (comparaison à temps constant). Les demandes n'entrent pas
directement dans la base : elles rejoignent une file d'attente et attendent une
qualification humaine, ce qui évite d'y déverser les soumissions automatisées.
Le code RNCP est détecté automatiquement dans le texte saisi.

Configuration : `SiteWeb:Secret`.

### SharePoint — liens profonds

L'application ne lit ni n'écrit dans SharePoint. Elle conserve le **chemin
relatif** de chaque dossier et sait reconstruire une URL ouvrable : déplacer ou
renommer le site n'invalide aucune donnée, et aucune permission Entra ID n'est à
demander.

Le gabarit de nommage est configurable pour épouser l'arborescence réelle
décidée lors de la migration :

```json
"SharePoint": {
  "SiteUrl": "https://eigroupe.sharepoint.com/sites/VAE",
  "Bibliotheque": "Documents partages",
  "RacineCandidats": "Candidats",
  "GabaritDossierCandidat": "{nom}_{prenom}_{certification}"
}
```

Jetons : `{annee}` `{nom}` `{prenom}` `{parcoursId}` `{certification}` `{acf}`.
Un jeton non renseigné ne laisse pas de séparateur orphelin dans le nom du dossier.
La certification prend l'abrégé, à défaut l'intitulé tronqué à 60 caractères.

---

## Reprise des données

Deux scripts Python produisent les fichiers d'amorçage. Ils ne tournent qu'une
fois ; en production, c'est l'importeur C# qui prend le relais.

```bash
# Référentiel des certifications depuis l'export officiel
python tools/extract_france_competences.py \
  --zip export-fiches-rncp-v4-1-AAAA-MM-JJ.zip \
  --codes tools/rncp_codes.txt \
  --out seed/certifications.json

# Dossiers, intervenants et factures depuis les fichiers de suivi
python tools/extract_reprise.py
```

Reprise effectuée : 123 certifications, 590 blocs de compétences, 88 dossiers,
16 intervenants, 18 financements, 11 factures.

Le chargement est **idempotent** : chaque entité est rapprochée sur une clé
naturelle — code RNCP, nom d'intervenant, numéro de facture. Relancer l'amorçage
ne crée aucun doublon.

**Limites connues de la reprise**, à traiter manuellement :

- 14 factures sur 27 n'ont pas pu être rattachées : elles concernent des
  candidats absents du tableau de suivi (lots UDD).
- Deux factures ont été écartées car rattachables à des homonymes ; elles sont
  signalées dans le journal de démarrage.
- Les résultats de jury étaient saisis en texte libre. Le champ est désormais
  structuré, mais les dossiers repris apparaissent en « non renseigné » tant
  qu'ils n'ont pas été complétés — c'est la mesure exacte du trou de traçabilité
  à combler avant le prochain audit.
- Les heures réalisées ont été reconstituées depuis les colonnes de suivi. Le
  module de saisie des séances les remplace pour tous les nouveaux parcours.

---

## Déploiement

Deux formes de publication, selon ce qui est installé sur le serveur cible.

**Autonome — recommandée.** L'exécutable embarque le runtime : aucun .NET à
installer ni à maintenir sur le serveur, et aucun risque qu'une version
préexistante entre en conflit.

```bash
dotnet publish src/EiVae.Api -c Release -r win-x64 --self-contained true -o publish
# Linux : -r linux-x64
```

Environ 120 Mo. Le dossier se copie tel quel sur le serveur et `EiVae.Api.exe`
démarre directement.

**Dépendante du framework.** Plus légère (14 Mo), mais le serveur doit porter le
**runtime ASP.NET Core 10**, pas seulement le runtime .NET :

```bash
dotnet publish src/EiVae.Api -c Release -o publish
```

Si le lanceur répond *You must install or update .NET to run this application*,
c'est que le runtime attendu est absent de l'emplacement où il le cherche.
Installez `Microsoft.DotNet.AspNetCore.10` sur le serveur, ou repassez à la
publication autonome.

Variables d'environnement attendues en production :

| Variable | Rôle |
|---|---|
| `ConnectionStrings__Postgres` | chaîne de connexion PostgreSQL |
| `SharePoint__SiteUrl` | racine du site SharePoint VAE |
| `SiteWeb__Secret` | secret partagé avec le formulaire du site |
| `FranceVae__Jeton` | jeton d'interopérabilité, si disponible |
| `DossiersCandidats__Racine` | dossier local synchronisé avec la racine Candidats de SharePoint |
| `Notifications__UrlApplication` | adresse publique, pour les liens des courriels |
| `Notifications__Smtp__Hote`, `__Expediteur`, `__Utilisateur`, `__MotDePasse` | envoi des notifications par courriel |
| `ASPNETCORE_URLS` | adresse d'écoute |

Placez l'application derrière un reverse proxy assurant TLS. Les migrations
s'appliquent au démarrage ; pour un déploiement contrôlé, passez
`Base__MigrerAuDemarrage=false` et appliquez-les séparément :

```bash
dotnet ef database update --project src/EiVae.Infrastructure --startup-project src/EiVae.Api
```

**Authentification** : l'application ne porte pas encore d'authentification. La
matrice des droits et les utilisateurs se gèrent dans l'application, mais les
droits ne sont pas appliqués : à brancher sur Entra ID avant toute mise en
service, l'API étant sinon ouverte à qui atteint le serveur.

---

## Ce qui reste à arbitrer

Ces points sont bloquants pour une mise en service complète, et relèvent d'une
décision du service, pas d'un choix technique.

1. **Authentification et application des droits.** La matrice et les
   utilisateurs existent ; le raccordement à Entra ID reste à faire.
2. **Quelle source fait foi.** France VAE, Solei, EI Académie et l'application
   portent des informations partiellement redondantes. Pour chaque donnée, une
   seule source doit être déclarée maîtresse.
3. **Qui saisit, et quand.** Le statut doit changer au moment de l'événement,
   pas lors d'une mise à jour hebdomadaire.
4. **Raccordement EI Académie.** Espace candidat, modules prescrits,
   progression, dernière activité : les champs existent, le flux reste à ouvrir.
5. **Rattachement Solei.** Une partie de l'activité VAE reste rattachée au SAP
   alors qu'elle est pilotée par l'unité MAD : le CA VAE réel ne peut pas encore
   être isolé de façon fiable.
6. **Enquêtes de satisfaction.** Le taux de retour actuel ne permet aucun
   indicateur qualité exploitable.

---

## Licence et données

Ce dépôt contient des données personnelles de candidats reprises des fichiers de
suivi du service. Il ne doit pas être publié ni diffusé hors d'EI Groupe.
