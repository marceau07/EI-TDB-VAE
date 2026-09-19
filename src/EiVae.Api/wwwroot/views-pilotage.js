/* =========================================================================
   VUES DE PILOTAGE — direction, alertes, pipeline, délais, charge, heures,
   qualité, finance. Toutes lisent les agrégats calculés par l'API.
   ========================================================================= */

const TON_SEV = { critique: 'crit', vigilance: 'warn', conforme: 'ok', neutre: '' };

function majFiltreCount(n) {
  $('#fCount').textContent = n + (n > 1 ? ' dossiers' : ' dossier');
}

/* ============================== DIRECTION ============================== */
VUES.direction = async function () {
  const s = await API.get('/parcours/synthese' + paramsFiltres());
  majFiltreCount(s.dossiersActifs + s.dossiersClotures + s.sorties);

  const total = s.dossiersActifs + s.dossiersClotures + s.sorties;
  const evolution = s.evolutionDemandes || [];
  const dernier = evolution.at(-1)?.valeur ?? 0;
  const precedent = evolution.at(-2)?.valeur ?? 0;
  const ecart = dernier - precedent;

  const couleurs = ['#0E6D7D', '#0F7D8E', '#0B8DA3', '#009CB7', '#2FAEC5',
    '#55BED2', '#7BCEDE', '#9BDAE7', '#B9E5EE', '#D2EEF3'];
  const pipeline = s.repartitionPipeline || [];
  const totalPipeline = pipeline.reduce((a, x) => a + x.nombre, 0) || 1;

  $('#vue').innerHTML = `
  ${section('Activité')}
  <div class="kpi-grid">
    ${kpi('Dossiers entrants ce mois', s.dossiersEntrantsMois, (ecart >= 0 ? '+' : '') + ecart + ' vs mois précédent', 'brand')}
    ${kpi('Dossiers actifs', s.dossiersActifs, 'parcours en cours de traitement')}
    ${kpi('Dossiers clôturés', s.dossiersClotures, eur(s.caFacture) + ' facturés')}
    ${kpi('Sorties de parcours', s.sorties, pct(s.tauxSortie) + ' du total traité', 'warn')}
  </div>
  <div class="grid" style="grid-template-columns:repeat(auto-fit,minmax(320px,1fr));margin-top:14px">
    ${carte('Évolution mensuelle des demandes',
      courbe(evolution.map(m => m.valeur)) +
      `<div class="mini-months">${evolution.filter((_, i) => i % 3 === 0 || i === evolution.length - 1)
        .map(m => `<span>${esc(m.mois)} ${String(m.annee).slice(2)}</span>`).join('')}</div>`,
      '18 derniers mois · ' + total + ' demandes')}
    ${carte('Répartition du portefeuille actif',
      `<div class="stack">${pipeline.map((x, i) => `<i style="width:${x.nombre / totalPipeline * 100}%;background:${couleurs[i % couleurs.length]}" title="${esc(x.etape)} : ${x.nombre}"></i>`).join('')}</div>
       <div class="legend">${pipeline.map((x, i) => `<span><i style="background:${couleurs[i % couleurs.length]}"></i>${esc(x.etape)} <b class="num">${x.nombre}</b></span>`).join('')}</div>`,
      'par étape du parcours')}
  </div>

  ${section('Pipeline')}
  <div class="kpi-grid">
    ${kpi('Demandes en qualification', s.enQualification, "entrée dans le processus")}
    ${kpi('Financement & faisabilité', s.enFaisabilite, 'étapes E4 à E7')}
    ${kpi('Accompagnements', s.enAccompagnement, 'parcours engagés')}
    ${kpi('Jurys à venir', s.jurysAVenir, s.jurysAVenir ? 'dates programmées' : 'aucune date programmée', s.jurysAVenir ? 'brand' : '')}
    ${kpi('Certifications obtenues', s.certificationsObtenues, 'validations totales enregistrées', 'ok')}
  </div>

  ${section('Performance')}
  <div class="kpi-grid">
    ${kpi('Délai moyen de prise en charge', s.delaiPriseEnCharge != null ? s.delaiPriseEnCharge + '<small> j</small>' : '—',
      'cible France VAE : 8 j ouvrés', s.delaiPriseEnCharge > 11 ? 'crit' : 'ok')}
    ${kpi('Durée moyenne de parcours', s.dureeMoyenneParcoursJours != null ? Math.round(s.dureeMoyenneParcoursJours / 30) + '<small> mois</small>' : '—',
      'objectif national : 6 à 8 mois', s.dureeMoyenneParcoursJours > 240 ? 'warn' : 'ok')}
    ${kpi('Taux de validation totale', pct(s.tauxValidationTotale),
      s.resultatsNonRenseignes ? s.resultatsNonRenseignes + ' résultats non renseignés' : 'sur les jurys passés',
      s.resultatsNonRenseignes > s.certificationsObtenues ? 'warn' : 'ok')}
    ${kpi('Dossiers conformes', s.dossiersConformes, ratio(s.dossiersConformes, s.dossiersActifs) + ' du portefeuille actif',
      s.dossiersConformes ? 'ok' : 'crit')}
  </div>

  ${section('Finance')}
  <div class="kpi-grid">
    ${kpi('CA prévisionnel', eurK(s.caPrevisionnel), 'portefeuille actif valorisé', 'brand')}
    ${kpi('CA facturé', eurK(s.caFacture), 'factures émises')}
    ${kpi('CA encaissé', eurK(s.caEncaisse), ratio(s.caEncaisse, s.caFacture) + ' du facturé', 'ok')}
    ${kpi('Marge prévisionnelle', eurK(s.margePrevisionnelle), 'après coût des intervenants', 'brand')}
    ${kpi('Panier moyen facturé', eur(s.panierMoyen), 'par facture émise')}
  </div>

  ${section('Ressources')}
  <div class="kpi-grid">
    ${kpi('AAP opérationnels', s.nbAap, 'architectes de parcours actifs', 'brand')}
    ${kpi('Charge moyenne par AAP', nf(s.chargeMoyenneAap) + '<small> dossiers</small>', 'capacité cible paramétrable',
      s.aapEnSurcharge ? 'crit' : 'ok')}
    ${kpi('Capacité disponible', s.capaciteDisponible > 0 ? s.capaciteDisponible + '<small> dossiers</small>' : 'saturé',
      'avant saturation du réseau', s.capaciteDisponible <= 0 ? 'crit' : s.capaciteDisponible < 6 ? 'warn' : 'ok')}
    ${kpi('AAP en surcharge', s.aapEnSurcharge, s.aapEnSurcharge ? 'au-delà de la capacité cible' : 'aucun',
      s.aapEnSurcharge ? 'crit' : 'ok')}
  </div>

  ${section('Alertes')}
  <div class="kpi-grid">
    ${kpi('Alertes critiques', s.alertesCritiques, 'action immédiate requise', s.alertesCritiques ? 'crit' : 'ok')}
    ${kpi('Alertes de vigilance', s.alertesVigilance, 'à surveiller cette semaine', s.alertesVigilance ? 'warn' : 'ok')}
    ${kpi('Financements non sécurisés', s.financementsNonSecurises, 'parcours engagés sans prise en charge', 'crit')}
    ${kpi('Dossiers sans activité', s.dossiersSansActivite, 'au-delà du seuil paramétré', 'crit')}
    ${kpi('Prestations non facturées', s.prestationsNonFacturees, eur(s.montantNonFacture) + ' en attente', 'crit')}
  </div>
  ${aSavoir([['Plan d\'action', 'alertes'], ['Notifications', 'notifications'], ['Seuils et règles d\'alerte', 'regles']], true)}`;
};

