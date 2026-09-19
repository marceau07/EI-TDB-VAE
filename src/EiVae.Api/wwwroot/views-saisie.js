/* =========================================================================
   SAISIE — fiche candidat, formulaires candidat et intervenant,
   réseau VAE, référentiel certifications, VAE collective, règles de gestion.
   ========================================================================= */

const REGIONS = ['Auvergne-Rhône-Alpes', 'Bourgogne-Franche-Comté', 'Bretagne', 'Centre-Val de Loire',
  'Corse', 'Grand Est', 'Hauts-de-France', 'Île-de-France', 'Normandie', 'Nouvelle-Aquitaine',
  'Occitanie', 'Pays de la Loire', 'PACA', 'Guadeloupe', 'Guyane', 'Martinique', 'La Réunion', 'Mayotte'];

const optionsIntervenants = (type) => ETAT.intervenants
  .filter(i => (!type || estValeur(i.type, type)) && i.mobilisable)
  .map(i => ({ code: i.id, libelle: i.nomComplet }));

const optionsCertifications = () => ETAT.certifications
  .map(c => ({ code: c.id, libelle: (c.abrege ? c.abrege + ' — ' : '') + c.intitule }));

/* ============================== FICHE CANDIDAT ============================== */
let FICHE_OUVERTE = null;
let FICHE_DONNEES = null;

/** Recharge la fiche si elle est affichée, puis la vue et les pastilles du menu. */
async function apresModification(id) {
  if ($('#drawer').classList.contains('on') && FICHE_OUVERTE === id) await ouvrirFiche(id);
  rafraichir();
  majCompteurs();
}

