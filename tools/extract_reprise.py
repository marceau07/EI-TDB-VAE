# -*- coding: utf-8 -*-
"""
Reprise des donnees du service depuis les fichiers Excel de suivi.

Produit `seed/reprise.json`, consomme au premier demarrage par
EiVae.Infrastructure.Seeding.DonneesInitialesSeeder.

Le tableur de suivi melange, dans une meme colonne, un statut, un historique en
texte libre et des dates de seances. Ce script en extrait ce qui est
structurable et laisse le reste dans le champ `historique`, visible sur la fiche
du candidat : on ne jette rien, mais on ne pretend pas non plus avoir structure
ce qui ne l'est pas.

Usage :
    python tools/extract_reprise.py
"""
from __future__ import annotations

import datetime
import io
import json
import os
import re
import sys
import warnings

import openpyxl

warnings.filterwarnings("ignore")

RACINE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOURCES = os.path.join(RACINE, "sources", "Dashboard VAE")
SORTIE = os.path.join(RACINE, "seed", "reprise.json")

# ---------------------------------------------------------------------------
# Correspondances : abrege du service -> code RNCP officiel.
# Etablies a partir du catalogue des certifications d'EI Groupe.
# ---------------------------------------------------------------------------
CERTIFICATIONS = {
    "DE EJE": ("RNCP37679", "DE EJE"),
    "DE ES": ("RNCP37676", "DE ES"),
    "DE ME": ("RNCP39643", "DE ME"),
    "DE AES": ("RNCP36004", "DE AES"),
    "CAP AEPE": ("RNCP38565", "CAP AEPE"),
    "TP FPA": ("RNCP37275", "TP FPA"),
    "TP ADVF": ("RNCP37715", "TP ADVF"),
    "DE Aide-soignant": ("RNCP40692", "DE AS"),
    "DE Aux Puer": ("RNCP40743", "DE AP"),
    "DE AP": ("RNCP40743", "DE AP"),
    "DE CESF": ("RNCP37678", "DE CESF"),
    "CAFERUIS": ("RNCP36836", "CAFERUIS"),
    "RC SAD": ("RNCP39539", "RC SAD"),
    "BTS SPSSS": ("RNCP36939", "BTS SP3S"),
    "BTS ESF": ("RNCP36938", "BTS ESF"),
    "TP RPIF": ("RNCP38953", "TP RPIF"),
    "TP CIP": ("RNCP37274", "TP CIP"),
    "Auxiliaire de vie": ("RNCP39387", "Auxiliaire de vie"),
    "BAC PRO ASSP": ("RNCP37231", "BAC PRO ASSP"),
    "BTS Gestion des transports": ("RNCP38365", "BTS GTLA"),
    "TP Agent de propret": ("RNCP37872", "TP APH"),
    "TP Technicien Sup": ("RNCP37277", "TP TSMEL"),
}

# Statuts du tableur -> etape du parcours et statut secondaire.
ETAPES = {
    "Non traité": ("DemandeRecue", None, None),
    "Accusé réception": ("Qualification", None, None),
    "Dernière relance x 3": ("Qualification", "AttenteCandidat", None),
    "Définition parcours": ("RecueilDesBesoins", None, None),
    "Validation parcours": ("ParcoursValide", None, None),
    "Faisabilité": ("Faisabilite", "AttenteCertificateur", None),
    "Faisabilité validée": ("Financement", None, None),
    "Prise en charge en attente": ("Financement", "AttenteFinanceur", None),
    "Cherche financement hors FVAE": ("Financement", "AttenteFinanceur", None),
    "Accompagnement en cours": ("Accompagnement", None, None),
    "Dépôt dossier validation": ("PreparationJury", None, None),
    "Attente date jury": ("Jury", "AttenteCertificateur", None),
    "A facturer fin de parcours": ("PostJury", None, None),
    "Dossier facturé et clôturé": ("Cloture", None, None),
    "Abandon candidat ?": ("Sortie", None, "AbandonCandidat"),
    "Abandon candidat": ("Sortie", None, "AbandonCandidat"),
    "Abandon AAP": ("Sortie", None, "AbandonAap"),
    "Disparu(e) de France VAE": ("Sortie", None, "DisparuFranceVae"),
    "Candidature supprimée": ("Sortie", None, "CandidatureSupprimee"),
}