/* ============================== ALERTES ============================== */
VUES.alertes = async function () {
  const [alertes, regles] = await Promise.all([
    API.get('/parcours/alertes' + paramsFiltres()),
    API.get('/referentiel/regles'),
  ]);

  majFiltreCount(new Set(alertes.map(a => a.parcoursId)).size);

  const parRegle = {};
  alertes.forEach(a => { parRegle[a.code] = (parRegle[a.code] || 0) + 1; });

  const critiques = alertes.filter(a => a.severite === 'critique').length;

  $('#vue').innerHTML = `
  ${aSavoir([
    'Résoudre : saisir l\'action qui lève l\'alerte',
    'Reporter : masquer l\'alerte jusqu\'à une date',
    ['Modifier les règles', 'regles'],
  ])}
  <div class="kpi-grid">
    ${kpi('Alertes critiques', critiques, 'action immédiate', critiques ? 'crit' : 'ok')}
    ${kpi('Alertes de vigilance', alertes.length - critiques, 'à surveiller', alertes.length - critiques ? 'warn' : 'ok')}
    ${kpi('Dossiers concernés', new Set(alertes.map(a => a.parcoursId)).size, 'sur le périmètre filtré')}
    ${kpi('Règles déclenchées', Object.keys(parRegle).length + ' / ' + regles.length, 'sur les règles actives')}
  </div>

  ${section('Répartition par règle')}
  ${carte('', barres(regles.filter(r => parRegle[r.code]).sort((a, b) => parRegle[b.code] - parRegle[a.code])
      .map(r => ({ l: r.code + ' · ' + r.libelle, v: parRegle[r.code], cls: r.severite === 'critique' ? 'crit' : 'warn' })),
    { large: true }), 'nombre de dossiers concernés')}

  ${section("Plan d'action", alertes.length + ' alertes ouvertes')}
  ${carte('', alertes.length ? alertes.map(a => `
    <div class="alert-row a-${a.severite === 'critique' ? 'crit' : 'warn'}">
      <span class="sev"></span>
      <div class="alert-b">
        <div class="l1">
          <span class="chip c-${a.severite === 'critique' ? 'crit' : 'warn'}">${esc(a.code)}</span>
          <b>${esc(a.libelle)}</b>
          <span class="chip c-neutral">${esc(a.etape)}</span>
        </div>
        <div class="l2"><a href="#" onclick="ouvrirFiche(${a.parcoursId});return false;">${esc(a.candidat)}</a>
          · ${esc(a.certification || 'certification non renseignée')} — ${esc(a.detail)}</div>
        <div class="l3">
          <span>AAP ${esc(a.aap || 'non affecté')}</span>
          <span>financement ${esc(a.financement)}</span>
          <span>${esc(a.region || '—')}</span>
          <span>${esc(a.fondement)}</span>
        </div>
      </div>
      <div class="alert-r">
        <span class="act">${esc(a.actionAttendue)}</span>
        <span class="age">${esc(a.responsable)}</span>
        <span class="age">dernier mouvement ${fmtDate(a.dernierMouvement)}</span>
        <span style="display:flex;gap:5px">
          <button class="btn primary mini" onclick="resoudreDepuisPlan(${a.parcoursId},${a.id})">Résoudre</button>
          <button class="btn ghost mini" onclick="reporterAlerte(${a.id})">Reporter</button>
        </span>
      </div>
    </div>`).join('') : vide('Aucune alerte sur ce périmètre.'), '', 'tight')}`;
};

async function reporterAlerte(id) {
  const dans7j = new Date(Date.now() + 7 * 864e5).toISOString().slice(0, 10);
  ouvrirModale('Reporter l\'alerte', null, `
    <form id="frmReport" onsubmit="return false">
      <div class="form-grid">
        ${champ({ nom: 'jusquA', label: "Reporter jusqu'au", type: 'date', valeur: dans7j, requis: true })}
        ${champ({ nom: 'motif', label: 'Motif', type: 'text', exemple: 'Relance programmée, congés du candidat…', span: true })}
      </div>
      <div class="form-actions">
        <span class="grow"></span>
        <button class="btn" onclick="fermerModale()">Annuler</button>
        <button class="btn primary" id="btnReport">Reporter</button>
      </div>
    </form>`);

  $('#btnReport').onclick = async () => {
    const d = lireFormulaire($('#frmReport'));
    if (!d.jusquA) { toast('Indiquez une date de report.', 'err'); return; }
    try {
      await API.post(`/parcours/alertes/${id}/reporter`, d);
      fermerModale();
      toast('Alerte reportée.', 'ok');
      if ($('#drawer').classList.contains('on') && FICHE_OUVERTE) ouvrirFiche(FICHE_OUVERTE);
      rafraichir();
    } catch (e) { toast(e.message, 'err', 'Report impossible'); }
  };
}