async function ouvrirFiche(id) {
  FICHE_OUVERTE = id;
  if (!$('#drawer').classList.contains('on')) ouvrirTiroir(chargement('Ouverture de la fiche…'));

  let d;
  try { d = await API.get('/parcours/' + id); }
  catch (e) { ouvrirTiroir(`<div class="dr-sec"><div class="banner">${esc(e.message)}</div></div>`); return; }
  FICHE_DONNEES = d;

  const p = d.parcours, c = d.certification, e = d.economie;
  const j = p.jalons, h = p.heures;

  const etapes = [
    ['Demande reçue', j.dateDemande], ['Premier contact', j.datePremierContact],
    ['Recueil des besoins', j.dateRecueilBesoins], ['RDV faisabilité', j.dateRdvFaisabilite],
    ['Dépôt faisabilité', j.dateDepotFaisabilite], ['Recevabilité', j.dateRecevabilite],
    ['Parcours validé', j.dateParcoursValide], ['Début accompagnement', j.dateDebutAccompagnement],
    ['Dépôt dossier de validation', j.dateDepotDossierValidation], ['Passage en jury', j.dateJury],
    ['Entretien post-jury', j.dateEntretienPostJury],
  ];
  const dernierFait = etapes.reduce((acc, s, i) => s[1] ? i : acc, -1);

  const kv = lignes => `<dl class="kv">${lignes.filter(Boolean).map(([k, v, brut]) =>
    `<dt>${esc(k)}</dt><dd>${brut ? v : esc(v == null || v === '' ? '—' : v)}</dd>`).join('')}</dl>`;

  const tauxHeures = h.total ? h.realisees / h.total : 0;
  const rgpd = p.candidat.consentementRgpd;

  ouvrirTiroir(`
  <div class="dr-h">
    <span class="av" style="background:${avat(p.candidat.prenom + p.candidat.nom)}">${esc(inits(p.candidat.prenom + ' ' + p.candidat.nom))}</span>
    <div style="flex:1;min-width:0">
      <h2>${esc(p.candidat.prenom)} ${esc(p.candidat.nom)}</h2>
      <div class="s">${c ? esc((c.abrege ? c.abrege + ' — ' : '') + c.intitule) : 'Certification non renseignée'}${c?.codeRncp ? ' · ' + esc(c.codeRncp) : ''}</div>
      <div style="margin-top:7px;display:flex;gap:6px;flex-wrap:wrap">
        ${p.motifSortie ? `<span class="chip c-neutral">${esc(p.motifSortie)}</span>`
          : `<span class="chip c-brand">E${p.etapeRang} · ${esc(p.etape)}</span>`}
        ${p.statutSecondaire ? `<span class="chip c-warn">${esc(p.statutSecondaire)}</span>` : ''}
        <span class="chip c-neutral">grille ${esc(e.grille)}</span>
        ${d.alertes.length ? `<span class="chip c-${d.alertes.some(a => a.severite === 'critique') ? 'crit' : 'warn'}">${d.alertes.length} alerte${d.alertes.length > 1 ? 's' : ''}</span>`
          : '<span class="chip c-ok">conforme</span>'}
      </div>
    </div>
    <button class="btn ghost" onclick="fermerTiroir()" aria-label="Fermer">✕</button>
  </div>
  <div class="dr-b">
    ${d.alertes.length ? `<div class="dr-sec" style="background:var(--crit-bg);border-bottom-color:var(--crit-line)">
      <span class="eyebrow" style="color:var(--crit)">Actions attendues</span>
      ${d.alertes.map(a => `<div class="alerte-fiche">
        <span class="chip c-${a.severite === 'critique' ? 'crit' : 'warn'}" style="flex:none">${esc(a.codeRegle)}</span>
        <div class="corps"><b style="font-weight:600">${esc(a.actionAttendue)}</b>
          <div style="color:var(--ink-2);font-size:11.5px">${esc(a.libelle)} — ${esc(a.detail)}${a.responsable ? ` · <span style="color:var(--muted)">${esc(a.responsable)}</span>` : ''}</div>
          ${a.reporteeJusquA ? `<div style="color:var(--muted);font-size:11px">reportée au ${fmtDate(a.reporteeJusquA)}${a.motifReport ? ' — ' + esc(a.motifReport) : ''}</div>` : ''}
        </div>
        <div class="boutons">
          <button class="btn primary mini" onclick="resoudreAlerte(${a.id})">Résoudre</button>
          <button class="btn ghost mini" onclick="reporterAlerte(${a.id})">Reporter</button>
        </div>
      </div>`).join('')}
    </div>` : ''}

    <div class="dr-sec" style="display:flex;gap:8px;flex-wrap:wrap">
      <button class="btn primary" onclick="formulaireCandidat(${p.id})">Modifier le dossier</button>
      <button class="btn" onclick="formulaireJalons(${p.id})">Jalons &amp; jury</button>
      <button class="btn" onclick="formulaireSeance(${p.id})">Ajouter une séance</button>
      <button class="btn" onclick="formulaireFinancement(${p.id})">Financement</button>
      <button class="btn" onclick="formulaireFacture(${p.id})">Facture</button>
    </div>

    <div class="dr-sec"><span class="eyebrow">Identification &amp; demande</span>
      ${kv([
        ['Territoire', [p.candidat.ville, p.candidat.codePostal, p.candidat.region].filter(Boolean).join(' · ')],
        ['Courriel', p.candidat.email], ['Téléphone', p.candidat.telephone],
        ['Date de première demande', fmtDateLongue(j.dateDemande)],
        ['Origine', p.origine],
        ['Ancienneté du dossier', j.dateDemande ? Math.round(jours(j.dateDemande) / 30) + ' mois' : '—'],
        ['Consentement RGPD', `<label class="check" style="font-size:12.5px">
            <input type="checkbox" id="caseRgpd"${rgpd ? ' checked' : ''} onchange="basculerConsentement(${p.id}, this)">
            <span>${rgpd ? 'recueilli' + (p.candidat.dateConsentementRgpd ? ' le ' + fmtDateLongue(p.candidat.dateConsentementRgpd) : '') : 'non recueilli'}</span>
          </label>`, true],
      ])}
    </div>

    <div class="dr-sec"><span class="eyebrow">Acteurs</span>
      ${kv([
        ['AAP référent', p.aap ? esc(p.aap.nom) : '<span class="chip c-warn">non affecté</span>', true],
        ['Accompagnateur', p.accompagnateur ? esc(p.accompagnateur.nom) : '<span class="chip c-warn">non affecté</span>', true],
        ['Gestionnaire', p.gestionnaire?.nom],
        ['Contact certificateur', c?.contact?.nom || c?.certificateurs?.[0]?.nom],
      ])}
      ${!p.accompagnateur && c ? `<button class="btn" style="margin-top:10px" onclick="proposerAccompagnateur(${c.id},${p.id})">Proposer un accompagnateur</button>` : ''}
    </div>

    <div class="dr-sec"><span class="eyebrow">Parcours &amp; jalons</span>
      <div class="tl">${etapes.map((s, i) => `
        <div class="tl-i ${s[1] ? 'done' : (i === dernierFait + 1 ? 'now' : '')}">
          <span class="tl-d"></span><span class="tl-t">${esc(s[0])}</span><span class="tl-x">${fmtDate(s[1])}</span>
        </div>`).join('')}</div>
      <p style="font-size:11px;color:var(--faint);margin-top:8px">
        Date de démarrage retenue : <b>${fmtDateLongue(j.dateDebutParcours)}</b></p>
    </div>

    <div class="dr-sec"><span class="eyebrow">Heures &amp; enveloppe</span>
      ${kv([
        ['Individuel prescrit', `${nf(h.individuelPrescrit)} h (plafond ${nf(h.plafondIndividuel)} h)`],
        ['Collectif prescrit', `${nf(h.collectifPrescrit)} h (plafond ${nf(h.plafondCollectif)} h)`],
        ['Compléments formatifs', `${nf(h.complementPrescrit)} h (plafond ${nf(h.plafondComplement)} h)`],
        ['Heures réalisées', `${nf(h.realisees)} h sur ${d.seances.length} séance${d.seances.length > 1 ? 's' : ''}`],
        ['Reste à consommer', h.total ? nf(Math.max(0, h.total - h.realisees)) + ' h' : '—'],
      ])}
      ${h.total ? `<div class="gauge" style="margin-top:9px"><i class="${tauxHeures > 1 ? 'c' : tauxHeures > .85 ? 'w' : 'o'}" style="width:${Math.min(100, tauxHeures * 100)}%"></i></div>
      <div style="font-size:10.5px;color:var(--faint);margin-top:4px;font-family:var(--f-data)">taux de consommation ${pct(tauxHeures)}</div>` : ''}
      ${d.seances.length ? `<table class="t" style="margin-top:11px"><thead><tr><th>Date</th><th>Nature</th><th class="n">Durée</th><th>Intervenant</th></tr></thead>
        <tbody>${d.seances.map(s => `<tr><td class="num">${fmtDate(s.date)}</td><td>${esc(s.nature)}</td>
        <td class="n">${nf(s.dureeHeures)} h</td><td style="font-size:11.5px">${esc(s.intervenant || '—')}</td></tr>`).join('')}</tbody></table>` : ''}
    </div>

    <div class="dr-sec"><span class="eyebrow">Compléments formatifs &amp; e-learning</span>
      ${d.modules.length ? `<table class="t" style="margin-top:8px"><thead><tr><th>Module</th><th class="n">Durée</th><th>Statut</th></tr></thead>
        <tbody>${d.modules.map(m => `<tr>
          <td><span class="chip c-${estValeur(m.nature, 'ComplementFormatif') ? 'brand' : 'info'}" style="margin-right:5px">${estValeur(m.nature, 'ComplementFormatif') ? 'complément' : 'e-learning'}</span>
            ${m.url ? lienExterne(m.url, m.titre) : esc(m.titre)}</td>
          <td class="n">${m.dureeHeures ? nf(m.dureeHeures) + ' h' : '—'}</td>
          <td>${badgeStatutModule(m.statut)}</td></tr>`).join('')}</tbody></table>`
        : '<div style="font-size:12px;color:var(--faint);margin-top:6px">Aucun complément formatif ni module e-learning prescrit.</div>'}
      <div style="display:flex;gap:8px;flex-wrap:wrap;margin-top:10px">
        <button class="btn" onclick="formulaireCandidat(${p.id})">Choisir les modules</button>
        <button class="btn ghost" onclick="fermerTiroir();aller('elearning')">Suivi e-learning</button>
      </div>
    </div>

    <div class="dr-sec"><span class="eyebrow">Financement &amp; facturation</span>
      ${kv([
        ['Grille appliquée', `<b>${esc(e.grille)}</b> — ${esc(e.grilleLibelle)}`, true],
        ['Forfait architecture', e.forfait ? eur(e.forfait) : '—'],
        ['Accompagnement individuel', e.montantIndividuel ? `${nf(h.individuelPrescrit)} h × ${e.tarifs.tarifHoraireIndividuel} € = ${eur(e.montantIndividuel)}` : '—'],
        ['Accompagnement collectif', e.montantCollectif ? `${nf(h.collectifPrescrit)} h × ${e.tarifs.tarifHoraireCollectif} € = ${eur(e.montantCollectif)}` : '—'],
        ['Compléments formatifs', e.montantComplementFormatif ? `${nf(h.complementPrescrit)} h × ${e.tarifs.tarifHoraireComplementFormatif} € = ${eur(e.montantComplementFormatif)}` : '—'],
        ['Frais de jury', e.fraisJury ? eur(e.fraisJury) : '—'],
        ['Montant prescrit', `<b style="font-size:14px">${eur(e.total)}</b>`, true],
        ['Coût pédagogique', eur(e.coutPedagogique)],
        ['Marge estimée', `<b style="color:var(--ok)">${eur(e.marge)}</b> (${pct(e.tauxMarge)})`, true],
      ])}
      ${e.depassements?.length ? `<div class="banner warn" style="margin-top:11px">${e.depassements.map(esc).join('<br>')}</div>` : ''}
      <div style="display:flex;align-items:center;margin-top:13px">
        <span class="eyebrow">Financements</span>
        <button class="btn ghost mini" style="margin-left:auto" onclick="formulaireFinancement(${p.id})">Ajouter</button>
      </div>
      ${d.financements.length ? d.financements.map(f => `
        <div style="padding:8px 0;border-top:1px solid var(--line);font-size:12.5px">
          <div style="display:flex;gap:8px;align-items:center;flex-wrap:wrap">
            <span class="chip c-${f.estSecurise ? 'ok' : 'crit'}">${esc(f.dispositifLibelle)}</span>
            <b style="font-weight:600">${esc(f.financeur || 'financeur non précisé')}</b>
            <span style="margin-left:auto" class="actions-ligne">
              <button class="btn ghost mini" onclick="formulaireFinancement(${p.id}, ${f.id})">Modifier</button>
              <button class="btn ghost mini danger" onclick="supprimerFinancement(${p.id}, ${f.id})">Supprimer</button>
            </span>
          </div>
          <div style="display:grid;grid-template-columns:repeat(auto-fit,minmax(130px,1fr));gap:3px 12px;margin-top:6px;font-size:11.5px;color:var(--ink-2)">
            <span>N° : <b>${esc(f.numeroPriseEnCharge || '—')}</b></span>
            <span>Accordé : <b>${f.montantAccorde != null ? eur(f.montantAccorde) : '—'}</b></span>
            <span>Reste à charge : <b>${f.resteACharge != null ? eur(f.resteACharge) : '—'}</b></span>
            <span>Demandé le : <b>${fmtDate(f.dateDemande)}</b></span>
            <span>Sécurisé le : <b>${f.estSecurise ? fmtDate(f.dateSecurisation) : 'non sécurisé'}</b></span>
          </div>
          ${f.commentaire ? `<div style="font-size:11.5px;color:var(--muted);margin-top:4px">${esc(f.commentaire)}</div>` : ''}
        </div>`).join('') : '<div class="banner" style="margin-top:8px">Aucun financement enregistré.</div>'}
      ${d.factures.length ? `<table class="t" style="margin-top:11px"><thead><tr><th>Facture</th><th>Date</th><th>Financeur</th><th class="n">Montant HT</th><th>Règlement</th></tr></thead>
        <tbody>${d.factures.map(f => `<tr><td class="num">${esc(f.numero)}</td><td class="num">${fmtDate(f.dateEmission)}</td>
        <td style="font-size:11.5px">${esc(f.financeur || '—')}</td><td class="n">${eur(f.montantHt)}</td>
        <td>${f.dateReglement ? `<span class="chip c-ok">${fmtDate(f.dateReglement)}</span>` : '<span class="chip c-warn">en attente</span>'}</td></tr>`).join('')}</tbody></table>` : ''}
    </div>

    <div class="dr-sec"><span class="eyebrow">Dossier documentaire</span>
      ${kv([
        ['Dossier', `<span class="num" style="font-size:11.5px">${esc(p.sharePoint.chemin)}</span>`, true],
        ['SharePoint', p.sharePoint.configure
          ? lienExterne(p.sharePoint.url, 'ouvrir le dossier')
          : '<span class="chip c-neutral">non configuré</span>', true],
      ])}
      <div style="display:flex;gap:8px;flex-wrap:wrap;margin-top:10px">
        <a class="btn" href="/api/parcours/${p.id}/fiche.docx">Fiche Word</a>
        ${p.sharePoint.dossierLocalConfigure
          ? `<button class="btn" onclick="genererDossier(${p.id})">Créer ou mettre à jour le dossier</button>` : ''}
      </div>
    </div>

    <div class="dr-sec"><span class="eyebrow">Systèmes tiers</span>
      ${kv([
        ['Statut France VAE', p.candidatureFranceVaeId ? 'identifiant ' + p.candidatureFranceVaeId : 'non rattaché'],
        ['Dossier Solei', p.codeAcfSolei || 'non créé'],
        ['Espace EI Académie', p.espaceAcademieCree
          ? lienExterne(p.urlEspaceAcademie, 'ouvrir l\'espace') : '<span class="chip c-neutral">non créé</span>', true],
        ['Dernière activité LMS', fmtDate(p.dateDerniereActiviteAcademie)],
      ])}
    </div>

    ${c ? `<div class="dr-sec"><span class="eyebrow">Certification visée</span>
      ${kv([
        ['Code RNCP', `${esc(c.codeRncp)} · ${lienExterne(c.lienFranceCompetences, 'fiche France Compétences')}`, true],
        ['Niveau', c.niveau], ['Domaine', c.domaineEi],
        ['Voie VAE', c.voieVaeOuverte ? '<span class="chip c-ok">ouverte</span>' : '<span class="chip c-crit">fermée</span>', true],
        ['Certificateur', c.certificateurs?.[0]?.nom],
      ])}
      ${c.blocs?.length ? `<div style="margin-top:10px"><span class="eyebrow">Blocs de compétences</span>
        <div style="display:flex;flex-direction:column;gap:5px;margin-top:7px">${c.blocs.map(b =>
          `<div style="font-size:12px;color:var(--ink-2)"><span class="num" style="font-size:10px;color:var(--teal)">${esc(b.code)}</span> ${esc(b.libelle)}</div>`).join('')}</div></div>` : ''}
      <button class="btn" style="margin-top:11px" onclick="fermerTiroir();ficheCertification(${c.id})">Ouvrir la fiche certification</button>
    </div>` : ''}

    <div class="dr-sec"><span class="eyebrow">Jury &amp; qualité</span>
      ${kv([
        ['Date de jury', fmtDateLongue(j.dateJury)],
        ['Résultat', badgeResultat(p.resultatJury), true],
        ['Motif de sortie', p.motifSortie || '—'],
      ])}
    </div>

    ${p.historique ? `<div class="dr-sec"><span class="eyebrow">Historique repris du tableur</span>
      <p style="font-size:12px;color:var(--ink-2);white-space:pre-line;line-height:1.55">${esc(p.historique)}</p></div>` : ''}
  </div>`);
}