# Les intitules du tableur sont abreges et heterogenes ; on les normalise.
INTERVENANTS = {
    "IFG": "Isabelle GOUAULT FARISSIER",
    "IGF": "Isabelle GOUAULT FARISSIER",
    "Isabelle": "Isabelle GOUAULT FARISSIER",
    "Jade R.R": "Jade RAYNARD RODRIGUES",
    "Jade R.": "Jade RAYNARD RODRIGUES",
    "Jade R": "Jade RAYNARD RODRIGUES",
    "Jade": "Jade RAYNARD RODRIGUES",
    "Rodulfo DJ": "Rodulfo DOMINGUEZ JIMENEZ",
    "Mireille ABADIE": "Mireille ABADIE",
    "Miraille A.": "Mireille ABADIE",
    "Aurélie C.": "Aurélie CHENIER",
    "Aurélie C": "Aurélie CHENIER",
    "Tifaine": "Tifaine VENTRE",
    "marie": "Marie-Hélène",
    "Marie-Hélène": "Marie-Hélène",
    "Lucile GRAS": "Lucile GRAS",
    "Corinne ALLAVOINE": "Corinne ALLAVOINE",
    "Céline TERUEL": "Céline TERUEL",
    "Céline FANJAUD": "Céline FANJAUD",
    "Sylvie": "Sylvie",
    "IPERIA": "Mireille ABADIE",
}
AAP = {
    "Isabelle GOUAULT FARISSIER",
    "Jade RAYNARD RODRIGUES",
    "Rodulfo DOMINGUEZ JIMENEZ",
    "Mireille ABADIE",
}
REGIONS = {
    "34": "Occitanie", "31": "Occitanie", "30": "Occitanie", "11": "Occitanie", "66": "Occitanie",
    "13": "PACA", "83": "PACA", "84": "PACA", "06": "PACA",
    "63": "Auvergne-Rhône-Alpes", "69": "Auvergne-Rhône-Alpes", "43": "Auvergne-Rhône-Alpes",
    "17": "Nouvelle-Aquitaine", "33": "Nouvelle-Aquitaine",
    "94": "Île-de-France", "91": "Île-de-France", "78": "Île-de-France",
    "56": "Bretagne", "35": "Bretagne", "39": "Bourgogne-Franche-Comté",
    "88": "Grand Est", "62": "Hauts-de-France", "974": "La Réunion",
}


def date(v):
    if isinstance(v, (datetime.datetime, datetime.date)):
        return v.strftime("%Y-%m-%d") if 2015 < v.year < 2100 else None
    if v is None:
        return None
    s = str(v)
    m = re.search(r"(\d{1,2})[/.](\d{1,2})[/.](\d{2,4})", s)
    if m:
        d, mo, y = m.groups()
        y = int(y)
        if y < 100:
            y += 2000
        if not (2020 <= y <= 2030):
            return None
        try:
            return datetime.date(y, int(mo), int(d)).isoformat()
        except ValueError:
            return None
    m = re.search(r"(\d{4})-(\d{2})-(\d{2})", s)
    return m.group(0) if m else None


def nombre(v):
    if v is None:
        return 0.0
    if isinstance(v, (int, float)):
        return float(v)
    m = re.findall(r"(\d+[.,]?\d*)", str(v).replace(" ", ""))
    return float(m[0].replace(",", ".")) if m else 0.0


def normaliser(v):
    return re.sub(r"\s+", " ", str(v)).strip() if v is not None else ""


def intervenant(cle):
    if not cle:
        return None
    cle = normaliser(cle).strip(" ?:.")
    return INTERVENANTS.get(cle, INTERVENANTS.get(cle.rstrip("?").strip()))