/* ============================== PIPELINE ============================== */
VUES.pipeline = async function () {
  const [lignes, etapes] = await Promise.all([
    API.get('/parcours' + paramsFiltres()),
    API.get('/referentiel/etapes'),
  ]);
  majFiltreCount(lignes.length);

  const actifs = lignes.filter(l => l.etape !== 'Sorti du parcours');
  const sorties = lignes.filter(l => l.etape === 'Sorti du parcours');
  const colonnes = etapes.filter(e => e.code !== 'Sortie');

  $('#vue').innerHTML = `
  <div class="kpi-grid" style="margin-bottom:16px">
    ${kpi('Dossiers dans le pipeline', actifs.length, 'toutes étapes confondues')}
    ${kpi("En attente d'un tiers", actifs.filter(l => l.statutSecondaire).length,
      'candidat, financeur ou certificateur', 'warn')}
    ${kpi('Sortis du parcours', sorties.length, 'hors pipeline')}
    ${kpi('Alertes critiques', actifs.filter(l => l.severite === 'critique').length, 'dossiers à traiter', 'crit')}
  </div>
  <div class="pipe">
    ${colonnes.map(e => {
      const dedans = actifs.filter(l => l.etapeRang === e.rang);
      return `<div class="pcol">
        <div class="pcol-h">
          <div class="t"><span class="step">E${e.rang}</span><span>${esc(e.libelle)}</span><span class="n">${dedans.length}</span></div>
        </div>
        <div class="pcol-b">
          ${dedans.length ? dedans.map(l => carteParcours(l)).join('')
            : '<div style="padding:14px 4px;text-align:center;color:var(--faint);font-size:11.5px">—</div>'}
        </div></div>`;
    }).join('')}
    <div class="pcol" style="background:var(--sunken)">
      <div class="pcol-h"><div class="t"><span class="step" style="background:var(--muted)">—</span><span>Sorties</span><span class="n">${sorties.length}</span></div></div>
      <div class="pcol-b">${sorties.slice(0, 25).map(l => carteParcours(l, true)).join('')}
        ${sorties.length > 25 ? `<div style="font-size:10.5px;color:var(--faint);padding:4px">+ ${sorties.length - 25} autres</div>` : ''}</div>
    </div>
  </div>
  <div class="legend" style="margin-top:4px">
    <span><i style="background:var(--crit)"></i>alerte critique</span>
    <span><i style="background:var(--warn)"></i>vigilance</span>
    <span><i style="background:var(--ok)"></i>conforme</span>
    <span style="color:var(--faint)">jours depuis le dernier mouvement</span>
  </div>`;
};

function carteParcours(l, terne) {
  const anciennete = jours(l.dernierMouvement);
  return `<button class="pcard sev-${TON_SEV[l.severite] || ''}" onclick="ouvrirFiche(${l.id})"${terne ? ' style="opacity:.72"' : ''}>
    <b>${esc(l.prenom)} ${esc(l.nom)}</b>
    <span class="m">${esc(l.certification || 'certification non renseignée')}</span>
    ${l.statutSecondaire ? `<span class="chip c-warn" style="margin-top:5px">${esc(l.statutSecondaire)}</span>` : ''}
    ${l.sortie ? `<span class="chip c-neutral" style="margin-top:5px">${esc(l.sortie)}</span>` : ''}
    <span class="f">
      <span class="dot d-${TON_SEV[l.severite] || 'neutral'}"></span>
      <span>${esc(inits(l.aap || '—'))}</span><span>·</span>
      <span>${anciennete != null ? anciennete + ' j' : '—'}</span>
      ${l.financement === 'Non sécurisé' ? '<span>· financement ?</span>' : ''}
    </span></button>`;
}

/* ============================== CANDIDATS ============================== */
VUES.candidats = async function () {
  const lignes = await API.get('/parcours' + paramsFiltres());
  majFiltreCount(lignes.length);

  $('#vActions').innerHTML =
    `<button class="btn primary" onclick="formulaireCandidat()">Nouveau candidat</button>`;

  $('#vue').innerHTML = `
  ${aSavoir([
    'Cliquez une ligne pour ouvrir la fiche',
    ['Nouveau candidat', null, 'formulaireCandidat()'],
    ['Importer des candidatures', 'integrations'],
  ])}
  ${carte('', `<div class="tw"><table class="t">
    <thead><tr>
      <th>Candidat</th><th>Certification</th><th>Étape</th><th>AAP</th><th>Accompagnateur</th>
      <th>Financement</th><th class="n">Heures</th><th class="n">Montant</th><th>Grille</th>
      <th class="n">Dernier mvt</th><th>État</th>
    </tr></thead>
    <tbody>${lignes.length ? lignes.map(l => {
      const anciennete = jours(l.dernierMouvement);
      return `<tr class="clik" onclick="ouvrirFiche(${l.id})">
        <td>${who(l.prenom + ' ' + l.nom, [l.ville, l.region].filter(Boolean).join(' · '), true)}</td>
        <td>${l.certification ? `<b style="font-weight:600">${esc(l.certification)}</b><br><span style="font-size:10.5px;color:var(--faint)">${esc(l.codeRncp || '')}${l.niveau ? ' · niv. ' + l.niveau : ''}</span>` : '<span class="chip c-warn">non renseignée</span>'}</td>
        <td>${l.sortie ? `<span class="chip c-neutral">${esc(l.sortie)}</span>`
          : `<span class="chip c-${l.etapeRang === 12 ? 'ok' : 'brand'}">E${l.etapeRang} ${esc(l.etape)}</span>`}
          ${l.statutSecondaire ? `<br><span class="chip c-warn" style="margin-top:3px">${esc(l.statutSecondaire)}</span>` : ''}</td>
        <td>${l.aap ? who(l.aap, '', true) : '<span class="chip c-warn">non affecté</span>'}</td>
        <td>${l.accompagnateur ? who(l.accompagnateur, '', true) : '<span style="color:var(--faint)">—</span>'}</td>
        <td><span class="chip c-${l.financement === 'Non sécurisé' ? 'crit' : 'ok'}">${esc(l.financement)}</span></td>
        <td class="n">${l.heuresPrescrites ? nf(l.heuresRealisees) + ' / ' + nf(l.heuresPrescrites) : '—'}</td>
        <td class="n">${l.montant ? eur(l.montant) : '—'}</td>
        <td><span class="chip c-neutral">${esc(l.codeGrille)}</span></td>
        <td class="n">${anciennete != null ? anciennete + ' j' : '—'}</td>
        <td>${l.nbAlertes ? `<span class="chip c-${TON_SEV[l.severite]}">${l.nbAlertes} alerte${l.nbAlertes > 1 ? 's' : ''}</span>`
          : (l.severite === 'conforme' ? '<span class="chip c-ok">conforme</span>' : '<span style="color:var(--faint)">—</span>')}</td>
      </tr>`;
    }).join('') : '<tr><td colspan="11">' + vide('Aucun candidat sur ce périmètre.') + '</td></tr>'}</tbody>
  </table></div>`, lignes.length + ' dossiers', 'tight')}`;
};