async function basculerConsentement(id, caseACocher) {
  caseACocher.disabled = true;
  try {
    await API.put(`/parcours/${id}/consentement`, { consentementRgpd: caseACocher.checked });
    toast(caseACocher.checked ? 'Consentement RGPD enregistré.' : 'Consentement RGPD retiré.', 'ok');
    apresModification(id);
  } catch (e) {
    caseACocher.checked = !caseACocher.checked;
    caseACocher.disabled = false;
    toast(e.message, 'err', 'Enregistrement impossible');
  }
}

async function genererDossier(id) {
  try {
    const r = await API.post(`/parcours/${id}/dossier`);
    if (r.ecrit) toast(`${r.nomFichier} écrit dans ${r.cheminLocal}.`, 'ok', 'Dossier à jour');
    else toast(r.avertissement || 'Dossier non écrit.', 'warn');
  } catch (e) { toast(e.message, 'err', 'Génération impossible'); }
}

/** Message rendu après une création : le dossier documentaire a-t-il pu être écrit ? */
function toastDossier(dossier) {
  if (!dossier) return;
  if (dossier.ecrit) toast(`Dossier « ${dossier.chemin.split('/').pop()} » créé avec la fiche Word.`, 'ok');
  else if (dossier.avertissement) toast(dossier.avertissement, 'warn', 'Dossier non créé');
}

function badgeStatutModule(s) {
  const m = {
    prescrit: '<span class="chip c-neutral">prescrit</span>',
    encours: '<span class="chip c-info">en cours</span>',
    termine: '<span class="chip c-ok">terminé</span>',
    abandonne: '<span class="chip c-crit">abandonné</span>',
  };
  return m[String(s).toLowerCase()] || esc(s);
}

function badgeResultat(r) {
  const m = {
    ValidationTotale: '<span class="chip c-ok">Validation totale</span>',
    ValidationPartielle: '<span class="chip c-warn">Validation partielle</span>',
    Refus: '<span class="chip c-crit">Refus</span>',
    Absence: '<span class="chip c-neutral">Absence</span>',
  };
  return m[r] || '<span class="chip c-neutral">non renseigné</span>';
}

async function proposerAccompagnateur(certificationId, parcoursId) {
  ouvrirModale('Proposer un accompagnateur', null, chargement());
  try {
    const props = await API.get('/intervenants/proposer?certificationId=' + certificationId);
    $('.modal-b').innerHTML = props.length ? `<div class="tw"><table class="t">
      <thead><tr><th class="n">Score</th><th>Intervenant</th><th>Motifs</th><th></th></tr></thead>
      <tbody>${props.map(x => `<tr>
        <td class="n"><b style="color:${x.score >= 60 ? 'var(--ok)' : x.score >= 35 ? 'var(--warn)' : 'var(--faint)'}">${x.score}</b></td>
        <td>${who(x.nom, x.region, true)}</td>
        <td style="font-size:11.5px;color:var(--ink-2)">${esc(x.motifs.join(' · '))}</td>
        <td><button class="btn primary mini" onclick="affecter(${parcoursId},${x.intervenantId})">Affecter</button></td></tr>`).join('')}
      </tbody></table></div>` : vide('Aucun intervenant mobilisable.');
  } catch (e) { $('.modal-b').innerHTML = `<div class="banner">${esc(e.message)}</div>`; }
}

