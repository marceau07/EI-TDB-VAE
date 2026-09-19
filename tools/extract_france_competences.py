# -*- coding: utf-8 -*-
"""
Extraction des fiches RNCP depuis l'export officiel France Competences.

Source : https://www.data.gouv.fr/datasets/repertoire-national-des-certifications-
         professionnelles-et-repertoire-specifique
Export quotidien `export-fiches-rncp-v4-1-AAAA-MM-JJ.zip` (XML v4.1, ~470 Mo
decompresse). Le fichier est parcouru en streaming : aucune charge memoire
proportionnelle a sa taille.

Ce script produit le fichier de seed `seed/certifications.json` consomme par
EiVae.Infrastructure.Seeding au premier demarrage. En production, c'est
l'ImportFranceCompetences (C#) qui rafraichit la base chaque nuit ; ce script
sert a l'amorcage et au diagnostic.

Usage :
    python tools/extract_france_competences.py --zip <export.zip> --codes <catalogue.xlsx>
"""
from __future__ import annotations

import argparse
import io
import json
import os
import re
import sys
import zipfile
import xml.etree.ElementTree as ET
from datetime import date, datetime

# ---------------------------------------------------------------------------
# Classification interne EI Groupe : les codes NSF de France Competences sont
# trop fins pour le pilotage du service. On les regroupe en domaines metier.
# Reference : nomenclature NSF (Nomenclature des Specialites de Formation).
# ---------------------------------------------------------------------------
NSF_DOMAINE = {
    "332": "Sanitaire & social",
    "330": "Sanitaire & social",
    "331": "Sanitaire & social",
    "333": "Petite enfance",
    "334": "Commerce",
    "336": "Commerce",
    "311": "Formation & RH",
    "315": "Formation & RH",
    "313": "Formation & RH",
    "310": "Formation & RH",
    "312": "Commerce",
    "314": "Formation & RH",
    "324": "Formation & RH",
    "320": "Animation & culture",
    "321": "Animation & culture",
    "322": "Animation & culture",
    "323": "Animation & culture",
    "335": "Animation & culture",
    "343": "Propreté & environnement",
    "344": "Sécurité",
    "345": "Formation & RH",
    "346": "Propreté & environnement",
    "311t": "Logistique & transport",
    "311u": "Logistique & transport",
    "342": "Commerce",
    "300": "Autres",
}
DOMAINE_MOTS = [
    ("Petite enfance", ["petite enfance", "jeunes enfants", "éducatif petite", "puéricult",
                        "périscolaire", "vie scolaire", "ludothé"]),
    ("Sanitaire & social", ["social", "médico", "aide-soignant", "soins", "éducateur", "educateur",
                            "famili", "caferuis", "gérontolog", "médiat", "insertion",
                            "orthopédago", "handicap", "auxiliaire de vie", "assistant de service"]),
    ("Service à la personne", ["domicile", "vie aux familles", "grand âge", "aide à domicile"]),
    ("Formation & RH", ["formateur", "formation", "pédagog", "ressources humaines", "compétences",
                        "management", "ingénierie"]),
    ("Logistique & transport", ["logisti", "transport", "magasin", "entrepos", "commande",
                                "déménag", "chaine logistique", "chaîne logistique"]),
    ("Commerce", ["commerc", "vente", "client", "négoce", "esthéti", "thermal"]),
    ("Sécurité", ["sécurit", "sûreté", "surveillance", "vidéoprotection", "prévention",
                  "gardien", "concierge", "incendie"]),
    ("Propreté & environnement", ["propreté", "hygiène", "nettoy", "vitres", "environnement",
                                  "biocontamin", "nuisible", "réemploi", "valoriste", "stérilis"]),
    ("Animation & culture", ["animation", "animateur", "esport", "sportive", "artistique",
                             "culturel", "multimédia", "graphi", "motion", "designer", "numérique"]),
    ("Agriculture", ["agricol", "agrofourn", "équine", "rural"]),
]


def domaine_ei(intitule: str, nsf_codes: list[str]) -> str:
    """Domaine metier EI Groupe, deduit du libelle puis du code NSF."""
    low = (intitule or "").lower()
    for nom, mots in DOMAINE_MOTS:
        if any(m in low for m in mots):
            return nom
    for c in nsf_codes:
        for key in (c, c[:3]):
            if key in NSF_DOMAINE:
                return NSF_DOMAINE[key]
    return "Autres"