/* ============================== DÉLAIS ============================== */
VUES.delais = async function () {
  const d = await API.get('/pilotage/delais' + paramsFiltres());
  majFiltreCount(d.total);

  const base = d.total || 1;

  $('#vue').innerHTML = `
  ${section('Entonnoir du parcours')}
  ${carte('', `<div class="funnel">${d.entonnoir.map((x, i) => {
    const prec = i ? d.entonnoir[i - 1].nombre : x.nombre;
    const taux = prec ? Math.round(x.nombre / prec * 100) : 100;
    return `<div class="fn-row">
      <span class="fn-lab"><span class="step">${esc(x.rang)}</span><span style="overflow:hidden;text-overflow:ellipsis">${esc(x.libelle)}</span></span>
      <span class="fn-bar"><i style="width:${x.nombre / base * 100}%"></i><b>${x.nombre}</b></span>
      <span class="fn-meta">${i ? (taux < 60 ? `<em>${taux} %</em>` : taux + ' %') + ' de passage' : ratio(x.nombre, base) + ' du total'}</span>
    </div>`;
  }).join('')}</div>`, d.total + ' dossiers analysés')}

  <div class="grid" style="grid-template-columns:repeat(auto-fit,minmax(330px,1fr));margin-top:14px">
    ${carte('Délais observés par transition', barres(d.transitions.map(t => ({
      l: t.libelle, v: t.moyenne, repere: t.cible,
      n: t.moyenne + ' j (méd. ' + t.mediane + ')',
      cls: t.cible && t.moyenne > t.cible ? 'crit' : t.cible && t.moyenne > t.cible * .8 ? 'warn' : 'ok',
    })), { large: true }), 'trait rouge = délai cible ou réglementaire')}
    ${carte('Où les dossiers sont perdus', barres(d.pertes.map(p => ({
      l: p.etape, v: p.nombre, cls: 'warn', n: p.nombre + ' dossiers',
    })), { large: true }), 'dernier jalon atteint avant la sortie')}
  </div>

  ${section('Dossiers hors délai')}
  ${carte('', `<div class="tw"><table class="t">
    <thead><tr><th>Transition</th><th class="n">Mesurés</th><th class="n">Moyenne</th><th class="n">Médiane</th>
    <th class="n">Cible</th><th class="n">Hors délai</th><th>Écart</th></tr></thead>
    <tbody>${d.transitions.map(t => `<tr>
      <td><b style="font-weight:600">${esc(t.libelle)}</b></td>
      <td class="n">${t.mesures}</td><td class="n">${t.moyenne} j</td><td class="n">${t.mediane} j</td>
      <td class="n">${t.cible ? t.cible + ' j' : '—'}</td>
      <td class="n">${t.horsDelai}${t.mesures ? ` <span style="color:var(--faint)">(${Math.round(t.horsDelai / t.mesures * 100)} %)</span>` : ''}</td>
      <td>${t.cible ? (t.moyenne > t.cible ? `<span class="chip c-crit">+${t.moyenne - t.cible} j</span>`
        : '<span class="chip c-ok">dans la cible</span>') : '—'}</td>
    </tr>`).join('')}</tbody></table></div>`, '', 'tight')}`;
};