async function affecter(parcoursId, intervenantId, acteur = 'Accompagnateur') {
  try {
    const r = await API.put(`/parcours/${parcoursId}/affectation`, { acteur, intervenantId });
    fermerModale();
    toast(r.notifications ? 'Intervenant affecté et notifié.' : 'Intervenant affecté.', 'ok');
    apresModification(parcoursId);
  } catch (e) { toast(e.message, 'err', 'Affectation impossible'); }
}

/* ============================== RÉSOLUTION D'ALERTE ============================== */
async function resoudreDepuisPlan(parcoursId, alerteId) {
  FICHE_DONNEES = await API.get('/parcours/' + parcoursId);
  resoudreAlerte(alerteId);
}

/**
 * Ouvre le formulaire de résolution adapté à la règle : saisir le jalon manquant,
 * sécuriser le financement, émettre la facture, affecter l'intervenant… La même
 * boîte permet aussi de reporter l'alerte.
 */
function resoudreAlerte(alerteId) {
  const d = FICHE_DONNEES;
  const a = d.alertes.find(x => x.id === alerteId);
  if (!a) { toast('Cette alerte a déjà été levée.', 'ok'); return; }

  const p = d.parcours;
  const aujourdhui = new Date().toISOString().slice(0, 10);
  const cond = String(a.condition || '').toLowerCase();
  let corps = '', libelleAction = 'Enregistrer et lever l\'alerte';

  if (cond === 'delaidepuisjalon' && a.jalonAttendu) {
    corps = champ({ nom: 'date', label: a.jalonAttendu.libelle, type: 'date', valeur: aujourdhui, requis: true });
  } else if (cond === 'delaidepuisjalon' && a.jalonReference?.code === 'DateDernierMouvement') {
    libelleAction = 'Consigner le contact';
  } else if (cond === 'echeanceproche') {
    libelleAction = 'Préparation engagée';
  } else if (cond === 'financementnonsecurise') {
    const enAttente = d.financements.find(f => !f.estSecurise);
    corps = `
      ${champ({ nom: 'dispositif', label: 'Dispositif', type: 'select', valeur: enAttente ? '' : 'Cpf',
        vide: enAttente ? `Sécuriser le financement ${enAttente.dispositifLibelle} existant` : false,
        options: ETAT.nomenclatures.dispositifs.filter(x => x.code !== 'NonSecurise') })}
      ${champ({ nom: 'financeur', label: 'Financeur' })}
      ${champ({ nom: 'numeroPriseEnCharge', label: 'N° de prise en charge' })}
      ${champ({ nom: 'montantAccorde', label: 'Montant accordé (€)', type: 'number', pas: '0.01', min: 0 })}
      ${champ({ nom: 'date', label: 'Date de sécurisation', type: 'date', valeur: aujourdhui })}`;
  } else if (cond === 'facturemanquante') {
    corps = `
      ${champ({ nom: 'numeroFacture', label: 'Numéro de facture', requis: true })}
      ${champ({ nom: 'date', label: 'Date d\'émission', type: 'date', valeur: aujourdhui })}
      ${champ({ nom: 'montantHt', label: 'Montant HT (€)', type: 'number', pas: '0.01', min: 0, valeur: a.montantAFacturer, requis: true })}
      ${champ({ nom: 'financeur', label: 'Financeur' })}`;
  } else if (cond === 'acteurmanquant') {
    const type = { aap: 'ArchitecteAccompagnateurParcours', accompagnateur: 'Accompagnateur', gestionnaire: 'Interne' }[String(a.acteur).toLowerCase()];
    corps = champ({ nom: 'intervenantId', label: 'Intervenant', type: 'select', requis: true,
      options: optionsIntervenants(type), vide: 'Choisir…', span: true });
  } else if (cond === 'consommationheures') {
    corps = `
      ${champ({ nom: 'heuresIndividuel', label: 'Heures individuelles', type: 'number', pas: '0.5', min: 0, valeur: p.heures.individuelPrescrit })}
      ${champ({ nom: 'heuresCollectif', label: 'Heures collectives', type: 'number', pas: '0.5', min: 0, valeur: p.heures.collectifPrescrit })}
      ${champ({ nom: 'heuresComplementFormatif', label: 'Compléments formatifs', type: 'number', pas: '0.5', min: 0, valeur: p.heures.complementPrescrit })}`;
    libelleAction = 'Enregistrer l\'avenant';
  } else if (cond === 'statutsecondaire') {
    libelleAction = 'Lever le statut d\'attente';
  } else if (cond === 'consentementmanquant') {
    libelleAction = 'Consentement recueilli';
  } else {
    libelleAction = null;
  }

  const dans7j = new Date(Date.now() + 7 * 864e5).toISOString().slice(0, 10);

  ouvrirModale(`Résoudre ${a.codeRegle} — ${a.libelle}`, `${p.candidat.prenom} ${p.candidat.nom} · ${a.detail}`, `
    <form id="frmResolution" onsubmit="return false">
      <div class="banner info" style="margin-top:0"><div><b>${esc(a.actionAttendue)}</b>${a.responsable ? ' · ' + esc(a.responsable) : ''}</div></div>
      ${libelleAction ? `<div class="form-grid" style="margin-top:12px">
        ${corps}
        ${champ({ nom: 'commentaire', label: 'Commentaire', type: 'textarea', span: true })}
      </div>
      <div class="form-actions"><span class="grow"></span>
        <button class="btn primary" id="btnResoudre">${esc(libelleAction)}</button></div>`
      : '<p class="hint" style="margin-top:10px">Cette alerte se lève quand le dossier avance : modifiez le dossier, ou reportez-la.</p>'}
    </form>
    <form id="frmReportResolution" class="resolution" onsubmit="return false">
      <span class="eyebrow">Ou reporter l'alerte</span>
      <div class="form-grid" style="margin-top:8px">
        ${champ({ nom: 'reporterJusquA', label: 'Jusqu\'au', type: 'date', valeur: dans7j })}
        ${champ({ nom: 'commentaire', label: 'Motif', exemple: 'Relance programmée, congés du candidat…' })}
      </div>
      <div class="form-actions"><span class="grow"></span>
        <button class="btn" onclick="fermerModale()">Annuler</button>
        <button class="btn" id="btnReporterResolution">Reporter</button></div>
    </form>`);

  const envoyer = async (donnees, bouton) => {
    bouton.disabled = true;
    try {
      const r = await API.post(`/parcours/alertes/${a.id}/resoudre`, donnees);
      fermerModale();
      toast(r.message, r.resolue || r.reportee ? 'ok' : 'warn');
      apresModification(p.id);
    } catch (e) {
      toast(e.message, 'err', 'Résolution impossible');
      bouton.disabled = false;
    }
  };

  $('#btnResoudre')?.addEventListener('click', () => {
    const donnees = lireFormulaire($('#frmResolution'));
    if (cond === 'acteurmanquant' && !donnees.intervenantId) {
      marquerErreurs($('#frmResolution'), { intervenantId: 'Choisissez l\'intervenant.' }); return;
    }
    if (cond === 'facturemanquante' && (!donnees.numeroFacture || !donnees.montantHt)) {
      marquerErreurs($('#frmResolution'), {
        ...(donnees.numeroFacture ? {} : { numeroFacture: 'Obligatoire.' }),
        ...(donnees.montantHt ? {} : { montantHt: 'Obligatoire.' }),
      }); return;
    }
    envoyer(donnees, $('#btnResoudre'));
  });

  $('#btnReporterResolution').onclick = () => {
    const donnees = lireFormulaire($('#frmReportResolution'));
    if (!donnees.reporterJusquA) { toast('Indiquez une date de report.', 'err'); return; }
    envoyer(donnees, $('#btnReporterResolution'));
  };
}