def txt(el, path, default=""):
    node = el.find(path)
    if node is None or node.text is None:
        return default
    return re.sub(r"[ \t]+", " ", node.text).strip()


def oui(el, path) -> bool:
    return txt(el, path).strip().lower() in ("oui", "true", "1")


def iso(val: str) -> str | None:
    """Les dates de l'export sont au format JJ/MM/AAAA."""
    val = (val or "").strip()
    if not val:
        return None
    for fmt in ("%d/%m/%Y", "%Y-%m-%d", "%d-%m-%Y"):
        try:
            return datetime.strptime(val, fmt).date().isoformat()
        except ValueError:
            continue
    return None


def parse_fiche(el: ET.Element) -> dict:
    numero = txt(el, "NUMERO_FICHE")
    intitule = txt(el, "INTITULE")

    nsf = [{"code": txt(n, "CODE"), "libelle": txt(n, "LIBELLE")}
           for n in el.findall("./CODES_NSF/NSF")]
    formacodes = [{"code": txt(n, "CODE"), "libelle": txt(n, "LIBELLE")}
                  for n in el.findall("./FORMACODES/FORMACODE")]
    rome = [{"code": txt(n, "CODE"), "libelle": txt(n, "LIBELLE")}
            for n in el.findall("./CODES_ROME/ROME")]
    certificateurs = [{
        "siret": txt(n, "SIRET_CERTIFICATEUR"),
        "nom": txt(n, "NOM_CERTIFICATEUR"),
        "etat": txt(n, "ETAT_CERTIFICATEUR"),
    } for n in el.findall("./CERTIFICATEURS/CERTIFICATEUR")]

    blocs = []
    for b in el.findall("./BLOCS_COMPETENCES/BLOC_COMPETENCES"):
        blocs.append({
            "code": txt(b, "CODE"),
            "libelle": txt(b, "LIBELLE"),
            "competences": txt(b, "LISTE_COMPETENCES"),
            "modalitesEvaluation": txt(b, "MODALITES_EVALUATION"),
        })

    stats = []
    for s in el.findall("./STATISTIQUES_PROMOTIONS/STATISTIQUE_PROMOTION") or \
             el.findall("./STATISTIQUES_PROMOTIONS/STATISTIQUES_PROMOTION"):
        stats.append({
            "annee": txt(s, "ANNEE"),
            "certifies": txt(s, "NOMBRE_CERTIFIES"),
            "certifiesVae": txt(s, "NOMBRE_CERTIFIES_VAE"),
            "insertionGlobale6Mois": txt(s, "TAUX_INSERTION_GLOBAL_6MOIS"),
            "insertionMetier2Ans": txt(s, "TAUX_INSERTION_METIER_2ANS"),
        })

    # Voies d'acces : chaque SI_JURY_* est un conteneur portant ACTIF (Oui/Non)
    # et, quand la voie est ouverte, COMPOSITION (composition du jury).
    voies = {
        "vae": oui(el, "SI_JURY_VAE/ACTIF"),
        "formationInitiale": oui(el, "SI_JURY_FI/ACTIF"),
        "formationContinue": oui(el, "SI_JURY_FC/ACTIF"),
        "apprentissage": oui(el, "SI_JURY_CA/ACTIF"),
        "contratProfessionnalisation": oui(el, "SI_JURY_CQ/ACTIF"),
        "candidatLibre": oui(el, "SI_JURY_CL/ACTIF"),
    }
    # La composition du jury VAE est l'element central des « modalites de VAE ».
    composition_jury_vae = txt(el, "SI_JURY_VAE/COMPOSITION")

    etat = txt(el, "ETAT_FICHE")
    fin = iso(txt(el, "DATE_FIN_ENREGISTREMENT"))
    actif = etat.lower().startswith("publi") and (
        fin is None or fin >= date.today().isoformat()
    )

    return {
        "codeRncp": numero,
        "idFiche": txt(el, "ID_FICHE"),
        "intitule": intitule,
        "etatFiche": etat,
        "actif": actif,
        "niveau": _niveau(txt(el, "./NOMENCLATURE_EUROPE/NIVEAU")),
        "libelleNiveau": txt(el, "./NOMENCLATURE_EUROPE/LIBELLE"),
        "typeEnregistrement": txt(el, "TYPE_ENREGISTREMENT"),
        "dateDecision": iso(txt(el, "DATE_DECISION")),
        "dureeEnregistrement": txt(el, "DUREE_ENREGISTREMENT"),
        "dateFinEnregistrement": fin,
        "dateLimiteDelivrance": iso(txt(el, "DATE_LIMITE_DELIVRANCE")),
        "dateDerniereModification": iso(txt(el, "DATE_DERNIERE_MODIFICATION")),
        "codesNsf": nsf,
        "formacodes": formacodes,
        "codesRome": rome,
        "certificateurs": certificateurs,
        "existencePartenaires": txt(el, "EXISTENCE_PARTENAIRES"),
        "voiesAcces": voies,
        "compositionJuryVae": composition_jury_vae,
        "blocs": blocs,
        "activitesVisees": txt(el, "ACTIVITES_VISEES"),
        "capacitesAttestees": txt(el, "CAPACITES_ATTESTEES"),
        "secteursActivite": txt(el, "SECTEURS_ACTIVITE"),
        "typeEmploiAccessibles": txt(el, "TYPE_EMPLOI_ACCESSIBLES"),
        "objectifsContexte": txt(el, "OBJECTIFS_CONTEXTE"),
        "prerequis": txt(el, "PREREQUIS_ENTREE_FORMATION"),
        "reglementationActivites": txt(el, "REGLEMENTATIONS_ACTIVITES"),
        "statistiques": stats,
        "lienDescription": txt(el, "LIEN_URL_DESCRIPTION"),
        "lienFranceCompetences":
            f"https://www.francecompetences.fr/recherche/rncp/{numero.replace('RNCP', '')}/",
        "domaineEi": domaine_ei(intitule, [c["code"] for c in nsf]),
    }