/* ============================== CHARGE ============================== */
VUES.charge = async function () {
  const c = await API.get('/pilotage/charge');
  majFiltreCount(c.dossiersActifs);

  $('#vue').innerHTML = `
  <div class="kpi-grid">
    ${kpi('AAP opérationnels', c.aaps.length, c.dossiersActifs + ' dossiers actifs à répartir', 'brand')}
    ${kpi('Capacité disponible', c.capaciteDisponible > 0 ? c.capaciteDisponible + '<small> dossiers</small>' : 'saturé',
      `${c.aaps.length} AAP × ${c.capaciteAap} dossiers`, c.capaciteDisponible <= 0 ? 'crit' : c.capaciteDisponible < 6 ? 'warn' : 'ok')}
    ${kpi('Dossiers sans AAP', c.sansAap, c.sansAap ? 'à affecter' : 'tous affectés', c.sansAap ? 'crit' : 'ok')}
    ${kpi('Accompagnateurs actifs', c.accompagnateurs.length, 'mobilisables sur le réseau')}
    ${kpi('Certifications non couvertes', c.certificationsNonCouvertes.length,
      c.certificationsNonCouvertes.length ? 'demande active sans intervenant habilité' : 'toutes couvertes',
      c.certificationsNonCouvertes.length ? 'warn' : 'ok')}
  </div>

  ${section('Charge des architectes accompagnateurs de parcours')}
  ${carte('', `<div class="tw"><table class="t">
    <thead><tr><th>AAP</th><th class="n">Actifs</th><th class="n">Nouveaux (60 j)</th><th class="n">Faisabilité</th>
    <th class="n">Accompagnement</th><th class="n">Jury &lt; 90 j</th><th class="n">Alertes</th><th style="width:170px">Charge</th></tr></thead>
    <tbody>${c.aaps.map(a => `<tr>
      <td>${who(a.nom)}</td>
      <td class="n"><b>${a.actifs}</b></td><td class="n">${a.nouveaux}</td><td class="n">${a.faisabilite}</td>
      <td class="n">${a.accompagnement}</td><td class="n">${a.juryProche}</td>
      <td class="n">${a.alertesCritiques ? `<span class="chip c-crit">${a.alertesCritiques}</span>` : '—'}</td>
      <td>${jauge(a.tauxCharge, a.actifs + ' / ' + a.capacite)}</td></tr>`).join('')}
      ${c.sansAap ? `<tr><td><span class="chip c-crit">Non affectés</span></td><td class="n"><b>${c.sansAap}</b></td>
        <td colspan="6" style="color:var(--muted)">à répartir entre les AAP disponibles</td></tr>` : ''}
    </tbody></table></div>`, `capacité cible : ${c.capaciteAap} dossiers par AAP`, 'tight')}

  ${section("Réseau d'accompagnateurs")}
  ${carte('', `<div class="tw"><table class="t">
    <thead><tr><th>Intervenant</th><th>Spécialités</th><th>Territoire</th><th class="n">Actifs</th>
    <th class="n">H. prescrites</th><th class="n">H. réalisées</th><th style="width:150px">Charge</th></tr></thead>
    <tbody>${c.accompagnateurs.map(a => `<tr>
      <td>${who(a.nom, a.tarifHoraire ? a.tarifHoraire + ' €/h' : '', true)}</td>
      <td style="max-width:200px;font-size:11.5px;color:var(--ink-2)">${esc(a.specialites || '—')}</td>
      <td style="font-size:11.5px">${esc(a.region || a.territoire || '—')}${a.interventionDistanciel ? '<br><span class="chip c-info">distanciel</span>' : ''}</td>
      <td class="n"><b>${a.actifs}</b></td>
      <td class="n">${a.heuresPrescrites ? nf(a.heuresPrescrites) + ' h' : '—'}</td>
      <td class="n">${a.heuresRealisees ? nf(a.heuresRealisees) + ' h' : '—'}</td>
      <td>${jauge(a.tauxCharge, a.actifs + ' / ' + a.capacite)}</td></tr>`).join('')}
    </tbody></table></div>`, `capacité cible : ${c.capaciteAcc} candidats simultanés`, 'tight')}

  ${c.certificationsNonCouvertes.length ? `
  ${section('Certifications à couvrir')}
  ${carte('', `<div style="display:flex;gap:7px;flex-wrap:wrap">${c.certificationsNonCouvertes.map(x =>
    `<span class="chip c-crit">${esc(x.intitule)}</span>`).join('')}</div>
    ${aSavoir([['Habiliter un intervenant', 'reseau']], true)}`)}` : ''}

  ${section("Aide à l'affectation")}
  ${carte('', `
    <div style="display:flex;gap:11px;flex-wrap:wrap;align-items:flex-end;margin-bottom:13px">
      <div class="field" style="min-width:280px"><label for="affCert">Certification à couvrir</label>
        <select id="affCert">${ETAT.certifications.map(x =>
          `<option value="${x.id}">${esc(x.abrege || x.intitule)}</option>`).join('')}</select></div>
      <div class="field" style="min-width:170px"><label for="affTerr">Territoire</label>
        <select id="affTerr"><option value="">Indifférent</option>${
          [...new Set(ETAT.intervenants.map(i => i.region).filter(Boolean))].sort()
            .map(r => `<option>${esc(r)}</option>`).join('')}</select></div>
      <div class="field" style="min-width:150px"><label for="affMod">Modalité</label>
        <select id="affMod"><option value="">Indifférente</option><option value="true">Distanciel</option>
        <option value="false">Présentiel</option></select></div>
    </div>
    <div id="affOut">${chargement()}</div>
    ${aSavoir(['Score : habilitation 50 · spécialité 30 · disponibilité 30 · expérience 15 · territoire 15 · modalité 5',
      ['Capacités cibles', 'regles']], true)}`, 'proposition classée')}`;

  const lancer = async () => {
    const p = new URLSearchParams({ certificationId: $('#affCert').value });
    if ($('#affTerr').value) p.set('territoire', $('#affTerr').value);
    if ($('#affMod').value) p.set('distanciel', $('#affMod').value);

    $('#affOut').innerHTML = chargement();
    try {
      const props = await API.get('/intervenants/proposer?' + p);
      $('#affOut').innerHTML = props.length ? `<div class="tw"><table class="t">
        <thead><tr><th class="n">Score</th><th>Intervenant</th><th>Motifs</th><th style="width:140px">Charge</th></tr></thead>
        <tbody>${props.map(x => `<tr>
          <td class="n"><b style="color:${x.score >= 60 ? 'var(--ok)' : x.score >= 35 ? 'var(--warn)' : 'var(--faint)'}">${x.score}</b></td>
          <td>${who(x.nom, x.region || x.territoire, true)}</td>
          <td style="font-size:11.5px;color:var(--ink-2)">${esc(x.motifs.join(' · '))}</td>
          <td>${jauge(x.tauxCharge, x.candidatsActifs + ' / ' + x.capacite)}</td></tr>`).join('')}
        </tbody></table></div>` : vide('Aucun intervenant mobilisable pour cette certification.');
    } catch (e) { $('#affOut').innerHTML = `<div class="banner">${esc(e.message)}</div>`; }
  };

  ['affCert', 'affTerr', 'affMod'].forEach(i => $('#' + i).onchange = lancer);
  lancer();
};

function jauge(taux, libelle) {
  const cls = taux > 1 ? 'c' : taux > .8 ? 'w' : 'o';
  return `<div style="display:flex;align-items:center;gap:8px">
    <span class="gauge" style="flex:1"><i class="${cls}" style="width:${Math.min(100, (taux || 0) * 100)}%"></i></span>
    <span class="num" style="font-size:11px;color:var(--muted);min-width:44px;text-align:right">${esc(libelle)}</span></div>`;
}