/* ============================== FORMULAIRE CANDIDAT ============================== */
async function formulaireCandidat(id = null) {
  const modification = id != null;
  let p = null, cert = null, modulesDossier = [];

  if (modification) {
    const d = await API.get('/parcours/' + id);
    p = d.parcours; cert = d.certification; modulesDossier = d.modules;
  }

  const c = p?.candidat || {};
  const j = p?.jalons || {};
  const h = p?.heures || {};

  // Catalogue EI Académie : modules actifs, plus ceux déjà prescrits au dossier
  // même s'ils ont été désactivés depuis.
  const dejaPrescrits = new Set(modulesDossier.map(m => m.id));
  const catalogue = (await API.get('/elearning/modules')).filter(m => m.actif || dejaPrescrits.has(m.id));
  const optionsModules = nature => catalogue.filter(m => estValeur(m.nature, nature)).map(m => ({
    code: m.id, libelle: m.titre,
    sous: [m.type, m.dureeHeures ? nf(m.dureeHeures) + ' h' : null].filter(Boolean).join(' · '),
  }));
  const choisis = nature => [...dejaPrescrits].filter(mid => catalogue.some(m => m.id === mid && estValeur(m.nature, nature)));

  ouvrirModale(
    modification ? 'Modifier le dossier' : 'Nouveau candidat',
    modification ? `${c.prenom} ${c.nom}` : null,
    `<form id="frmCandidat" onsubmit="return false">
      <fieldset class="fieldset"><legend>Identification</legend>
        <div class="form-grid">
          ${champ({ nom: 'nom', label: 'Nom', requis: true, valeur: c.nom })}
          ${champ({ nom: 'prenom', label: 'Prénom', requis: true, valeur: c.prenom })}
          ${champ({ nom: 'email', label: 'Courriel', type: 'email', valeur: c.email })}
          ${champ({ nom: 'telephone', label: 'Téléphone', valeur: c.telephone })}
          ${champ({ nom: 'ville', label: 'Ville', valeur: c.ville })}
          ${champ({ nom: 'codePostal', label: 'Code postal', valeur: c.codePostal })}
          ${champ({ nom: 'departement', label: 'Département', valeur: c.departement, aide: 'Deux chiffres, ou trois outre-mer' })}
          ${champ({ nom: 'region', label: 'Région', type: 'select', valeur: c.region, options: REGIONS, vide: '—' })}
        </div>
      </fieldset>

      <fieldset class="fieldset"><legend>Demande</legend>
        <div class="form-grid">
          ${champ({ nom: 'certificationId', label: 'Certification visée', type: 'select',
            valeur: cert?.id, options: optionsCertifications(), vide: 'À déterminer', span: true })}
          ${champ({ nom: 'origine', label: 'Origine', type: 'select', vide: false,
            valeur: p?.origine || 'FranceVae', options: ETAT.nomenclatures.origines })}
          ${champ({ nom: 'dateDemande', label: 'Date de première demande', type: 'date',
            valeur: j.dateDemande || new Date().toISOString().slice(0, 10) })}
          ${champ({ nom: 'etape', label: 'Étape', type: 'select', vide: false,
            valeur: p?.etapeCode || 'DemandeRecue',
            options: ETAT.etapes.map(e => ({ code: e.code, libelle: `E${e.rang} · ${e.libelle}` })) })}
          ${champ({ nom: 'statutSecondaire', label: 'Statut secondaire', type: 'select', vide: false,
            valeur: p?.statutSecondaireCode || 'Aucun',
            options: [{ code: 'Aucun', libelle: 'Aucun' }, ...ETAT.nomenclatures.statutsSecondaires] })}
        </div>
      </fieldset>

      <fieldset class="fieldset"><legend>Acteurs</legend>
        <div class="form-grid">
          ${champ({ nom: 'aapId', label: 'AAP référent', type: 'select', valeur: p?.aap?.id,
            options: optionsIntervenants('ArchitecteAccompagnateurParcours'), vide: 'Non affecté' })}
          ${champ({ nom: 'accompagnateurId', label: 'Accompagnateur', type: 'select', valeur: p?.accompagnateur?.id,
            options: optionsIntervenants('Accompagnateur'), vide: 'Non affecté' })}
        </div>
      </fieldset>

      <fieldset class="fieldset"><legend>Parcours prescrit</legend>
        <div class="form-grid">
          ${champ({ nom: 'dateDebutParcours', label: 'Date de démarrage', type: 'date',
            valeur: j.dateDebutParcours, aide: 'Vide : calculée depuis les jalons.' })}
          ${champ({ nom: 'heuresIndividuel', label: 'Heures individuelles', type: 'number', pas: '0.5', min: 0,
            valeur: h.individuelPrescrit ?? 0 })}
          ${champ({ nom: 'heuresCollectif', label: 'Heures collectives', type: 'number', pas: '0.5', min: 0,
            valeur: h.collectifPrescrit ?? 0 })}
          ${champ({ nom: 'heuresComplementFormatif', label: 'Heures de compléments formatifs', type: 'number', pas: '0.5', min: 0,
            valeur: h.complementPrescrit ?? 0 })}
          ${champMulti({ nom: 'complementsFormatifs', label: 'Compléments formatifs', span: true,
            options: optionsModules('ComplementFormatif'), valeurs: choisis('ComplementFormatif'),
            vide: 'Aucun complément formatif', siVide: 'Aucun complément au catalogue : ajoutez-en dans Administration › E-learning.',
            aide: 'Les heures de compléments se recalculent depuis la sélection.' })}
          ${champMulti({ nom: 'modulesElearning', label: 'Modules e-learning', span: true,
            options: optionsModules('ELearning'), valeurs: choisis('ELearning'),
            vide: 'Aucun module e-learning', siVide: 'Aucun module au catalogue : ajoutez-en dans Administration › E-learning.' })}
          ${champ({ nom: 'forfaitArchitecture', label: 'Forfait architecture applicable', type: 'checkbox',
            valeur: p ? p.forfaitArchitectureApplique : true })}
          ${champ({ nom: 'fraisJuryInclus', label: 'Frais de jury inclus au devis', type: 'checkbox',
            valeur: p ? p.fraisJuryInclus : false })}
        </div>
        <div id="apercuTarif" class="banner info" style="margin-top:12px">Renseignez les heures pour voir le montant.</div>
      </fieldset>

      <fieldset class="fieldset"><legend>Systèmes tiers &amp; conformité</legend>
        <div class="form-grid">
          ${champ({ nom: 'codeAcfSolei', label: 'Code ACF Solei', valeur: p?.codeAcfSolei })}
          ${champ({ nom: 'candidatureFranceVaeId', label: 'Identifiant France VAE', valeur: p?.candidatureFranceVaeId })}
          ${champ({ nom: 'notes', label: 'Notes', type: 'textarea', valeur: c.notes, span: true })}
          ${champ({ nom: 'consentementRgpd', label: 'Consentement RGPD recueilli', type: 'checkbox',
            valeur: c.consentementRgpd })}
        </div>
      </fieldset>

      <div class="form-actions">
        <span class="grow"></span>
        <button class="btn" onclick="fermerModale()">Annuler</button>
        <button class="btn primary" id="btnEnregistrer">${modification ? 'Enregistrer' : 'Créer le dossier'}</button>
      </div>
    </form>`);

  // Aperçu du montant : il montre immédiatement quelle grille s'applique.
  const apercu = async () => {
    const d = lireFormulaire($('#frmCandidat'));
    const p2 = new URLSearchParams({
      dateDebut: d.dateDebutParcours || d.dateDemande || new Date().toISOString().slice(0, 10),
      forfait: !!d.forfaitArchitecture, individuel: d.heuresIndividuel || 0,
      collectif: d.heuresCollectif || 0, complement: d.heuresComplementFormatif || 0,
      jury: !!d.fraisJuryInclus, participants: 1,
    });
    try {
      const r = await API.get('/pilotage/simuler?' + p2);
      const el = $('#apercuTarif');
      el.className = 'banner ' + (r.plafondRespecte ? 'info' : 'warn');
      el.innerHTML = `<div><b>Grille ${esc(r.grille.code)}</b> — ${esc(r.grille.libelle)}<br>
        Montant prescrit <b>${eur(r.total)}</b> · coût ${eur(r.coutPedagogique)} · marge ${eur(r.marge)} (${pct(r.tauxMarge)})
        ${r.plafondRespecte ? '' : '<br>' + r.depassements.map(esc).join('<br>')}</div>`;
    } catch { /* l'aperçu est confortable, pas bloquant */ }
  };

  $$('#frmCandidat input,#frmCandidat select').forEach(el => el.addEventListener('change', apercu));
  apercu();

  // Les heures de compléments suivent les produits choisis, quand ils ont une durée.
  $('[data-multi="complementsFormatifs"]').addEventListener('change', () => {
    const ids = valeursMulti('complementsFormatifs', $('#frmCandidat'));
    const total = catalogue.filter(m => ids.includes(m.id)).reduce((a, m) => a + (m.dureeHeures || 0), 0);
    if (total > 0 || !ids.length) { $('#f_heuresComplementFormatif').value = total; apercu(); }
  });

  // À la création, la certification choisie pré-coche les modules de son socle obligatoire.
  if (!modification) {
    $('#f_certificationId').addEventListener('change', e => {
      const cert = Number(e.target.value);
      catalogue.filter(m => m.certifications.some(x => x.id === cert && x.obligatoire)).forEach(m => {
        const multi = $(`[data-multi="${estValeur(m.nature, 'ComplementFormatif') ? 'complementsFormatifs' : 'modulesElearning'}"]`);
        const caseModule = multi.querySelector(`input[value="${m.id}"]`);
        if (caseModule && !caseModule.checked) { caseModule.checked = true; resumerMulti(multi); }
      });
    });
  }

  $('#btnEnregistrer').onclick = async () => {
    const d = lireFormulaire($('#frmCandidat'));

    const erreurs = {};
    if (!d.nom) erreurs.nom = 'Le nom est obligatoire.';
    if (!d.prenom) erreurs.prenom = 'Le prénom est obligatoire.';
    marquerErreurs($('#frmCandidat'), erreurs);
    if (Object.keys(erreurs).length) { toast('Complétez les champs obligatoires.', 'err'); return; }

    $('#btnEnregistrer').disabled = true;
    try {
      const r = modification
        ? await API.put('/parcours/' + id, d)
        : await API.post('/parcours', d);

      fermerModale();
      if (r.doublonPossible) {
        toast('Un dossier existe déjà pour ce nom et cette certification. Vérifiez qu\'il ne s\'agit pas d\'un doublon.',
          'warn', 'Doublon possible');
      } else if (r.grilleChangee) {
        toast(`La date de démarrage a changé : le dossier passe à la grille ${r.grille}.`, 'warn', 'Grille modifiée');
      } else {
        toast(modification ? 'Dossier mis à jour.' : `Dossier créé, grille ${r.grille}.`, 'ok');
      }
      toastDossier(r.dossier);

      // La fiche ouverte doit refléter ce qui vient d'être enregistré,
      // consentement compris ; un dossier créé s'ouvre directement.
      if (modification) apresModification(id);
      else { rafraichir(); majCompteurs(); ouvrirFiche(r.id); }
    } catch (e) {
      toast(e.message, 'err', 'Enregistrement impossible');
      $('#btnEnregistrer').disabled = false;
    }
  };
}