def acteurs(valeur):
    """Extrait l'AAP, l'accompagnateur et le gestionnaire de la colonne « traité par »."""
    s = normaliser(valeur).replace("\n", " / ")
    aap = acc = None

    m = re.search(r"(?:Archi|ARCHI)\w*\s*[:.]?\s*([^/]+)", s)
    if m:
        aap = intervenant(m.group(1))

    m = re.search(r"Acc(?:ompagnat\w+)?\.?\s*(?:VAE)?\s*[:.]\s*([^/]+)", s)
    if m:
        acc = intervenant(m.group(1))

    gestionnaire = intervenant(re.split(r"[/\n]", s)[0])
    if aap is None and gestionnaire in AAP:
        aap = gestionnaire

    return aap, acc, gestionnaire


def certification(libelle):
    s = normaliser(libelle)
    for cle, (rncp, abrege) in CERTIFICATIONS.items():
        if s.lower().startswith(cle.lower()):
            return rncp, abrege
    return None, None


def geo(v):
    s = normaliser(v).replace("\xa0", " ")
    m = re.search(r"\((\d{2,3})[\)\]]", s)
    dep = m.group(1) if m else None
    ville = re.split(r"[\n(]", str(v or ""))[0].strip() or None
    return ville, dep, REGIONS.get(dep or "")


def seances(ligne, colonnes):
    """Reconstitue les seances depuis les colonnes de suivi (« 09/01/2026 : 2h »)."""
    sortie = []
    for col in ("suivi 1", "suivi 2", "suivi 3"):
        brut = normaliser(ligne.get(col))
        if not brut:
            continue
        for bloc in re.split(r"[/\n]", brut):
            d = date(bloc)
            if not d:
                continue
            m = re.search(r"(\d+[,.]?\d*)\s*h", bloc, re.I)
            sortie.append({
                "date": d,
                "nature": "Individuel",
                "dureeHeures": float(m.group(1).replace(",", ".")) if m else None,
                "objet": bloc.strip()[:120] or None,
            })
    _ = colonnes
    return sortie


def resultat_jury(historique):
    h = historique or ""
    if re.search(r"r[ée]us+ite", h, re.I):
        return "ValidationTotale"
    if re.search(r"partiel", h, re.I):
        return "ValidationPartielle"
    return "NonRenseigne"