/* ============================== HEURES ============================== */
VUES.heures = async function () {
  const h = await API.get('/pilotage/heures' + paramsFiltres());
  majFiltreCount(h.lignes.length);

  const total = h.totalPrescrites || 1;

  $('#vue').innerHTML = `
  <div class="kpi-grid">
    ${kpi('Heures prescrites', nf(h.totalPrescrites) + '<small> h</small>', h.lignes.length + ' parcours valorisés', 'brand')}
    ${kpi('Heures réalisées', nf(h.totalRealisees) + '<small> h</small>', ratio(h.totalRealisees, h.totalPrescrites) + ' de consommation')}
    ${kpi('Heures restantes', nf(Math.max(0, h.totalPrescrites - h.totalRealisees)) + '<small> h</small>', 'à planifier ou à libérer')}
    ${kpi('Enveloppes ≥ 85 %', h.lignes.filter(l => l.taux >= .85).length, 'arbitrage à prévoir',
      h.lignes.filter(l => l.taux >= .85).length ? 'warn' : 'ok')}
    ${kpi('Dépassements de plafond',
      h.lignes.filter(l => l.depassementIndividuel || l.depassementCollectif || l.depassementComplement).length,
      'au-delà des maxima de la grille', 'crit')}
  </div>

  <div class="grid" style="grid-template-columns:repeat(auto-fit,minmax(300px,1fr));margin-top:14px">
    ${carte("Répartition par nature d'heures", `
      <div class="stack" style="height:14px">
        <i style="width:${h.totalIndividuel / total * 100}%;background:var(--teal)"></i>
        <i style="width:${h.totalCollectif / total * 100}%;background:var(--cyan)"></i>
        <i style="width:${h.totalComplement / total * 100}%;background:var(--amber)"></i>
      </div>
      <div class="legend">
        <span><i style="background:var(--teal)"></i>Individuel <b class="num">${nf(h.totalIndividuel)} h</b></span>
        <span><i style="background:var(--cyan)"></i>Collectif <b class="num">${nf(h.totalCollectif)} h</b></span>
        <span><i style="background:var(--amber)"></i>Compléments formatifs <b class="num">${nf(h.totalComplement)} h</b></span>
      </div>`, 'sur ' + nf(h.totalPrescrites) + ' h prescrites')}

    ${carte('Usage des plafonds', barres([
      { l: 'Individuel — moyenne', v: h.totalIndividuel / (h.lignes.length || 1), repere: h.plafonds.individuel,
        n: nf(h.totalIndividuel / (h.lignes.length || 1)) + ' / ' + h.plafonds.individuel + ' h' },
      { l: 'Collectif — moyenne', v: h.totalCollectif / (h.lignes.length || 1), repere: h.plafonds.collectif,
        n: nf(h.totalCollectif / (h.lignes.length || 1)) + ' / ' + h.plafonds.collectif + ' h', cls: 'alt' },
      { l: 'Compléments — moyenne', v: h.totalComplement / (h.lignes.length || 1), repere: h.plafonds.complement,
        n: nf(h.totalComplement / (h.lignes.length || 1)) + ' / ' + h.plafonds.complement + ' h', cls: 'warn' },
    ], { large: true }), 'trait rouge = plafond')}
  </div>

  ${section('Consommation par parcours')}
  ${carte('', `<div class="tw"><table class="t">
    <thead><tr><th>Candidat</th><th>Étape</th><th class="n">Indiv.</th><th class="n">Collectif</th><th class="n">Compl.</th>
    <th class="n">Prescrit</th><th class="n">Réalisé</th><th class="n">Reste</th><th class="n">Séances</th>
    <th style="width:160px">Consommation</th></tr></thead>
    <tbody>${h.lignes.length ? h.lignes.map(l => `<tr class="clik" onclick="ouvrirFiche(${l.id})">
      <td>${who(l.candidat, l.certification, true)}</td>
      <td><span class="chip c-brand">${esc(l.etape)}</span></td>
      <td class="n"${l.depassementIndividuel ? ' style="color:var(--crit);font-weight:600"' : ''}>${nf(l.individuel)}</td>
      <td class="n"${l.depassementCollectif ? ' style="color:var(--crit);font-weight:600"' : ''}>${nf(l.collectif)}</td>
      <td class="n"${l.depassementComplement ? ' style="color:var(--crit);font-weight:600"' : ''}>${nf(l.complement)}</td>
      <td class="n"><b>${nf(l.prescrites)}</b></td><td class="n">${nf(l.realisees)}</td>
      <td class="n">${nf(l.restantes)}</td><td class="n">${l.seances}</td>
      <td>${jauge(l.taux, pct(l.taux))}</td></tr>`).join('')
      : '<tr><td colspan="10">' + vide('Aucun parcours avec heures prescrites.') + '</td></tr>'}
    </tbody></table></div>`, h.lignes.length + ' parcours', 'tight')}`;
};

/* ============================== QUALITÉ ============================== */
VUES.qualite = async function () {
  const q = await API.get('/pilotage/qualite' + paramsFiltres());
  majFiltreCount(q.dossiers);

  const r = q.resultats;

  $('#vue').innerHTML = `
  <div class="kpi-grid">
    ${kpi('Dossiers complets', q.complets, ratio(q.complets, q.dossiers) + ' du portefeuille',
      q.complets / (q.dossiers || 1) > .7 ? 'ok' : 'warn')}
    ${kpi('Complétude moyenne', pct(q.completudeMoyenne), 'sur ' + q.controles.length + ' points de contrôle')}
    ${kpi('Risque de non-conformité', q.aRisque, 'au moins 2 contrôles critiques manquants', q.aRisque ? 'crit' : 'ok')}
    ${kpi('Taux de validation totale', ratio(r.validationTotale, r.juryPasses),
      r.validationTotale + ' réussites sur ' + r.juryPasses + ' jurys', 'ok')}
    ${kpi('Résultats non renseignés', r.nonRenseigne, 'jurys passés sans résultat saisi',
      r.nonRenseigne > r.validationTotale ? 'crit' : 'warn')}
  </div>

  <div class="grid" style="grid-template-columns:repeat(auto-fit,minmax(320px,1fr));margin-top:14px">
    ${carte('Points de contrôle documentaire', barres(q.controles.map(c => ({
      l: (c.critique ? '● ' : '○ ') + c.libelle, v: c.satisfaits, n: c.satisfaits + ' / ' + c.total,
      cls: c.satisfaits / (c.total || 1) > .8 ? 'ok' : c.satisfaits / (c.total || 1) > .5 ? 'warn' : 'crit',
    })), { large: true }) + '<p style="font-size:11px;color:var(--faint);margin-top:10px">● contrôle critique · ○ contrôle secondaire</p>',
      'portefeuille actif — ' + q.dossiers + ' dossiers')}

    ${carte('Résultats et sorties', barres([
      { l: 'Validation totale', v: r.validationTotale, cls: 'ok' },
      { l: 'Validation partielle', v: r.validationPartielle, cls: 'warn' },
      { l: 'Refus', v: r.refus, cls: 'crit' },
      { l: 'Résultat non renseigné', v: r.nonRenseigne, cls: 'crit' },
      ...q.sorties.map(s => ({ l: s.motif, v: s.nombre, cls: 'warn' })),
    ].filter(x => x.v > 0), { large: true }), 'sur le périmètre filtré')}
  </div>

  ${section('Dossiers à sécuriser en priorité')}
  ${carte('', q.dossiersARisque.length ? `<div class="tw"><table class="t">
    <thead><tr><th>Candidat</th><th>Étape</th><th>Contrôles manquants</th><th style="width:150px">Complétude</th></tr></thead>
    <tbody>${q.dossiersARisque.map(d => `<tr class="clik" onclick="ouvrirFiche(${d.id})">
      <td>${who(d.candidat, d.certification, true)}</td>
      <td><span class="chip c-brand">${esc(d.etape)}</span></td>
      <td style="font-size:11.5px">${d.manquants.map(m =>
        `<span class="chip c-${m.critique ? 'crit' : 'neutral'}" style="margin:1px 3px 1px 0">${esc(m.libelle)}</span>`).join('')}</td>
      <td>${jauge(d.completude, pct(d.completude))}</td></tr>`).join('')}
    </tbody></table></div>` : vide('Aucun dossier à risque sur ce périmètre.'), '', 'tight')}`;
};