/* ============================== JALONS ============================== */
async function formulaireJalons(id) {
  const d = await API.get('/parcours/' + id);
  const j = d.parcours.jalons;

  ouvrirModale('Jalons & jury', `${d.parcours.candidat.prenom} ${d.parcours.candidat.nom}`, `
    <form id="frmJalons" onsubmit="return false">
      <div class="form-grid">
        ${champ({ nom: 'premierContact', label: 'Premier contact', type: 'date', valeur: j.datePremierContact })}
        ${champ({ nom: 'recueilBesoins', label: 'Recueil des besoins', type: 'date', valeur: j.dateRecueilBesoins })}
        ${champ({ nom: 'rdvFaisabilite', label: 'RDV faisabilité', type: 'date', valeur: j.dateRdvFaisabilite })}
        ${champ({ nom: 'depotFaisabilite', label: 'Dépôt faisabilité', type: 'date', valeur: j.dateDepotFaisabilite })}
        ${champ({ nom: 'recevabilite', label: 'Recevabilité', type: 'date', valeur: j.dateRecevabilite })}
        ${champ({ nom: 'parcoursValide', label: 'Parcours validé', type: 'date', valeur: j.dateParcoursValide })}
        ${champ({ nom: 'debutAccompagnement', label: 'Début accompagnement', type: 'date', valeur: j.dateDebutAccompagnement })}
        ${champ({ nom: 'depotDossierValidation', label: 'Dépôt dossier de validation', type: 'date', valeur: j.dateDepotDossierValidation })}
        ${champ({ nom: 'jury', label: 'Passage en jury', type: 'date', valeur: j.dateJury })}
        ${champ({ nom: 'entretienPostJury', label: 'Entretien post-jury', type: 'date', valeur: j.dateEntretienPostJury })}
      </div>
      <fieldset class="fieldset"><legend>Résultat</legend>
        <div class="form-grid">
          ${champ({ nom: 'resultatJury', label: 'Résultat de jury', type: 'select', vide: false,
            valeur: d.parcours.resultatJury, options: ETAT.nomenclatures.resultatsJury })}
          ${champ({ nom: 'motifSortie', label: 'Motif de sortie', type: 'select',
            options: ETAT.nomenclatures.motifsSortie, vide: 'Aucun — le parcours continue' })}
          ${champ({ nom: 'commentaireSortie', label: 'Commentaire', type: 'textarea', span: true })}
        </div>
      </fieldset>
      <div class="form-actions"><span class="grow"></span>
        <button class="btn" onclick="fermerModale()">Annuler</button>
        <button class="btn primary" id="btnJalons">Enregistrer</button></div>
    </form>`);

  $('#btnJalons').onclick = async () => {
    try {
      await API.put(`/parcours/${id}/jalons`, lireFormulaire($('#frmJalons')));
      fermerModale();
      toast('Jalons enregistrés.', 'ok');
      apresModification(id);
    } catch (e) { toast(e.message, 'err', 'Enregistrement impossible'); }
  };
}