def _niveau(niv: str) -> int | None:
    m = re.search(r"(\d)", niv or "")
    return int(m.group(1)) if m else None


def iter_fiches(zip_path: str):
    """Parcourt l'export en streaming (iterparse) et libere chaque fiche lue."""
    with zipfile.ZipFile(zip_path) as z:
        name = z.infolist()[0].filename
        with z.open(name) as raw:
            for event, el in ET.iterparse(raw, events=("end",)):
                if el.tag == "FICHE":
                    yield el
                    el.clear()


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--zip", required=True, help="export-fiches-rncp-v4-1-*.zip")
    ap.add_argument("--codes", help="fichier texte : un code RNCP par ligne (sinon : toutes)")
    ap.add_argument("--out", default="seed/certifications.json")
    args = ap.parse_args()

    wanted = None
    if args.codes:
        with io.open(args.codes, encoding="utf-8") as f:
            wanted = {("RNCP" + l.strip().replace("RNCP", "")) for l in f if l.strip()}
        print(f"{len(wanted)} codes RNCP recherches", file=sys.stderr)

    out, seen, total = [], set(), 0
    for el in iter_fiches(args.zip):
        total += 1
        numero = txt(el, "NUMERO_FICHE")
        if wanted is not None and numero not in wanted:
            continue
        if numero in seen:
            continue
        seen.add(numero)
        out.append(parse_fiche(el))
        if wanted is not None and len(seen) == len(wanted):
            break

    os.makedirs(os.path.dirname(args.out) or ".", exist_ok=True)
    with io.open(args.out, "w", encoding="utf-8") as f:
        json.dump({
            "source": "France Competences - export RNCP v4.1",
            "extraitLe": date.today().isoformat(),
            "nombre": len(out),
            "certifications": out,
        }, f, ensure_ascii=False, indent=1)

    manquants = sorted(wanted - seen) if wanted else []
    print(f"{total} fiches parcourues, {len(out)} retenues -> {args.out}", file=sys.stderr)
    if manquants:
        print(f"{len(manquants)} codes introuvables : {', '.join(manquants[:20])}", file=sys.stderr)
    avec_blocs = sum(1 for c in out if c["blocs"])
    avec_vae = sum(1 for c in out if c["voiesAcces"]["vae"])
    print(f"{avec_blocs} fiches avec blocs de competences, {avec_vae} ouvertes a la VAE",
          file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