/* ============================== FINANCE ============================== */
VUES.finance = async function () {
  const f = await API.get('/pilotage/finance' + paramsFiltres());
  majFiltreCount(f.lignes.length);

  $('#vue').innerHTML = `
  <div class="kpi-grid">
    ${kpi('CA prévisionnel', eurK(f.caPrevisionnel), f.lignes.length + ' parcours actifs valorisés', 'brand')}
    ${kpi('CA facturé', eurK(f.caFacture), f.nombreFactures + ' factures émises')}
    ${kpi('CA encaissé', eurK(f.caEncaisse), ratio(f.caEncaisse, f.caFacture) + ' du facturé', 'ok')}
    ${kpi("En attente d'encaissement", eurK(f.enAttente), f.enAttente > 0 ? 'à relancer' : 'à jour',
      f.enAttente > 5000 ? 'warn' : 'ok')}
    ${kpi('Marge prévisionnelle', eurK(f.margePrevisionnelle), ratio(f.margePrevisionnelle, f.caPrevisionnel) + ' du CA', 'brand')}
    ${kpi('Panier moyen facturé', eur(f.panierMoyen), 'par facture émise')}
  </div>

  ${section('Répartition par grille tarifaire')}
  ${carte('', `<div class="tw"><table class="t">
    <thead><tr><th>Grille</th><th class="n">Dossiers actifs</th><th class="n">CA prévisionnel</th><th class="n">Marge</th><th class="n">Taux</th></tr></thead>
    <tbody>${f.parGrille.map(g => `<tr>
      <td><span class="chip c-brand">${esc(g.grille)}</span></td>
      <td class="n">${g.dossiers}</td><td class="n"><b>${eur(g.montant)}</b></td>
      <td class="n" style="color:var(--ok)">${eur(g.marge)}</td><td class="n">${ratio(g.marge, g.montant)}</td>
    </tr>`).join('')}</tbody></table></div>`, '', 'tight')}

  <div class="grid" style="grid-template-columns:repeat(auto-fit,minmax(320px,1fr));margin-top:14px">
    ${carte('CA facturé par financeur', barres(f.parFinanceur.map(x => ({
      l: x.financeur, v: x.montant, n: eur(x.montant) + ' · ' + x.nombre + ' fact.',
    })), { large: true }), 'sur le facturé réel')}
    ${carte('CA prévisionnel par certification', barres(f.parCertification.slice(0, 12).map(x => ({
      l: x.certification, v: x.montant, n: eur(x.montant), cls: 'alt',
    })), { large: true }), 'portefeuille actif')}
  </div>

  ${section('Simulateur de parcours')}
  ${carte('', `
    <div class="sim">
      <label>Date de démarrage <input type="date" id="sD" value="${new Date().toISOString().slice(0, 10)}"></label>
      <label>Forfait architecture <input type="number" id="sF" value="1" min="0" max="1"></label>
      <label>Heures individuelles <input type="number" id="sI" value="20" min="0" step="0.5"></label>
      <label>Heures collectives <input type="number" id="sC" value="2" min="0" step="0.5"></label>
      <label>Compléments formatifs <input type="number" id="sK" value="0" min="0" step="0.5"></label>
      <label>Frais de jury <input type="number" id="sJ" value="1" min="0" max="1"></label>
      <label>Participants <input type="number" id="sP" value="1" min="1" max="15"></label>
    </div>
    <div class="presets">
      <button class="btn" data-p="20,2,0,1">Parcours standard</button>
      <button class="btn" data-p="24,4,10,1">Parcours renforcé</button>
      <button class="btn" data-p="36,4,21,1">Parcours intensif</button>
      <button class="btn" data-p="6,20,8,3">Cohorte collective — TP IEPE (3 candidates)</button>
    </div>
    <div class="simout" id="simOut">${chargement()}</div>
    ${aSavoir([['Grilles tarifaires', 'regles']], true)}`)}

  ${section('Rentabilité par parcours actif')}
  ${carte('', `<div class="tw"><table class="t">
    <thead><tr><th>Candidat</th><th>Certification</th><th>Grille</th><th class="n">Forfait</th><th class="n">Individuel</th>
    <th class="n">Collectif</th><th class="n">Compl.</th><th class="n">Jury</th><th class="n">CA</th>
    <th class="n">Coût</th><th class="n">Marge</th><th class="n">Taux</th></tr></thead>
    <tbody>${f.lignes.map(l => `<tr class="clik" onclick="ouvrirFiche(${l.id})">
      <td>${who(l.candidat, '', true)}</td>
      <td style="font-size:11.5px">${esc(l.certification || '—')}</td>
      <td><span class="chip c-neutral">${esc(l.grille)}</span></td>
      <td class="n">${l.forfait ? eur(l.forfait) : '—'}</td>
      <td class="n">${l.montantIndividuel ? eur(l.montantIndividuel) : '—'}</td>
      <td class="n">${l.montantCollectif ? eur(l.montantCollectif) : '—'}</td>
      <td class="n">${l.montantComplementFormatif ? eur(l.montantComplementFormatif) : '—'}</td>
      <td class="n">${l.fraisJury ? eur(l.fraisJury) : '—'}</td>
      <td class="n"><b>${eur(l.total)}</b></td>
      <td class="n" style="color:var(--muted)">${eur(l.coutPedagogique)}</td>
      <td class="n" style="color:var(--ok);font-weight:600">${eur(l.marge)}</td>
      <td class="n">${pct(l.tauxMarge)}</td></tr>`).join('')}
    </tbody></table></div>`, f.lignes.length + ' parcours valorisés', 'tight')}`;

  const simuler = async () => {
    const g = id => $('#' + id).value;
    const p = new URLSearchParams({
      dateDebut: g('sD'),
      forfait: Number(g('sF')) > 0, individuel: g('sI'), collectif: g('sC'),
      complement: g('sK'), jury: Number(g('sJ')) > 0, participants: g('sP'),
    });

    try {
      const r = await API.get('/pilotage/simuler?' + p);
      $('#simOut').innerHTML = `
        <div><div class="l">Grille appliquée</div><div class="v" style="font-size:17px;color:var(--teal)">${esc(r.grille.code)}</div></div>
        <div><div class="l">CA total</div><div class="v" style="color:var(--teal)">${eur(r.total)}</div></div>
        <div><div class="l">Par candidat</div><div class="v">${eur(r.parCandidat)}</div></div>
        <div><div class="l">Coût pédagogique</div><div class="v" style="color:var(--muted)">${eur(r.coutPedagogique)}</div></div>
        <div><div class="l">Marge brute</div><div class="v" style="color:var(--ok)">${eur(r.marge)}</div></div>
        <div><div class="l">CA / heure mobilisée</div><div class="v">${eur(r.caParHeureMobilisee)}</div></div>
        <div><div class="l">Plafonds</div><div class="v" style="font-size:14px;color:${r.plafondRespecte ? 'var(--ok)' : 'var(--crit)'}">${r.plafondRespecte ? 'respectés' : 'dépassés'}</div></div>`;

      if (!r.plafondRespecte) {
        $('#simOut').insertAdjacentHTML('afterend',
          `<div class="banner warn" id="simAlerte" style="margin-top:11px">${r.depassements.map(esc).join('<br>')}</div>`);
      } else {
        $('#simAlerte')?.remove();
      }
    } catch (e) { $('#simOut').innerHTML = `<div style="padding:12px">${esc(e.message)}</div>`; }
  };

  ['sD', 'sF', 'sI', 'sC', 'sK', 'sJ', 'sP'].forEach(id => $('#' + id).oninput = simuler);
  $$('#vue [data-p]').forEach(b => b.onclick = () => {
    const [i, c, k, p] = b.dataset.p.split(',').map(Number);
    $('#sI').value = i; $('#sC').value = c; $('#sK').value = k; $('#sP').value = p;
    $('#sF').value = 1; $('#sJ').value = 1;
    simuler();
  });
  simuler();
};