/* ============================== SÉANCE ============================== */
async function formulaireSeance(id) {
  ouvrirModale('Ajouter une séance', null, `
    <form id="frmSeance" onsubmit="return false">
      <div class="form-grid">
        ${champ({ nom: 'date', label: 'Date', type: 'date', requis: true, valeur: new Date().toISOString().slice(0, 10) })}
        ${champ({ nom: 'nature', label: 'Nature', type: 'select', vide: false, valeur: 'Individuel',
          options: ETAT.nomenclatures.naturesHeure })}
        ${champ({ nom: 'dureeHeures', label: 'Durée (heures)', type: 'number', pas: '0.25', min: 0.25, requis: true, valeur: 2 })}
        ${champ({ nom: 'intervenantId', label: 'Intervenant', type: 'select',
          options: optionsIntervenants(), vide: 'Non précisé' })}
        ${champ({ nom: 'modalite', label: 'Modalité', type: 'select', valeur: 'Distanciel', vide: false,
          options: ['Présentiel', 'Distanciel', 'Asynchrone'] })}
        ${champ({ nom: 'objet', label: 'Objet', span: true, exemple: 'Méthodologie du dossier, préparation orale…' })}
        ${champ({ nom: 'emargee', label: 'Émargement recueilli', type: 'checkbox' })}
        ${champ({ nom: 'realisee', label: 'Séance réalisée', type: 'checkbox', valeur: true,
          aide: 'Décochez pour enregistrer une séance seulement planifiée.' })}
      </div>
      <div class="form-actions"><span class="grow"></span>
        <button class="btn" onclick="fermerModale()">Annuler</button>
        <button class="btn primary" id="btnSeance">Ajouter</button></div>
    </form>`);

  $('#btnSeance').onclick = async () => {
    const d = lireFormulaire($('#frmSeance'));
    if (!d.date || !d.dureeHeures) { toast('La date et la durée sont obligatoires.', 'err'); return; }
    try {
      await API.post(`/parcours/${id}/seances`, d);
      fermerModale();
      toast('Séance enregistrée.', 'ok');
      apresModification(id);
    } catch (e) { toast(e.message, 'err', 'Enregistrement impossible'); }
  };
}

/* ============================== FINANCEMENT ============================== */
function formulaireFinancement(id, financementId = null) {
  const f = financementId ? FICHE_DONNEES?.financements.find(x => x.id === financementId) : null;

  ouvrirModale(f ? 'Modifier le financement' : 'Ajouter un financement', f ? f.dispositifLibelle : null, `
    <form id="frmFin" onsubmit="return false">
      <div class="form-grid">
        ${champ({ nom: 'dispositif', label: 'Dispositif', type: 'select', vide: false, valeur: f?.dispositif || 'Cpf',
          options: ETAT.nomenclatures.dispositifs.filter(d => d.code !== 'NonSecurise') })}
        ${champ({ nom: 'financeur', label: 'Financeur', valeur: f?.financeur, exemple: 'Uniformation, Caisse des Dépôts…' })}
        ${champ({ nom: 'numeroPriseEnCharge', label: 'N° de prise en charge', valeur: f?.numeroPriseEnCharge, span: true })}
        ${champ({ nom: 'montantAccorde', label: 'Montant accordé (€)', type: 'number', pas: '0.01', min: 0, valeur: f?.montantAccorde })}
        ${champ({ nom: 'resteACharge', label: 'Reste à charge (€)', type: 'number', pas: '0.01', min: 0, valeur: f?.resteACharge })}
        ${champ({ nom: 'dateDemande', label: 'Date de demande', type: 'date', valeur: f?.dateDemande })}
        ${champ({ nom: 'dateSecurisation', label: 'Date de sécurisation', type: 'date', valeur: f?.dateSecurisation })}
        ${champ({ nom: 'commentaire', label: 'Commentaire', type: 'textarea', valeur: f?.commentaire, span: true })}
      </div>
      <div class="form-actions"><span class="grow"></span>
        <button class="btn" onclick="fermerModale()">Annuler</button>
        <button class="btn primary" id="btnFin">${f ? 'Enregistrer' : 'Ajouter'}</button></div>
    </form>`);

  $('#btnFin').onclick = async () => {
    const d = lireFormulaire($('#frmFin'));
    try {
      if (f) await API.put(`/parcours/${id}/financements/${f.id}`, d);
      else await API.post(`/parcours/${id}/financements`, d);
      fermerModale();
      toast(f ? 'Financement modifié.' : 'Financement enregistré.', 'ok');
      apresModification(id);
    } catch (e) { toast(e.message, 'err', 'Enregistrement impossible'); }
  };
}

async function supprimerFinancement(id, financementId) {
  const f = FICHE_DONNEES?.financements.find(x => x.id === financementId);
  if (!confirm(`Supprimer le financement ${f?.dispositifLibelle || ''}${f?.financeur ? ' — ' + f.financeur : ''} ?`)) return;
  try {
    await API.del(`/parcours/${id}/financements/${financementId}`);
    toast('Financement supprimé.', 'ok');
    apresModification(id);
  } catch (e) { toast(e.message, 'err', 'Suppression impossible'); }
}

/* ============================== FACTURE ============================== */
async function formulaireFacture(id) {
  ouvrirModale('Enregistrer une facture', null, `
    <form id="frmFacture" onsubmit="return false">
      <div class="form-grid">
        ${champ({ nom: 'numero', label: 'Numéro de facture', requis: true, exemple: 'FA2608001' })}
        ${champ({ nom: 'dateEmission', label: "Date d'émission", type: 'date', requis: true,
          valeur: new Date().toISOString().slice(0, 10) })}
        ${champ({ nom: 'financeur', label: 'Financeur' })}
        ${champ({ nom: 'montantHt', label: 'Montant HT (€)', type: 'number', pas: '0.01', min: 0, requis: true })}
        ${champ({ nom: 'dateReglement', label: 'Date de règlement', type: 'date' })}
        ${champ({ nom: 'codeAcf', label: 'Code ACF' })}
      </div>
      <div class="form-actions"><span class="grow"></span>
        <button class="btn" onclick="fermerModale()">Annuler</button>
        <button class="btn primary" id="btnFacture">Enregistrer</button></div>
    </form>`);

  $('#btnFacture').onclick = async () => {
    const d = lireFormulaire($('#frmFacture'));
    if (!d.numero || !d.montantHt) { toast('Le numéro et le montant sont obligatoires.', 'err'); return; }
    try {
      await API.post(`/parcours/${id}/factures`, d);
      fermerModale();
      toast('Facture enregistrée.', 'ok');
      apresModification(id);
    } catch (e) { toast(e.message, 'err', 'Enregistrement impossible'); }
  };
}