def main() -> int:
    suivi = openpyxl.load_workbook(
        os.path.join(SOURCES, "Tableau de suivi des demandes.xlsx"), data_only=True)

    # ---------------- intervenants ----------------
    intervenants = []
    vus = set()

    ws = suivi["Accompagnateurs VAE"]
    lignes = list(ws.iter_rows(values_only=True))
    entete = next(i for i, r in enumerate(lignes) if normaliser(r[0]) == "Intervenants")

    for r in lignes[entete + 1:]:
        if not r or not r[0]:
            continue
        nom = normaliser(r[0])
        retire = bool(re.search(r"ne souhaite plus", " ".join(normaliser(x) for x in r[20:] if x), re.I))
        intervenants.append({
            "cle": nom,
            "nom": nom,
            "type": "ArchitecteAccompagnateurParcours" if nom in AAP else "Accompagnateur",
            "statut": "RetireDuReseau" if retire else "Actif",
            "telephone": normaliser(r[1]) or None,
            "email": normaliser(r[2]) or None,
            "territoire": normaliser(r[3]) or None,
            "region": normaliser(r[3]) or None,
            "distanciel": normaliser(r[4]).upper() == "OK",
            "specialites": normaliser(r[5]) or None,
            "tarifHoraire": nombre(r[23]) if len(r) > 23 and r[23] else 30.0,
        })
        vus.add(nom)

    # Les AAP et gestionnaires n'apparaissent pas dans l'onglet accompagnateurs.
    for nom in sorted(set(INTERVENANTS.values())):
        if nom in vus:
            continue
        intervenants.append({
            "cle": nom,
            "nom": nom,
            "type": "ArchitecteAccompagnateurParcours" if nom in AAP else "Interne",
            "statut": "Actif",
            "distanciel": True,
            "tarifHoraire": 30.0 if nom not in AAP else None,
        })
        vus.add(nom)

    # ---------------- factures ----------------
    facturation = openpyxl.load_workbook(
        os.path.join(SOURCES, "Facturation VAE.xlsx"), data_only=True)["Feuil1"]

    factures_par_nom = {}
    for r in facturation.iter_rows(min_row=2, values_only=True):
        if not r[1] or not r[4]:
            continue
        cle = normaliser(r[4]).upper()
        factures_par_nom.setdefault(cle, []).append({
            "numero": normaliser(r[1]),
            "dateEmission": date(r[2]),
            "financeur": normaliser(r[3]) or None,
            "montantHt": nombre(r[5]),
            "codeAcf": normaliser(r[0]) or None,
        })

    # ---------------- dossiers ----------------
    parcours = []
    deja = set()

    for onglet in ["En cours", "Abandon", "Candidature supprimée", "Disparue France VAE"]:
        ws = suivi[onglet]
        lignes = list(ws.iter_rows(values_only=True))
        entetes = [i for i, r in enumerate(lignes)
                   if r and any(normaliser(x) == "Premier contact" for x in r if x)]

        for k, debut in enumerate(entetes):
            fin = entetes[k + 1] if k + 1 < len(entetes) else len(lignes)
            H = [normaliser(x) if x else "" for x in lignes[debut]]
            index = {h: i for i, h in enumerate(H) if h}
            col_nom = index.get("NOM (en majuscule)", index.get("Nom"))
            if col_nom is None:
                continue

            for r in lignes[debut + 1:fin]:
                if not r or col_nom >= len(r) or not r[col_nom]:
                    continue

                nom = normaliser(r[col_nom])
                if nom in ("Nom", "NOM (en majuscule)", "A FACTURER"):
                    continue

                champ = lambda c: (r[index[c]] if c in index and index[c] < len(r) else None)
                ligne = {c: champ(c) for c in index}

                prenom = normaliser(champ("Prénom"))
                if (nom.upper(), prenom.upper()) in deja:
                    continue
                deja.add((nom.upper(), prenom.upper()))

                statut = normaliser(r[0])
                etape, secondaire, motif = ETAPES.get(statut, ("Qualification", None, None))
                if onglet != "En cours" and etape != "Sortie":
                    etape, secondaire, motif = ETAPES.get(
                        {"Abandon": "Abandon candidat",
                         "Candidature supprimée": "Candidature supprimée",
                         "Disparue France VAE": "Disparu(e) de France VAE"}[onglet],
                        ("Sortie", None, "AbandonCandidat"))

                # L'historique est parfois dans une colonne non titree, juste
                # apres le statut : on la recupere par position.
                historique = normaliser(champ("ETAT DES LIEUX - HISTORIQUE"))
                if not historique and len(r) > 1 and r[1] and normaliser(r[1]) != nom:
                    historique = normaliser(r[1])

                rncp, abrege = certification(champ("Diplôme visé") or champ("diplôme visé"))
                aap, acc, gestionnaire = acteurs(champ("traité par"))
                ville, dep, region = geo(champ("GA") or champ("localisation"))

                pec = normaliser(champ("N° prise en charge / financement")
                                 or champ("Prise en charge") or champ("N° prise en charge"))

                financements = []
                if pec:
                    if "CPF" in pec or "reva_" in pec or "Dossier N" in pec:
                        dispositif = "Cpf"
                    elif "Convention" in pec or "employeur" in pec.lower():
                        dispositif = "Employeur"
                    else:
                        dispositif = "Autre"
                    financements.append({
                        "dispositif": dispositif,
                        "numeroPriseEnCharge": pec[:80],
                        "dateSecurisation": date(champ("Parcours validé")) or date(champ("Prise en charge")),
                    })

                mvt = [d for d in (
                    date(champ("date 1ère demande")), date(champ("Premier contact")),
                    date(champ("Recueil besoins (RDV pédagogique)")), date(champ("RDV Faisabilité")),
                    date(champ("Dépôt Faisabilité après accord candidat")) or date(champ("Dépôt Faisabilité")),
                    date(champ("Parcours validé")), date(champ("date dépôt dossier")),
                    date(champ("date passage jury")), date(champ("Date jury")),
                    date(champ("entretien post jury - fin architecture")),
                ) if d]

                for bloc in re.split(r"[/\n]", historique):
                    d = date(bloc)
                    if d:
                        mvt.append(d)

                aujourdhui = datetime.date.today().isoformat()
                mvt = [d for d in mvt if d <= aujourdhui]

                parcours.append({
                    "nom": nom,
                    "prenom": prenom,
                    "email": normaliser(champ("mail")) or None,
                    "telephone": normaliser(champ("telephone")) or None,
                    "ville": ville,
                    "departement": dep,
                    "region": region,
                    "codeRncp": rncp,
                    "abrege": abrege,
                    "etape": etape,
                    "statutSecondaire": secondaire,
                    "motifSortie": motif,
                    "origine": "FranceVae",
                    "resultatJury": resultat_jury(historique),
                    "aap": aap,
                    "accompagnateur": acc,
                    "gestionnaire": gestionnaire,
                    "dateDemande": date(champ("date 1ère demande")),
                    "datePremierContact": date(champ("Premier contact")),
                    "dateRecueilBesoins": date(champ("Recueil besoins (RDV pédagogique)")),
                    "dateRdvFaisabilite": date(champ("RDV Faisabilité")),
                    "dateDepotFaisabilite": (date(champ("Dépôt Faisabilité après accord candidat"))
                                             or date(champ("Dépôt Faisabilité"))),
                    "dateParcoursValide": date(champ("Parcours validé")),
                    "dateRecevabilite": date(champ("Parcours validé")),
                    "dateDepotDossier": date(champ("date dépôt dossier")),
                    "dateJury": date(champ("date passage jury")) or date(champ("Date jury")),
                    "datePostJury": date(champ("entretien post jury - fin architecture")),
                    "dateDernierMouvement": max(mvt) if mvt else None,
                    "heuresIndividuel": nombre(champ("nbre hrs individuel")),
                    "heuresCollectif": nombre(champ("nbre hrs collectif")),
                    "heuresComplement": nombre(champ("form compl 1")) + nombre(champ("form compl 2")),
                    "forfaitArchitecture": nombre(champ("AAP")) > 0,
                    "fraisJury": "certif" in normaliser(champ("AAP")).lower(),
                    "codeAcf": normaliser(champ("Code ACF SOLEI")) or None,
                    "historique": historique[:1000] or None,
                    "financements": financements,
                    "factures": factures_par_nom.get(nom.upper(), []),
                    "seances": seances(ligne, index),
                })

    os.makedirs(os.path.dirname(SORTIE), exist_ok=True)
    with io.open(SORTIE, "w", encoding="utf-8") as f:
        json.dump({
            "source": "Tableau de suivi des demandes.xlsx + Facturation VAE.xlsx",
            "extraitLe": datetime.date.today().isoformat(),
            "intervenants": intervenants,
            "parcours": parcours,
        }, f, ensure_ascii=False, indent=1)

    rattachees = sum(len(p["factures"]) for p in parcours)
    total_factures = sum(len(v) for v in factures_par_nom.values())

    print(f"{len(intervenants)} intervenants", file=sys.stderr)
    print(f"{len(parcours)} dossiers", file=sys.stderr)
    print(f"{sum(1 for p in parcours if p['codeRncp'])} dossiers avec certification rapprochee",
          file=sys.stderr)
    print(f"{rattachees}/{total_factures} factures rattachees", file=sys.stderr)
    print(f"{sum(len(p['seances']) for p in parcours)} seances reconstituees", file=sys.stderr)
    print(f"-> {SORTIE}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