/* ============================== NOTIFICATIONS ============================== */
let FILTRE_NOTIF = { destinataire: '', nonLues: false };

VUES.notifications = async function () {
  const [destinataires, etat] = await Promise.all([
    API.get('/notifications/destinataires'), API.get('/notifications/etat'),
  ]);
  const q = new URLSearchParams();
  if (FILTRE_NOTIF.destinataire) q.set('destinataire', FILTRE_NOTIF.destinataire);
  if (FILTRE_NOTIF.nonLues) q.set('nonLues', 'true');
  const liste = await API.get('/notifications?' + q);

  construireNav(null, etat.nonLues);
  $('#vActions').innerHTML = etat.nonLues
    ? `<button class="btn" onclick="toutLire()">Tout marquer comme lu</button>` : '';

  $('#vue').innerHTML = `
  ${aSavoir([
    ['Choisir qui est notifié', null, "aller('regles').then(()=>document.getElementById('droits')?.scrollIntoView())"],
    etat.envoiCourriel ? 'Envoi par courriel actif' : 'Envoi par courriel inactif : aucun serveur SMTP configuré',
  ])}
  <div class="kpi-grid">
    ${kpi('Non lues', etat.nonLues, 'tous destinataires confondus', etat.nonLues ? 'brand' : 'ok')}
    ${kpi('Destinataires', destinataires.length, 'personnes ou rôles notifiés')}
    ${kpi('Courriels en attente', etat.enAttenteEnvoi, etat.envoiCourriel ? 'envoi chaque minute' : 'envoi inactif',
      etat.enAttenteEnvoi ? 'warn' : '')}
  </div>
  <div style="display:flex;gap:8px;flex-wrap:wrap;align-items:center;margin:16px 0 12px">
    <label class="eyebrow" for="nDest">Destinataire</label>
    <select class="f" id="nDest"><option value="">Tous</option>${destinataires.map(d =>
      `<option value="${esc(d.nom)}"${d.nom === FILTRE_NOTIF.destinataire ? ' selected' : ''}>${esc(d.nom)}${d.nonLues ? ` (${d.nonLues})` : ''}</option>`).join('')}</select>
    <label class="check" style="font-size:12px"><input type="checkbox" id="nNonLues"${FILTRE_NOTIF.nonLues ? ' checked' : ''}> Non lues seulement</label>
  </div>
  ${carte('', liste.length ? liste.map(n => `
    <div class="notif${n.lueLe ? '' : ' non-lue'}">
      <span class="pastille"></span>
      <div class="corps">
        <div class="titre">${esc(n.titre)}</div>
        <div class="message">${esc(n.message)}</div>
        <div class="meta">
          <span class="chip c-neutral">${esc(n.evenementLibelle)}</span>
          <span>à ${esc(n.destinataireNom)}${n.destinataireEmail ? ' · ' + esc(n.destinataireEmail) : ''}</span>
          <span>${new Date(n.creeLe).toLocaleString('fr-FR', { dateStyle: 'short', timeStyle: 'short' })}</span>
          ${n.emailEnvoyeLe ? '<span>courriel envoyé</span>' : n.erreurEmail ? `<span style="color:var(--crit)">courriel non envoyé : ${esc(n.erreurEmail)}</span>` : ''}
        </div>
      </div>
      <div class="actions-ligne" style="align-self:center">
        ${n.lien && !n.lien.startsWith('/#') ? `<a class="btn primary mini" href="${esc(n.lien)}" target="_blank" rel="noopener">${esc(n.libelleLien || 'Ouvrir')}</a>` : ''}
        ${n.parcoursId ? `<button class="btn mini" onclick="lireNotification(${n.id}, ${n.parcoursId})">Ouvrir le dossier</button>` : ''}
        ${n.lueLe ? '' : `<button class="btn ghost mini" onclick="lireNotification(${n.id})">Marquer lu</button>`}
      </div>
    </div>`).join('') : vide('Aucune notification.'), liste.length + ' notification' + (liste.length > 1 ? 's' : ''), 'tight')}`;

  $('#nDest').onchange = e => { FILTRE_NOTIF.destinataire = e.target.value; rafraichir(); };
  $('#nNonLues').onchange = e => { FILTRE_NOTIF.nonLues = e.target.checked; rafraichir(); };
};

async function lireNotification(id, parcoursId) {
  try { await API.post(`/notifications/${id}/lue`); } catch { /* sans conséquence */ }
  if (parcoursId) ouvrirFiche(parcoursId);
  if (VUE === 'notifications') rafraichir(); else majCompteurs();
}

async function toutLire() {
  const q = FILTRE_NOTIF.destinataire ? '?destinataire=' + encodeURIComponent(FILTRE_NOTIF.destinataire) : '';
  const r = await API.post('/notifications/tout-lire' + q);
  toast(`${r.marquees} notification${r.marquees > 1 ? 's' : ''} marquée${r.marquees > 1 ? 's' : ''} comme lue${r.marquees > 1 ? 's' : ''}.`, 'ok');
  rafraichir();
}