/* ============================== RÉSEAU VAE ============================== */
VUES.reseau = async function () {
  const intervenants = await API.get('/intervenants');
  ETAT.intervenants = intervenants;

  $('#vActions').innerHTML =
    `<button class="btn primary" onclick="formulaireIntervenant()">Nouvel intervenant</button>`;

  const parType = t => intervenants.filter(i => estValeur(i.type, t));
  const aaps = parType('ArchitecteAccompagnateurParcours');
  const accs = parType('Accompagnateur');
  const experts = parType('ExpertMetier');

  const tableau = (liste, titre, indice) => carte('', `<div class="tw"><table class="t">
    <thead><tr><th>Intervenant</th><th>Statut</th><th>Spécialités</th><th>Territoire</th>
    <th class="n">Actifs</th><th class="n">Habilitations</th><th class="n">Heures</th><th></th></tr></thead>
    <tbody>${liste.length ? liste.map(i => `<tr>
      <td>${who(i.nomComplet, i.email, true)}</td>
      <td><span class="chip c-${estValeur(i.statut, 'Actif') ? 'ok' : estValeur(i.statut, 'RetireDuReseau') ? 'crit' : 'warn'}">${esc(i.statutLibelle)}</span></td>
      <td style="max-width:210px;font-size:11.5px;color:var(--ink-2)">${esc(i.specialites || '—')}</td>
      <td style="font-size:11.5px">${esc(i.region || i.territoire || '—')}
        ${i.interventionDistanciel ? '<br><span class="chip c-info">distanciel</span>' : ''}</td>
      <td class="n"><b>${i.candidatsActifs}</b> / ${i.capacite}</td>
      <td class="n">${i.habilitations.length || '—'}</td>
      <td class="n">${i.heuresRealisees ? nf(i.heuresRealisees) + ' h' : '—'}</td>
      <td><button class="btn ghost" style="padding:2px 8px;font-size:11.5px"
        onclick="formulaireIntervenant(${i.id})">Modifier</button></td></tr>`).join('')
      : '<tr><td colspan="8">' + vide('Aucun intervenant de ce type.') + '</td></tr>'}
    </tbody></table></div>`, indice, 'tight');

  $('#vue').innerHTML = `
  <div class="kpi-grid">
    ${kpi('Architectes de parcours', aaps.filter(i => i.mobilisable).length, 'AAP mobilisables', 'brand')}
    ${kpi('Accompagnateurs', accs.filter(i => i.mobilisable).length, `sur ${accs.length} référencés`)}
    ${kpi('Experts métiers', experts.length, 'référencés')}
    ${kpi('Habilitations', intervenants.reduce((a, i) => a + i.habilitations.length, 0),
      'liens intervenant × certification')}
    ${kpi('Retirés du réseau', intervenants.filter(i => estValeur(i.statut, 'RetireDuReseau')).length,
      '', 'neutral')}
  </div>

  ${aSavoir([['Charge des équipes', 'charge'], ['Capacités cibles', 'regles']])}
  ${section('Architectes accompagnateurs de parcours')}
  ${tableau(aaps, 'AAP', aaps.length + ' intervenants')}
  ${section('Accompagnateurs VAE')}
  ${tableau(accs, 'Accompagnateurs', accs.length + ' intervenants')}
  ${section('Experts métiers &amp; fonctions internes')}
  ${tableau([...experts, ...parType('Interne')], 'Autres', (experts.length + parType('Interne').length) + ' intervenants')}`;
};

async function formulaireIntervenant(id = null) {
  const modification = id != null;
  const i = modification ? ETAT.intervenants.find(x => x.id === id) : null;
  const habilitees = new Set((i?.habilitations || []).map(h => h.certificationId));

  // Le référentiel complet, pas seulement les certifications déjà demandées :
  // on habilite souvent un intervenant sur une certification à ouvrir.
  const toutes = await API.get('/certifications?vaeSeulement=true');

  ouvrirModale(
    modification ? 'Modifier un intervenant' : 'Nouvel intervenant',
    modification ? i.nomComplet : null,
    `<form id="frmIntervenant" onsubmit="return false">
      <fieldset class="fieldset"><legend>Identité</legend>
        <div class="form-grid">
          ${champ({ nom: 'nom', label: 'Nom', requis: true, valeur: i?.nom })}
          ${champ({ nom: 'prenom', label: 'Prénom', valeur: i?.prenom })}
          ${champ({ nom: 'type', label: 'Type', type: 'select', vide: false,
            valeur: i?.type || 'Accompagnateur', options: ETAT.nomenclatures.typesIntervenant })}
          ${champ({ nom: 'statut', label: 'Statut', type: 'select', vide: false,
            valeur: i?.statut || 'Actif', options: ETAT.nomenclatures.statutsIntervenant })}
          ${champ({ nom: 'email', label: 'Courriel', type: 'email', valeur: i?.email })}
          ${champ({ nom: 'telephone', label: 'Téléphone', valeur: i?.telephone })}
          ${champ({ nom: 'siret', label: 'SIRET', valeur: i?.siret, aide: 'Pour les intervenants indépendants' })}
        </div>
      </fieldset>

      <fieldset class="fieldset"><legend>Intervention</legend>
        <div class="form-grid">
          ${champ({ nom: 'region', label: 'Région', type: 'select', valeur: i?.region, options: REGIONS, vide: '—' })}
          ${champ({ nom: 'territoire', label: 'Territoire précis', valeur: i?.territoire,
            exemple: 'Hérault, Montpellier et alentours…' })}
          ${champ({ nom: 'tarifHoraire', label: 'Tarif horaire (€)', type: 'number', pas: '0.5', min: 0,
            valeur: i?.tarifHoraire ?? 30 })}
          ${champ({ nom: 'capaciteCandidats', label: 'Capacité (candidats)', type: 'number', min: 1,
            valeur: i?.capacite, aide: 'Vide : valeur par défaut du service' })}
          ${champ({ nom: 'capaciteHeuresTrimestre', label: 'Heures par trimestre', type: 'number', min: 0 })}
          ${champ({ nom: 'dateEntreeReseau', label: "Entrée au réseau", type: 'date', valeur: i?.dateEntreeReseau })}
          ${champ({ nom: 'dateSortieReseau', label: 'Sortie du réseau', type: 'date', valeur: i?.dateSortieReseau })}
          ${champ({ nom: 'interventionDistanciel', label: 'Intervient à distance', type: 'checkbox',
            valeur: i ? i.interventionDistanciel : true })}
          ${champ({ nom: 'interventionPresentiel', label: 'Intervient en présentiel', type: 'checkbox',
            valeur: i ? i.interventionPresentiel : true })}
          ${champ({ nom: 'specialites', label: 'Spécialités', type: 'textarea', valeur: i?.specialites, span: true,
            exemple: 'Petite enfance, économie sociale et familiale, formation d\'adultes…' })}
          ${champ({ nom: 'notes', label: 'Notes', type: 'textarea', valeur: i?.notes, span: true })}
        </div>
      </fieldset>

      <fieldset class="fieldset"><legend>Certifications habilitées</legend>
        <input class="f" id="filtreHabil" placeholder="Filtrer les certifications…" style="width:100%;max-width:none;margin-bottom:8px">
        <div class="picker" id="pickerHabil">
          ${toutes.map(c => `<label data-lib="${esc((c.abrege || '') + ' ' + c.intitule).toLowerCase()}">
            <input type="checkbox" value="${c.id}"${habilitees.has(c.id) ? ' checked' : ''}>
            <span>${esc(c.abrege ? c.abrege + ' — ' : '')}${esc(c.intitule)}</span>
            <span class="code">${esc(c.codeRncp)}</span></label>`).join('')}
        </div>
      </fieldset>

      <div class="form-actions"><span class="grow"></span>
        <button class="btn" onclick="fermerModale()">Annuler</button>
        <button class="btn primary" id="btnIntervenant">${modification ? 'Enregistrer' : 'Ajouter au réseau'}</button></div>
    </form>`);

  $('#filtreHabil').oninput = e => {
    const q = e.target.value.toLowerCase();
    $$('#pickerHabil label').forEach(l => { l.hidden = q && !l.dataset.lib.includes(q); });
  };

  $('#btnIntervenant').onclick = async () => {
    const d = lireFormulaire($('#frmIntervenant'));
    if (!d.nom) { marquerErreurs($('#frmIntervenant'), { nom: 'Le nom est obligatoire.' }); return; }

    d.certificationsHabilitees = $$('#pickerHabil input:checked').map(x => Number(x.value));

    $('#btnIntervenant').disabled = true;
    try {
      const r = modification
        ? await API.put('/intervenants/' + id, d)
        : await API.post('/intervenants', d);

      fermerModale();
      if (r.doublonPossible) toast('Un intervenant porte déjà ce nom. Vérifiez qu\'il ne s\'agit pas d\'un doublon.', 'warn');
      else toast(modification ? 'Intervenant mis à jour.' : 'Intervenant ajouté au réseau.', 'ok');

      ETAT.intervenants = await API.get('/intervenants');
      rafraichir();
    } catch (e) {
      toast(e.message, 'err', 'Enregistrement impossible');
      $('#btnIntervenant').disabled = false;
    }
  };
}

