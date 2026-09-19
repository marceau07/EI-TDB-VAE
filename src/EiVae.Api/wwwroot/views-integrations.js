/* =========================================================================
   RÉFÉRENTIEL CERTIFICATIONS, INTÉGRATIONS, VAE COLLECTIVE, RÈGLES DE GESTION
   ========================================================================= */

/* ============================== CERTIFICATIONS ============================== */
VUES.certifications = async function () {
  const [domaines] = await Promise.all([API.get('/certifications/domaines')]);

  $('#vActions').innerHTML = `
    <button class="btn" onclick="aller('integrations')">Synchroniser</button>
    <button class="btn primary" onclick="ajouterCertification()">Ajouter une certification</button>`;

  $('#vue').innerHTML = `
  <div id="certKpis"></div>
  <div style="display:flex;gap:8px;flex-wrap:wrap;align-items:center;margin:16px 0 12px">
    <label class="eyebrow" for="cDom">Domaine</label>
    <select class="f" id="cDom"><option value="">Tous</option>${
      domaines.utilises.map(d => `<option>${esc(d)}</option>`).join('')}</select>
    <label class="eyebrow" for="cNiv">Niveau</label>
    <select class="f" id="cNiv"><option value="">Tous</option>${
      [3, 4, 5, 6, 7].map(n => `<option value="${n}">Niveau ${n}</option>`).join('')}</select>
    <label class="eyebrow" for="cAct">Activité</label>
    <select class="f" id="cAct"><option value="">Toutes</option>
      <option value="true">Avec demandes</option><option value="false">Sans demande</option></select>
    <label class="check" style="font-size:12px"><input type="checkbox" id="cVae"> Voie VAE ouverte seulement</label>
    <input class="f" id="cQ" type="search" placeholder="Rechercher une certification…">
  </div>
  <div id="certTable">${chargement()}</div>
  ${aSavoir([['Synchroniser avec France Compétences', 'integrations'], ['Habiliter un intervenant', 'reseau']], true)}`;

  const dessiner = async () => {
    const p = new URLSearchParams();
    if ($('#cDom').value) p.set('domaine', $('#cDom').value);
    if ($('#cNiv').value) p.set('niveau', $('#cNiv').value);
    if ($('#cAct').value) p.set('avecDemandes', $('#cAct').value);
    if ($('#cVae').checked) p.set('vaeSeulement', 'true');
    if ($('#cQ').value) p.set('recherche', $('#cQ').value);

    $('#certTable').innerHTML = chargement();
    const liste = await API.get('/certifications?' + p);

    $('#certKpis').innerHTML = `<div class="kpi-grid">
      ${kpi('Certifications au catalogue', liste.length, 'sur le filtre courant', 'brand')}
      ${kpi('Avec demandes', liste.filter(c => c.demandes > 0).length, 'effectivement sollicitées')}
      ${kpi('Voie VAE ouverte', liste.filter(c => c.voieVaeOuverte).length, 'accessibles par la VAE', 'ok')}
      ${kpi('Sans intervenant habilité', liste.filter(c => c.actifs > 0 && !c.intervenantsHabilites).length,
        'demande active non couverte', 'crit')}
      ${kpi('Enregistrement expiré', liste.filter(c => !c.actifFranceCompetences).length,
        'fiche non publiée ou échue', 'warn')}
    </div>`;

    $('#certTable').innerHTML = carte('', `<div class="tw"><table class="t">
      <thead><tr><th class="n">RNCP</th><th>Intitulé</th><th class="n">Niv.</th><th>Domaine</th>
      <th>VAE</th><th class="n">Demandes</th><th class="n">Actifs</th><th class="n">Habilités</th>
      <th>Fiche</th></tr></thead>
      <tbody>${liste.length ? liste.map(c => `<tr class="clik" onclick="ficheCertification(${c.id})">
        <td class="n" style="color:var(--muted)">${esc(c.codeRncp)}</td>
        <td><b style="font-weight:600">${esc(c.intitule)}</b>${c.abrege ? `<br><span style="font-size:10.5px;color:var(--faint)">${esc(c.abrege)}</span>` : ''}</td>
        <td class="n">${c.niveau ?? '—'}</td>
        <td><span class="chip c-neutral">${esc(c.domaineEi || '—')}</span></td>
        <td>${c.voieVaeOuverte ? '<span class="chip c-ok">ouverte</span>' : '<span class="chip c-crit">fermée</span>'}</td>
        <td class="n">${c.demandes || '—'}</td><td class="n">${c.actifs || '—'}</td>
        <td class="n">${c.intervenantsHabilites
          ? `<span class="chip c-ok">${c.intervenantsHabilites}</span>`
          : (c.actifs ? '<span class="chip c-crit">0</span>' : '—')}</td>
        <td>${c.actifFranceCompetences ? '<span class="chip c-ok">active</span>'
          : `<span class="chip c-warn">${c.dateFinEnregistrement ? 'échue ' + fmtDate(c.dateFinEnregistrement) : 'non publiée'}</span>`}</td>
      </tr>`).join('') : '<tr><td colspan="9">' + vide('Aucune certification sur ce filtre.') + '</td></tr>'}
      </tbody></table></div>`, liste.length + ' certifications', 'tight');
  };

  ['cDom', 'cNiv', 'cAct', 'cVae'].forEach(i => $('#' + i).onchange = dessiner);
  let t; $('#cQ').oninput = () => { clearTimeout(t); t = setTimeout(dessiner, 320); };
  dessiner();
};

async function ficheCertification(id) {
  ouvrirTiroir(chargement('Ouverture de la fiche…'));
  const c = await API.get('/certifications/' + id);

  const kv = lignes => `<dl class="kv">${lignes.filter(Boolean).map(([k, v, brut]) =>
    `<dt>${esc(k)}</dt><dd>${brut ? v : esc(v == null || v === '' ? '—' : v)}</dd>`).join('')}</dl>`;

  const voies = Object.entries(c.vae.autresVoies)
    .filter(([, ouvert]) => ouvert)
    .map(([k]) => ({
      formationInitiale: 'Formation initiale', formationContinue: 'Formation continue',
      apprentissage: 'Apprentissage', contratProfessionnalisation: 'Contrat de professionnalisation',
      candidatLibre: 'Candidat libre',
    }[k]));

  ouvrirTiroir(`
  <div class="dr-h">
    <span class="av" style="background:${avat(c.intitule)};width:40px;height:40px;font-size:11px">${esc(c.niveau ? 'N' + c.niveau : '?')}</span>
    <div style="flex:1;min-width:0">
      <h2>${esc(c.intitule)}</h2>
      <div class="s">${esc(c.codeRncp)}${c.abrege ? ' · ' + esc(c.abrege) : ''} · ${esc(c.libelleNiveau || '')}</div>
      <div style="margin-top:7px;display:flex;gap:6px;flex-wrap:wrap">
        ${c.vae.ouverte ? '<span class="chip c-ok">VAE ouverte</span>' : '<span class="chip c-crit">VAE fermée</span>'}
        ${c.actifFranceCompetences ? '<span class="chip c-ok">fiche active</span>' : '<span class="chip c-warn">enregistrement échu</span>'}
        <span class="chip c-neutral">${esc(c.domaineEi || 'domaine non classé')}</span>
      </div>
    </div>
    <button class="btn ghost" onclick="fermerTiroir()" aria-label="Fermer">✕</button>
  </div>
  <div class="dr-b">
    <div class="dr-sec" style="display:flex;gap:8px;flex-wrap:wrap;align-items:center">
      <button class="btn primary" onclick="modifierCertification(${c.id})">Champs internes</button>
      ${lienExterne(c.lien, 'Fiche France Compétences')}
    </div>

    <div class="dr-sec"><span class="eyebrow">Identité réglementaire</span>
      ${kv([
        ['Code RNCP', c.codeRncp], ['Niveau', c.libelleNiveau], ['Type', c.typeEnregistrement],
        ['État de la fiche', c.etatFiche],
        ['Décision', fmtDateLongue(c.dates.dateDecision)],
        ['Fin d\'enregistrement', fmtDateLongue(c.dates.dateFinEnregistrement)],
        ['Limite de délivrance', fmtDateLongue(c.dates.dateLimiteDelivrance)],
        ['Dernière synchronisation', c.derniereSynchronisation ? fmtDateLongue(c.derniereSynchronisation.slice(0, 10)) : '—'],
      ])}
    </div>

    <div class="dr-sec"><span class="eyebrow">Modalités de VAE</span>
      ${kv([
        ['Voie VAE', c.vae.ouverte ? '<span class="chip c-ok">ouverte</span>' : '<span class="chip c-crit">fermée</span>', true],
        ['Autres voies d\'accès', voies.length ? voies.join(', ') : '—'],
      ])}
      ${c.vae.compositionJury ? `<div style="margin-top:10px">
        <span class="eyebrow">Composition du jury</span>
        <p style="font-size:12px;color:var(--ink-2);line-height:1.55;margin-top:6px;white-space:pre-line">${esc(c.vae.compositionJury)}</p>
      </div>` : ''}
    </div>

    <div class="dr-sec"><span class="eyebrow">Certificateur &amp; contact</span>
      ${c.certificateurs.length ? c.certificateurs.map(x => `<div style="padding:6px 0;border-top:1px solid var(--line)">
        <b style="font-size:12.5px;font-weight:600">${esc(x.nom)}</b>${x.estPrincipal ? ' <span class="chip c-brand">principal</span>' : ''}
        <div style="font-size:11px;color:var(--faint);font-family:var(--f-data)">SIRET ${esc(x.siret || 'non renseigné')} · ${esc(x.etat || '')}</div>
      </div>`).join('') : vide('Aucun certificateur renseigné.')}
      ${kv([
        ['Contact opérationnel', c.contact.nom],
        ['Courriel', c.contact.email], ['Téléphone', c.contact.telephone],
      ])}
    </div>

    <div class="dr-sec"><span class="eyebrow">Blocs de compétences (${c.blocs.length})</span>
      <div style="display:flex;flex-direction:column;gap:8px;margin-top:8px">
        ${c.blocs.length ? c.blocs.map(b => `<div class="bloc">
          <span class="code">${esc(b.code)}</span>
          <h4>${esc(b.libelle)}</h4>
          ${b.competences ? `<details><summary>Compétences évaluées</summary><div class="txt">${esc(b.competences)}</div></details>` : ''}
          ${b.modalitesEvaluation ? `<details><summary>Modalités d'évaluation</summary><div class="txt">${esc(b.modalitesEvaluation)}</div></details>` : ''}
        </div>`).join('') : vide('Aucun bloc publié pour cette certification.')}
      </div>
    </div>

    <div class="dr-sec"><span class="eyebrow">Réseau habilité</span>
      ${blocRéseau('AAP compétents', c.aapCompetents)}
      ${blocRéseau('Accompagnateurs habilités', c.accompagnateurs)}
      ${blocRéseau('Experts métiers', c.expertsMetier)}
      ${!c.aapCompetents.length && !c.accompagnateurs.length
        ? `<div class="banner warn" style="margin-top:10px">Aucun intervenant habilité.</div>
           <button class="btn" style="margin-top:8px" onclick="fermerTiroir();aller('reseau')">Habiliter un intervenant</button>` : ''}
    </div>

    <div class="dr-sec"><span class="eyebrow">Modules EI Académie</span>
      ${c.modules.length ? c.modules.map(m => `<div style="padding:5px 0;font-size:12.5px;display:flex;gap:8px;align-items:center">
        <span class="chip c-${m.obligatoire ? 'brand' : 'neutral'}">${esc(m.type)}</span>
        <span>${esc(m.titre)}</span>
        <span style="margin-left:auto;color:var(--faint);font-family:var(--f-data);font-size:11px">${m.dureeHeures ? nf(m.dureeHeures) + ' h' : ''}</span>
      </div>`).join('') : vide('Aucun module associé.')}
    </div>

    <div class="dr-sec"><span class="eyebrow">Paramètres internes EI Groupe</span>
      ${kv([
        ['Abrégé du service', c.abrege],
        ['Domaine', c.domaineEi],
        ['Statut interne', c.statutInterne],
        ['Durée habituelle', c.dureeHabituelleJours ? Math.round(c.dureeHabituelleJours / 30) + ' mois' : '—'],
      ])}
      ${c.particularites ? `<p style="font-size:12px;color:var(--ink-2);line-height:1.55;margin-top:8px">${esc(c.particularites)}</p>` : ''}
    </div>

    <div class="dr-sec"><span class="eyebrow">Activité EI Groupe</span>
      ${kv([
        ['Demandes reçues', c.activite.demandes], ['Dossiers actifs', c.activite.actifs],
        ['Clôturés', c.activite.clotures], ['Sorties', c.activite.sorties],
      ])}
    </div>

    ${c.statistiques?.length ? `<div class="dr-sec"><span class="eyebrow">Statistiques nationales</span>
      <table class="t"><thead><tr><th>Année</th><th class="n">Certifiés</th><th class="n">Dont VAE</th>
      <th class="n">Insertion 6 mois</th></tr></thead><tbody>
      ${c.statistiques.map(s => `<tr><td class="num">${esc(s.annee)}</td><td class="n">${esc(s.certifies || '—')}</td>
      <td class="n">${esc(s.certifiesVae || '—')}</td><td class="n">${s.insertionGlobale6Mois ? esc(s.insertionGlobale6Mois) + ' %' : '—'}</td></tr>`).join('')}
      </tbody></table></div>` : ''}

    ${c.fiche.activitesVisees ? `<div class="dr-sec"><span class="eyebrow">Activités visées</span>
      <p style="font-size:12px;color:var(--ink-2);line-height:1.6;white-space:pre-line">${esc(c.fiche.activitesVisees)}</p></div>` : ''}
    ${c.fiche.prerequis ? `<div class="dr-sec"><span class="eyebrow">Prérequis</span>
      <p style="font-size:12px;color:var(--ink-2);line-height:1.6;white-space:pre-line">${esc(c.fiche.prerequis)}</p></div>` : ''}
  </div>`);
}

function blocRéseau(titre, liste) {
  return `<div style="margin-bottom:10px"><div style="font-size:11.5px;color:var(--muted);margin-bottom:5px">${esc(titre)}</div>
    ${liste.length ? `<div style="display:flex;gap:6px;flex-wrap:wrap">${liste.map(x =>
      `<span class="chip c-${x.niveau === 'Habilité' ? 'ok' : 'warn'}">${esc(x.nom)}</span>`).join('')}</div>`
      : '<span style="font-size:12px;color:var(--faint)">aucun</span>'}</div>`;
}

async function modifierCertification(id) {
  const [c, modules, domaines] = await Promise.all([
    API.get('/certifications/' + id), API.get('/modules-academie'), API.get('/certifications/domaines'),
  ]);
  const retenus = new Set(c.modules.map(m => m.id));

  ouvrirModale('Champs internes', `${c.codeRncp} — ${c.intitule}`, `
    <form id="frmCert" onsubmit="return false">
      <div class="form-grid">
        ${champ({ nom: 'abrege', label: 'Abrégé du service', valeur: c.abrege, exemple: 'TP FPA, DE EJE…' })}
        ${champ({ nom: 'domaineEi', label: 'Domaine EI Groupe', type: 'select', valeur: c.domaineEi,
          options: domaines.disponibles, vide: '—' })}
        ${champ({ nom: 'statutInterne', label: 'Statut interne', type: 'select', vide: false, valeur: c.statutInterne,
          options: [{ code: 'Active', libelle: 'Active' }, { code: 'Suspendue', libelle: 'Suspendue' },
            { code: 'NonCouverte', libelle: 'Non couverte' }] })}
        ${champ({ nom: 'dureeHabituelleJours', label: 'Durée habituelle (jours)', type: 'number', min: 0,
          valeur: c.dureeHabituelleJours, aide: '240 jours = 8 mois' })}
        ${champ({ nom: 'contactCertificateurNom', label: 'Contact certificateur', valeur: c.contact.nom })}
        ${champ({ nom: 'contactCertificateurEmail', label: 'Courriel du contact', type: 'email', valeur: c.contact.email })}
        ${champ({ nom: 'contactCertificateurTelephone', label: 'Téléphone du contact', valeur: c.contact.telephone })}
        ${champ({ nom: 'particularites', label: 'Particularités', type: 'textarea', valeur: c.particularites, span: true,
          exemple: 'Stage obligatoire, habilitation AFGSU, prérequis métier…' })}
      </div>
      <fieldset class="fieldset"><legend>Modules EI Académie associés</legend>
        <div class="picker" id="pickerModules">
          ${modules.length ? modules.map(m => `<label>
            <input type="checkbox" value="${m.id}"${retenus.has(m.id) ? ' checked' : ''}>
            <span>${esc(m.titre)}</span><span class="code">${esc(m.type)}</span></label>`).join('')
            : '<div style="padding:12px;font-size:12px;color:var(--faint)">Aucun module au catalogue EI Académie.</div>'}
        </div>
      </fieldset>
      <div class="form-actions"><span class="grow"></span>
        <button class="btn" onclick="fermerModale()">Annuler</button>
        <button class="btn primary" id="btnCert">Enregistrer</button></div>
    </form>`);

  $('#btnCert').onclick = async () => {
    const d = lireFormulaire($('#frmCert'));
    d.modulesAcademie = $$('#pickerModules input:checked').map(x => Number(x.value));
    try {
      await API.put(`/certifications/${id}/interne`, d);
      fermerModale();
      toast('Certification mise à jour.', 'ok');
      ETAT.certifications = await API.get('/certifications?avecDemandes=true');
      ficheCertification(id);
    } catch (e) { toast(e.message, 'err', 'Enregistrement impossible'); }
  };
}

function ajouterCertification() {
  ouvrirModale('Ajouter une certification', null, `
    <form id="frmNewCert" onsubmit="return false">
      <div class="form-grid">
        ${champ({ nom: 'codeRncp', label: 'Code RNCP', requis: true, exemple: '37275 ou RNCP37275' })}
        ${champ({ nom: 'abrege', label: 'Abrégé du service', exemple: 'TP FPA' })}
      </div>
      <div class="form-actions"><span class="grow"></span>
        <button class="btn" onclick="fermerModale()">Annuler</button>
        <button class="btn primary" id="btnNewCert">Ajouter</button></div>
    </form>`);

  $('#btnNewCert').onclick = async () => {
    const d = lireFormulaire($('#frmNewCert'));
    if (!d.codeRncp) { marquerErreurs($('#frmNewCert'), { codeRncp: 'Indiquez un code RNCP.' }); return; }
    try {
      const r = await API.post('/certifications', d);
      fermerModale();
      toast(r.message, 'ok', r.codeRncp + ' ajoutée');
      rafraichir();
    } catch (e) { toast(e.message, 'err', 'Ajout impossible'); }
  };
}

/* ============================== INTÉGRATIONS ============================== */
VUES.integrations = async function () {
  const [fc, fvae, sp, imports, demandes] = await Promise.all([
    API.get('/integrations/france-competences/etat'),
    API.get('/integrations/france-vae/etat'),
    API.get('/referentiel/sharepoint'),
    API.get('/integrations/imports'),
    API.get('/integrations/demandes-web?statut=Nouvelle'),
  ]);

  $('#vue').innerHTML = `
  ${section('France Compétences')}
  ${carte('', `
    <div class="kpi-grid" style="margin-bottom:14px">
      ${kpi('Certifications en base', fc.certificationsEnBase, fc.certificationsSynchronisees + ' synchronisées', 'brand')}
      ${kpi('Blocs de compétences', fc.blocsEnBase, 'publiés par les certificateurs')}
      ${kpi('Dernier import', fc.dernierImport ? fmtDate(fc.dernierImport.demarreLe.slice(0, 10)) : 'jamais',
        fc.dernierImport ? fc.dernierImport.statut : 'aucune synchronisation', fc.dernierImport ? 'ok' : 'warn')}
      ${kpi('Synchronisation auto.', fc.synchronisationAutomatique ? 'activée' : 'désactivée',
        fc.synchronisationAutomatique ? 'chaque jour à ' + fc.heure : 'déclenchement manuel',
        fc.synchronisationAutomatique ? 'ok' : 'warn')}
    </div>
    ${fc.erreur ? `<div class="banner">Source inaccessible : ${esc(fc.erreur)}</div>` : ''}
    <dl class="kv" style="font-size:12.5px">
      <dt>Source</dt><dd>${esc(fc.source)}</dd>
      <dt>Accès</dt><dd>${esc(fc.acces)}</dd>
      <dt>Export disponible</dt><dd>${esc(fc.exportDisponible || 'non détecté')}</dd>
    </dl>
    <div style="display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:14px;margin-top:14px">
      <div><span class="eyebrow">Champs synchronisés — écrasés à chaque import</span>
        <div style="display:flex;gap:5px;flex-wrap:wrap;margin-top:7px">${fc.champsSynchronises.map(x =>
          `<span class="chip c-info">${esc(x)}</span>`).join('')}</div></div>
      <div><span class="eyebrow">Champs internes — jamais touchés</span>
        <div style="display:flex;gap:5px;flex-wrap:wrap;margin-top:7px">${fc.champsInternes.map(x =>
          `<span class="chip c-ok">${esc(x)}</span>`).join('')}</div></div>
    </div>
    <div class="form-actions">
      <button class="btn primary" id="btnSyncFc">Synchroniser maintenant</button>
    </div>
    <div id="sortieSync"></div>`)}

  ${section('France VAE')}
  ${carte('', `
    <div class="banner ${fvae.configure ? (fvae.connexion.ok ? 'info' : 'warn') : 'warn'}">
      <div><b>${fvae.configure ? (fvae.connexion.ok ? 'Connecteur opérationnel' : 'Connecteur configuré mais injoignable') : 'Connecteur non configuré'}</b><br>
      ${esc(fvae.connexion.message)}</div>
    </div>
    <dl class="kv" style="font-size:12.5px">
      <dt>URL de base</dt><dd class="num">${esc(fvae.baseUrl)}${esc(fvae.prefixe)}</dd>
      <dt>Dossiers rattachés à un identifiant France VAE</dt><dd>${fvae.dossiersAvecIdentifiant}</dd>
    </dl>
    <div class="form-actions">
      <input class="f" id="idFvae" placeholder="Identifiant de candidature France VAE" style="max-width:320px">
      <button class="btn" id="btnLireFvae">Lire la candidature</button>
    </div>
    <div id="sortieFvae"></div>`)}

  ${section('Import de candidatures')}
  ${carte('', `
    ${aSavoir(['Export du back-office France VAE, ou tout fichier CSV', 'Simuler avant d\'importer'])}
    <div class="form-actions" style="border-top:0;padding-top:0;margin-top:0">
      <input type="file" id="fichierImport" accept=".csv,.txt,.tsv" class="f" style="max-width:none;flex:1">
      <button class="btn" id="btnAnalyser">Analyser</button>
    </div>
    <div id="sortieImport"></div>`)}

  ${section('Site groupe-ei.fr')}
  ${carte('', `
    <div class="kpi-grid" style="margin-bottom:14px">
      ${kpi('Demandes en attente', demandes.length, 'à qualifier', demandes.length ? 'warn' : 'ok')}
    </div>
    ${aSavoir(['Point d\'entrée du formulaire : <span class="formula">POST /api/public/candidature</span>, en-tête <span class="formula">X-EiVae-Secret</span>'])}
    <div id="listeDemandes">${demandes.length ? `<div class="tw"><table class="t">
      <thead><tr><th>Reçue le</th><th>Candidat</th><th>Contact</th><th>Certification souhaitée</th><th>Message</th><th></th></tr></thead>
      <tbody>${demandes.map(d => `<tr>
        <td class="num">${fmtDate(d.recueLe.slice(0, 10))}</td>
        <td>${who(d.prenom + ' ' + d.nom, [d.ville, d.codePostal].filter(Boolean).join(' '), true)}</td>
        <td style="font-size:11.5px">${esc(d.email || '')}<br>${esc(d.telephone || '')}</td>
        <td style="font-size:11.5px">${esc(d.certificationSouhaitee || '—')}
          ${d.codeRncpDetecte ? `<br><span class="chip c-info">${esc(d.codeRncpDetecte)}</span>` : ''}</td>
        <td style="font-size:11.5px;max-width:220px;color:var(--ink-2)">${esc((d.message || '').slice(0, 140))}</td>
        <td><button class="btn primary" style="padding:3px 9px;font-size:11.5px"
          onclick="qualifierDemande(${d.id})">Qualifier</button></td></tr>`).join('')}
      </tbody></table></div>` : vide('Aucune demande en attente.')}</div>`)}

  ${section('SharePoint')}
  ${carte('', `
    <div class="banner ${sp.configure ? 'info' : 'warn'}"><div><b>${sp.configure ? 'Configuré' : 'Non configuré'}</b></div></div>
    <dl class="kv" style="font-size:12.5px">
      <dt>Site</dt><dd class="num">${esc(sp.siteUrl || '—')}</dd>
      <dt>Bibliothèque</dt><dd>${esc(sp.bibliotheque)}</dd>
      <dt>Racine candidats</dt><dd>${esc(sp.racineCandidats)}</dd>
      <dt>Racine réseau</dt><dd>${esc(sp.racineIntervenants)}</dd>
      <dt>Racine VAE collective</dt><dd>${esc(sp.racineProjets)}</dd>
      <dt>Gabarit de nommage</dt><dd class="num">${esc(sp.gabaritDossierCandidat)}</dd>
    </dl>
    ${aSavoir(['Jetons du gabarit : ' + sp.jetons.map(j => `<span class="formula">${esc(j)}</span>`).join(' '),
      'Réglage : <span class="formula">appsettings.json</span>, sections SharePoint et DossiersCandidats'], true)}
    <div id="apercuSp">${chargement('Aperçu…')}</div>`)}

  ${section('Journal des imports')}
  ${carte('', imports.length ? `<div class="tw"><table class="t">
    <thead><tr><th>Source</th><th>Démarré</th><th>Statut</th><th>Référence</th>
    <th class="n">Lus</th><th class="n">Créés</th><th class="n">Màj</th><th class="n">Ignorés</th><th class="n">Erreurs</th></tr></thead>
    <tbody>${imports.map(i => `<tr>
      <td><span class="chip c-neutral">${esc(i.source)}</span></td>
      <td class="num">${fmtDate(i.demarreLe.slice(0, 10))}</td>
      <td><span class="chip c-${i.statut === 'Termine' ? 'ok' : i.statut === 'Echec' ? 'crit' : 'warn'}">${esc(i.statut)}</span></td>
      <td style="font-size:11px;color:var(--muted);max-width:260px;overflow:hidden;text-overflow:ellipsis">${esc(i.reference || '—')}</td>
      <td class="n">${i.nombreLus}</td><td class="n">${i.nombreCrees}</td><td class="n">${i.nombreMisAJour}</td>
      <td class="n">${i.nombreIgnores}</td><td class="n">${i.nombreErreurs || '—'}</td></tr>`).join('')}
    </tbody></table></div>` : vide('Aucun import enregistré.'), '', 'tight')}`;

  // ---- aperçu SharePoint sur un dossier réel ----
  API.get('/referentiel/sharepoint/apercu').then(a => {
    $('#apercuSp').innerHTML = `<div style="border-top:1px solid var(--line);margin-top:12px;padding-top:11px">
      <span class="eyebrow">Aperçu sur un dossier réel</span>
      <dl class="kv" style="font-size:12.5px;margin-top:7px">
        <dt>Candidat</dt><dd>${esc(a.candidat)}</dd>
        <dt>Chemin</dt><dd class="num">${esc(a.chemin)}</dd>
        <dt>Lien</dt><dd>${a.url ? lienExterne(a.url, 'ouvrir dans SharePoint') : '—'}</dd>
      </dl></div>`;
  }).catch(() => { $('#apercuSp').innerHTML = ''; });

  // ---- synchronisation France Compétences ----
  $('#btnSyncFc').onclick = async () => {
    const b = $('#btnSyncFc');
    b.disabled = true; b.textContent = 'Synchronisation en cours…';
    $('#sortieSync').innerHTML = chargement('Téléchargement et lecture de l\'export…');
    try {
      const r = await API.post('/integrations/france-competences/synchroniser');
      $('#sortieSync').innerHTML = `<div class="banner ${r.nombreErreurs ? 'warn' : 'info'}" style="margin-top:12px">
        <div><b>${esc(r.statut)}</b> — ${r.nombreLus} fiches parcourues, ${r.nombreCrees} créées,
        ${r.nombreMisAJour} mises à jour, ${r.nombreIgnores} hors périmètre, ${r.nombreErreurs} erreurs.
        ${(r.journal || []).map(l => '<br>' + esc(l)).join('')}</div></div>`;
      toast('Référentiel synchronisé.', 'ok');
    } catch (e) {
      $('#sortieSync').innerHTML = `<div class="banner" style="margin-top:12px">${esc(e.message)}</div>`;
    }
    b.disabled = false; b.textContent = 'Synchroniser maintenant';
  };

  // ---- lecture France VAE ----
  $('#btnLireFvae').onclick = async () => {
    const id = $('#idFvae').value.trim();
    if (!id) { toast('Indiquez un identifiant de candidature.', 'err'); return; }
    $('#sortieFvae').innerHTML = chargement();
    try {
      const c = await API.post('/integrations/france-vae/candidature/' + encodeURIComponent(id));
      $('#sortieFvae').innerHTML = `<div class="banner info" style="margin-top:12px"><div>
        <b>${esc(c.prenom || '')} ${esc(c.nom || '')}</b><br>
        ${esc(c.intituleCertification || c.codeRncp || 'certification non précisée')} ·
        statut ${esc(c.statut || '—')} · ${esc(c.departement || '')}<br>
        ${c.dossierExistant ? `Dossier existant : <a href="#" onclick="ouvrirFiche(${c.dossierExistant});return false;">ouvrir la fiche</a>`
          : 'Aucun dossier local rattaché à cet identifiant.'}</div></div>`;
    } catch (e) {
      $('#sortieFvae').innerHTML = `<div class="banner" style="margin-top:12px">${esc(e.message)}</div>`;
    }
  };

  // ---- import de fichier ----
  $('#btnAnalyser').onclick = async () => {
    const f = $('#fichierImport').files[0];
    if (!f) { toast('Choisissez un fichier.', 'err'); return; }

    $('#sortieImport').innerHTML = chargement('Lecture des en-têtes…');
    const fd = new FormData(); fd.append('fichier', f);

    try {
      const a = await API.post('/integrations/import/entetes', fd);
      const cles = ['nom', 'prenom', 'email', 'telephone', 'certification', 'codeRncp',
        'dateDemande', 'departement', 'ville', 'statut', 'identifiantFranceVae'];
      const libelles = {
        nom: 'Nom', prenom: 'Prénom', email: 'Courriel', telephone: 'Téléphone',
        certification: 'Certification', codeRncp: 'Code RNCP', dateDemande: 'Date de demande',
        departement: 'Département', ville: 'Ville', statut: 'Statut',
        identifiantFranceVae: 'Identifiant France VAE',
      };

      $('#sortieImport').innerHTML = `
        <div style="border-top:1px solid var(--line);margin-top:14px;padding-top:13px">
          <span class="eyebrow">Correspondance des colonnes</span>
          <p class="hint" style="margin:6px 0 10px">${a.entetes.length} colonnes détectées.
          Nom et prénom sont obligatoires.</p>
          <div class="form-grid" id="mappage">
            ${cles.map(k => `<div class="field"><label for="m_${k}">${esc(libelles[k])}${k === 'nom' || k === 'prenom' ? ' <span class="req">*</span>' : ''}</label>
              <select id="m_${k}" data-cle="${k}"><option value="">— ignorer —</option>
              ${a.entetes.map(e => `<option${a.mappagePropose[k] === e ? ' selected' : ''}>${esc(e)}</option>`).join('')}
              </select></div>`).join('')}
          </div>
          <div class="form-actions">
            <button class="btn" id="btnSimuler">Simuler</button>
            <button class="btn primary" id="btnImporter">Importer</button>
          </div>
          <div id="resultatImport"></div>
        </div>`;

      const lancer = async (simulation) => {
        const mappage = {};
        $$('#mappage select').forEach(s => { if (s.value) mappage[s.dataset.cle] = s.value; });
        if (!mappage.nom || !mappage.prenom) { toast('Indiquez les colonnes nom et prénom.', 'err'); return; }

        $('#resultatImport').innerHTML = chargement(simulation ? 'Simulation…' : 'Import…');
        const fd2 = new FormData();
        fd2.append('fichier', f);
        fd2.append('simulation', String(simulation));
        fd2.append('mappage', JSON.stringify(mappage));

        try {
          const r = await API.post('/integrations/import/candidatures', fd2);
          $('#resultatImport').innerHTML = `<div class="banner ${r.erreurs ? 'warn' : 'info'}" style="margin-top:12px">
            <div><b>${simulation ? 'Simulation' : 'Import terminé'}</b> — ${r.lus} lignes lues,
            ${r.crees} dossiers ${simulation ? 'seraient créés' : 'créés'},
            ${r.misAJour} mis à jour, ${r.ignores} ignorés, ${r.erreurs} erreurs.
            ${(r.journal || []).slice(0, 12).map(l => '<br>' + esc(l)).join('')}</div></div>`;
          if (!simulation) { toast(`${r.crees} dossiers importés.`, 'ok'); }
        } catch (e) {
          $('#resultatImport').innerHTML = `<div class="banner" style="margin-top:12px">${esc(e.message)}</div>`;
        }
      };

      $('#btnSimuler').onclick = () => lancer(true);
      $('#btnImporter').onclick = () => lancer(false);
    } catch (e) {
      $('#sortieImport').innerHTML = `<div class="banner" style="margin-top:12px">${esc(e.message)}</div>`;
    }
  };
};

async function qualifierDemande(id) {
  ouvrirModale('Qualifier la demande', null, `
    <form id="frmQual" onsubmit="return false">
      <div class="form-grid">
        ${champ({ nom: 'certificationId', label: 'Certification', type: 'select',
          options: optionsCertifications(), vide: 'À déterminer', span: true })}
        ${champ({ nom: 'aapId', label: 'AAP référent', type: 'select',
          options: optionsIntervenants('ArchitecteAccompagnateurParcours'), vide: 'Non affecté' })}
        ${champ({ nom: 'commentaire', label: 'Commentaire', type: 'textarea', span: true })}
      </div>
      <div class="form-actions">
        <button class="btn" id="btnRejeter" style="color:var(--crit)">Rejeter la demande</button>
        <span class="grow"></span>
        <button class="btn" onclick="fermerModale()">Annuler</button>
        <button class="btn primary" id="btnQualifier">Créer le dossier</button>
      </div>
    </form>`);

  const envoyer = async (rejeter) => {
    const d = lireFormulaire($('#frmQual'));
    d.rejeter = rejeter;
    try {
      const r = await API.post(`/integrations/demandes-web/${id}/qualifier`, d);
      fermerModale();
      toast(rejeter ? 'Demande rejetée.' : 'Dossier créé.', 'ok');
      toastDossier(r.dossier);
      majCompteurs();
      if (r.parcoursId) ouvrirFiche(r.parcoursId); else rafraichir();
    } catch (e) { toast(e.message, 'err', 'Qualification impossible'); }
  };

  $('#btnQualifier').onclick = () => envoyer(false);
  $('#btnRejeter').onclick = () => envoyer(true);
}

/* ============================== VAE COLLECTIVE ============================== */
VUES.collective = async function () {
  const projets = await API.get('/collective/projets');

  $('#vActions').innerHTML =
    `<button class="btn primary" onclick="formulaireProjet()">Nouveau projet</button>`;

  const cohortes = projets.flatMap(p => p.cohortes);
  const candidats = cohortes.reduce((a, c) => a + c.candidats, 0);
  const ca = cohortes.reduce((a, c) => a + c.caPrevisionnel, 0);
  const marge = cohortes.reduce((a, c) => a + c.margePrevisionnelle, 0);

  $('#vue').innerHTML = `
  <div class="kpi-grid">
    ${kpi('Projets', projets.length, projets.filter(p => p.statut === 'En cours').length + ' en cours', 'brand')}
    ${kpi('Cohortes', cohortes.length, 'toutes certifications')}
    ${kpi('Candidats rattachés', candidats, 'dossiers en cohorte')}
    ${kpi('CA prévisionnel', eurK(ca), 'des cohortes ouvertes', 'brand')}
    ${kpi('Marge prévisionnelle', eurK(marge), ratio(marge, ca) + ' du CA', 'ok')}
  </div>

  ${projets.length ? projets.map(p => `
    ${section(p.raisonSociale, p.nom)}
    ${carte('', `
      <dl class="kv" style="font-size:12.5px">
        <dt>Statut</dt><dd><span class="chip c-brand">${esc(p.statut)}</span></dd>
        <dt>Référent entreprise</dt><dd>${esc([p.referent.referentNom, p.referent.referentFonction].filter(Boolean).join(' · ') || '—')}</dd>
        <dt>Contact</dt><dd>${esc([p.referent.referentEmail, p.referent.referentTelephone].filter(Boolean).join(' · ') || '—')}</dd>
        <dt>AAP référent</dt><dd>${esc(p.aapReferent || '—')}</dd>
        <dt>Financement</dt><dd>${esc(p.dispositif)}${p.montantContractualise ? ' · ' + eur(p.montantContractualise) : ''}</dd>
        <dt>OPCO</dt><dd>${esc(p.opco || '—')}</dd>
        <dt>Contractualisation</dt><dd>${fmtDateLongue(p.dateContractualisation)}</dd>
        <dt>Dossier SharePoint</dt><dd>${p.urlSharePoint ? lienExterne(p.urlSharePoint, 'ouvrir') : '—'}</dd>
      </dl>
      ${p.cohortes.length ? `<table class="t" style="margin-top:12px">
        <thead><tr><th>Cohorte</th><th>Certification</th><th class="n">Candidats</th><th class="n">Actifs</th>
        <th class="n">CA prévisionnel</th><th class="n">Marge</th><th>Ouverture</th><th></th></tr></thead>
        <tbody>${p.cohortes.map(c => `<tr>
          <td><b style="font-weight:600">${esc(c.nom)}</b></td>
          <td style="font-size:11.5px">${esc(c.certification || '—')}</td>
          <td class="n">${c.candidats}${c.effectifCible ? ' / ' + c.effectifCible : ''}</td>
          <td class="n">${c.actifs}</td>
          <td class="n">${eur(c.caPrevisionnel)}</td>
          <td class="n" style="color:var(--ok)">${eur(c.margePrevisionnelle)}</td>
          <td class="num">${fmtDate(c.dateOuverture)}</td>
          <td><button class="btn ghost" style="padding:2px 8px;font-size:11.5px"
            onclick="ficheCohorte(${c.id})">Suivi</button></td></tr>`).join('')}
        </tbody></table>` : vide('Aucune cohorte. Ajoutez-en une pour rattacher des candidats.')}
      <div class="form-actions">
        <button class="btn" onclick="formulaireCohorte(${p.id})">Ajouter une cohorte</button>
      </div>`)}`).join('') : `
    ${carte('', `<div class="empty" style="padding:34px 16px">
      Aucun projet de VAE collective enregistré.<br>
      <button class="btn primary" style="margin-top:12px" onclick="formulaireProjet()">Créer le premier projet</button>
    </div>`)}`}`;
};

function formulaireProjet() {
  ouvrirModale('Nouveau projet de VAE collective', null, `
    <form id="frmProjet" onsubmit="return false">
      <fieldset class="fieldset"><legend>Entreprise</legend>
        <div class="form-grid">
          ${champ({ nom: 'raisonSociale', label: 'Raison sociale', requis: true })}
          ${champ({ nom: 'nom', label: 'Nom du projet', requis: true, exemple: 'TP IEPE en crèche — promotion 2026' })}
          ${champ({ nom: 'siret', label: 'SIRET' })}
          ${champ({ nom: 'secteurActivite', label: "Secteur d'activité" })}
          ${champ({ nom: 'effectif', label: 'Effectif', type: 'number', min: 0 })}
          ${champ({ nom: 'conventionCollective', label: 'Convention collective' })}
          ${champ({ nom: 'opco', label: 'OPCO' })}
        </div>
      </fieldset>
      <fieldset class="fieldset"><legend>Référent entreprise</legend>
        <div class="form-grid">
          ${champ({ nom: 'referentNom', label: 'Nom' })}
          ${champ({ nom: 'referentFonction', label: 'Fonction' })}
          ${champ({ nom: 'referentEmail', label: 'Courriel', type: 'email' })}
          ${champ({ nom: 'referentTelephone', label: 'Téléphone' })}
        </div>
      </fieldset>
      <fieldset class="fieldset"><legend>Pilotage &amp; financement</legend>
        <div class="form-grid">
          ${champ({ nom: 'statut', label: 'Statut', type: 'select', vide: false, valeur: 'Diagnostic',
            options: ['Diagnostic', 'Négociation', 'Contractualisé', 'En cours', 'Clôturé', 'Abandonné'] })}
          ${champ({ nom: 'aapReferentId', label: 'AAP référent', type: 'select',
            options: optionsIntervenants('ArchitecteAccompagnateurParcours'), vide: 'Non affecté' })}
          ${champ({ nom: 'dispositif', label: 'Dispositif', type: 'select', vide: false, valeur: 'Entreprise',
            options: ETAT.nomenclatures.dispositifs.filter(d => d.code !== 'NonSecurise') })}
          ${champ({ nom: 'montantContractualise', label: 'Montant contractualisé (€)', type: 'number', pas: '0.01', min: 0 })}
          ${champ({ nom: 'referenceContrat', label: 'Référence du contrat' })}
          ${champ({ nom: 'dateDiagnostic', label: 'Diagnostic', type: 'date' })}
          ${champ({ nom: 'dateContractualisation', label: 'Contractualisation', type: 'date' })}
          ${champ({ nom: 'dateOuverture', label: 'Ouverture', type: 'date' })}
          ${champ({ nom: 'dateCloturePrevue', label: 'Clôture prévue', type: 'date' })}
          ${champ({ nom: 'commentaire', label: 'Commentaire', type: 'textarea', span: true })}
        </div>
      </fieldset>
      <div class="form-actions"><span class="grow"></span>
        <button class="btn" onclick="fermerModale()">Annuler</button>
        <button class="btn primary" id="btnProjet">Créer le projet</button></div>
    </form>`);

  $('#btnProjet').onclick = async () => {
    const d = lireFormulaire($('#frmProjet'));
    if (!d.raisonSociale || !d.nom) { toast('La raison sociale et le nom du projet sont obligatoires.', 'err'); return; }
    try {
      await API.post('/collective/projets', d);
      fermerModale(); toast('Projet créé.', 'ok'); rafraichir();
    } catch (e) { toast(e.message, 'err', 'Création impossible'); }
  };
}

function formulaireCohorte(projetId) {
  ouvrirModale('Nouvelle cohorte', null, `
    <form id="frmCohorte" onsubmit="return false">
      <div class="form-grid">
        ${champ({ nom: 'nom', label: 'Nom de la cohorte', requis: true, exemple: 'Promotion 1 — automne 2026' })}
        ${champ({ nom: 'certificationId', label: 'Certification visée', type: 'select',
          options: optionsCertifications(), vide: 'À déterminer' })}
        ${champ({ nom: 'effectifCible', label: 'Effectif cible', type: 'number', min: 1 })}
        ${champ({ nom: 'rythme', label: 'Rythme des ateliers', exemple: 'Un atelier collectif toutes les trois semaines' })}
        ${champ({ nom: 'animateurId', label: 'Animateur des ateliers', type: 'select',
          options: optionsIntervenants(), vide: 'Non affecté' })}
        ${champ({ nom: 'dateOuverture', label: 'Ouverture', type: 'date' })}
        ${champ({ nom: 'dateCloturePrevue', label: 'Clôture prévue', type: 'date' })}
        ${champ({ nom: 'commentaire', label: 'Commentaire', type: 'textarea', span: true })}
      </div>
      <div class="form-actions"><span class="grow"></span>
        <button class="btn" onclick="fermerModale()">Annuler</button>
        <button class="btn primary" id="btnCohorte">Créer la cohorte</button></div>
    </form>`);

  $('#btnCohorte').onclick = async () => {
    const d = lireFormulaire($('#frmCohorte'));
    if (!d.nom) { toast('Le nom de la cohorte est obligatoire.', 'err'); return; }
    try {
      await API.post(`/collective/projets/${projetId}/cohortes`, d);
      fermerModale(); toast('Cohorte créée.', 'ok'); rafraichir();
    } catch (e) { toast(e.message, 'err', 'Création impossible'); }
  };
}

async function ficheCohorte(id) {
  ouvrirTiroir(chargement());
  const c = await API.get('/collective/cohortes/' + id);

  ouvrirTiroir(`
  <div class="dr-h">
    <div style="flex:1;min-width:0">
      <h2>${esc(c.nom)}</h2>
      <div class="s">${esc(c.projet.raisonSociale)} · ${esc(c.certification || 'certification non précisée')}</div>
    </div>
    <button class="btn ghost" onclick="fermerTiroir()" aria-label="Fermer">✕</button>
  </div>
  <div class="dr-b">
    <div class="dr-sec"><span class="eyebrow">Économie de la cohorte</span>
      <div class="simout" style="margin-top:9px">
        <div><div class="l">CA prévisionnel</div><div class="v" style="color:var(--teal)">${eur(c.economie.caPrevisionnel)}</div></div>
        <div><div class="l">Marge</div><div class="v" style="color:var(--ok)">${eur(c.economie.margePrevisionnelle)}</div></div>
        <div><div class="l">Heures collectives</div><div class="v">${nf(c.economie.heuresCollectives)} h</div></div>
      </div>
    </div>
    <div class="dr-sec"><span class="eyebrow">Avancement des candidats</span>
      <table class="t"><thead><tr><th>Candidat</th><th>Étape</th><th>Écart</th><th class="n">Alertes</th></tr></thead>
      <tbody>${c.candidats.length ? c.candidats.map(x => `<tr class="clik" onclick="fermerTiroir();ouvrirFiche(${x.id})">
        <td>${who(x.nom, '', true)}</td>
        <td><span class="chip c-brand">${esc(x.etape)}</span></td>
        <td>${x.enRetard ? `<span class="chip c-crit">${x.ecartCohorte} étape${Math.abs(x.ecartCohorte) > 1 ? 's' : ''}</span>`
          : '<span class="chip c-ok">dans le groupe</span>'}</td>
        <td class="n">${x.alertes || '—'}</td></tr>`).join('')
        : '<tr><td colspan="4">' + vide('Aucun candidat rattaché.') + '</td></tr>'}</tbody></table>
    </div>
    <div class="dr-sec"><span class="eyebrow">Ateliers collectifs</span>
      ${c.ateliers.length ? `<table class="t"><thead><tr><th>Date</th><th>Thème</th><th class="n">Durée</th><th>Animateur</th><th>État</th></tr></thead>
      <tbody>${c.ateliers.map(a => `<tr><td class="num">${fmtDate(a.date)}</td><td>${esc(a.theme)}</td>
      <td class="n">${nf(a.dureeHeures)} h</td><td style="font-size:11.5px">${esc(a.intervenant || '—')}</td>
      <td>${a.realise ? '<span class="chip c-ok">réalisé</span>' : '<span class="chip c-neutral">planifié</span>'}</td></tr>`).join('')}
      </tbody></table>` : vide('Aucun atelier planifié.')}
    </div>
  </div>`);
}

/* ============================== RÈGLES DE GESTION ============================== */
let REF = {};

VUES.regles = async function () {
  const [grilles, parametres, regles, catalogue, etapes, droits] = await Promise.all([
    API.get('/referentiel/grilles'), API.get('/referentiel/parametres'),
    API.get('/referentiel/regles'), API.get('/referentiel/regles/catalogue'),
    API.get('/referentiel/etapes'), API.get('/referentiel/droits'),
  ]);
  REF = { parametres, regles, catalogue, droits };

  $('#vue').innerHTML = `
  ${aSavoir([
    ['Grilles', null, "document.getElementById('grilles').scrollIntoView()"],
    ['Paramètres', null, "document.getElementById('parametres').scrollIntoView()"],
    ['Règles d\'alerte', null, "document.getElementById('regles-alerte').scrollIntoView()"],
    ['Droits d\'accès', null, "document.getElementById('droits').scrollIntoView()"],
    ['Utilisateurs', null, "document.getElementById('utilisateurs').scrollIntoView()"],
  ])}

  <div id="grilles">${section('Grilles tarifaires')}</div>
  ${carte('', `<div class="tw"><table class="t">
    <thead><tr><th>Grille</th><th>Période d'application</th><th class="n">Forfait</th><th class="n">Individuel</th>
    <th class="n">Collectif</th><th class="n">Compléments</th><th class="n">Jury</th><th class="n">Plafond</th>
    <th class="n">Dossiers</th></tr></thead>
    <tbody>${grilles.map(g => `<tr${g.enVigueur ? ' style="background:var(--surface-2)"' : ''}>
      <td><b style="font-weight:600">${esc(g.code)}</b>${g.enVigueur ? ' <span class="chip c-ok">en vigueur</span>' : ''}
        <br><span style="font-size:10.5px;color:var(--faint)">${esc(g.libelle)}</span></td>
      <td class="num" style="font-size:11.5px">${fmtDate(g.dateEffet)} → ${g.dateFin ? fmtDate(g.dateFin) : '—'}</td>
      <td class="n">${eur(g.forfaitArchitecture)}</td>
      <td class="n">${g.tarifHoraireIndividuel} €/h</td>
      <td class="n">${g.tarifHoraireCollectif} €/h</td>
      <td class="n">${g.tarifHoraireComplementFormatif} €/h</td>
      <td class="n">${eur(g.fraisJury)}</td>
      <td class="n">${eur(g.plafondMontantTotal)}</td>
      <td class="n"><b>${g.dossiers}</b></td></tr>`).join('')}</tbody></table></div>
    <div class="form-actions">
      <button class="btn" onclick="formulaireGrille()">Ajouter une grille</button>
      <button class="btn ghost" onclick="reaffecterGrilles()">Réaffecter les dossiers non facturés</button>
      <span style="font-size:11.5px;color:var(--faint)">Plafonds horaires :
        ${grilles.at(-1).plafondHeuresIndividuel} h individuel ·
        ${grilles.at(-1).plafondHeuresCollectif} h collectif ·
        ${grilles.at(-1).plafondHeuresComplementFormatif} h compléments</span>
    </div>`, '', 'tight')}

  <div id="parametres">${section('Paramètres de gestion')}</div>
  ${carte('', `<div class="tw"><table class="t">
    <thead><tr><th>Paramètre</th><th>Catégorie</th><th>Description</th><th style="width:150px">Valeur</th><th></th></tr></thead>
    <tbody>${parametres.length ? parametres.map(p => `<tr>
      <td><b style="font-weight:600">${esc(p.libelle)}</b><br><span class="num" style="font-size:10px;color:var(--faint)">${esc(p.cle)}</span>
        ${p.utiliseParApplication ? '<span class="chip c-info" style="margin-left:4px">utilisé</span>' : ''}</td>
      <td><span class="chip c-neutral">${esc(p.categorie || '—')}</span></td>
      <td style="font-size:11.5px;color:var(--ink-2);max-width:340px">${esc(p.description || '')}</td>
      <td><div style="display:flex;gap:5px;align-items:center">
        <input class="f" style="width:74px;font-family:var(--f-data)" value="${esc(p.valeur)}" data-cle="${esc(p.cle)}">
        <span style="font-size:10.5px;color:var(--faint)">${esc(p.unite || '')}</span>
      </div></td>
      <td><div class="actions-ligne">
        <button class="btn ghost mini" onclick="formulaireParametre('${esc(p.cle)}')">Modifier</button>
        <button class="btn ghost mini danger" onclick="supprimerParametre('${esc(p.cle)}')">Supprimer</button>
      </div></td></tr>`).join('') : '<tr><td colspan="5">' + vide('Aucun paramètre.') + '</td></tr>'}</tbody></table></div>
    <div class="form-actions">
      <button class="btn" onclick="formulaireParametre()">Nouveau paramètre</button>
      <span class="grow"></span>
      <button class="btn primary" id="btnParams">Enregistrer les valeurs</button></div>`, '', 'tight')}

  <div id="regles-alerte">${section("Règles d'alerte", regles.filter(r => r.active).length + ' actives sur ' + regles.length)}</div>
  ${carte('', `<div class="tw"><table class="t">
    <thead><tr><th>Active</th><th class="n">Règle</th><th>Sévérité</th><th>Intitulé</th><th>Condition</th>
    <th>Action attendue</th><th>Responsable</th><th class="n">Ouvertes</th><th></th></tr></thead>
    <tbody>${regles.length ? regles.map(r => `<tr class="${r.active ? '' : 'inactive'}">
      <td><input type="checkbox" ${r.active ? 'checked' : ''} onchange="activerRegle(${r.id}, this.checked)" aria-label="Activer ${esc(r.code)}"></td>
      <td class="n"><b>${esc(r.code)}</b></td>
      <td><span class="chip c-${r.severite === 'critique' ? 'crit' : 'warn'}">${esc(r.severite)}</span></td>
      <td><b style="font-weight:600">${esc(r.libelle)}</b>${r.fondement ? `<br><span style="font-size:10.5px;color:var(--faint)">${esc(r.fondement)}</span>` : ''}</td>
      <td style="font-size:11.5px;color:var(--ink-2);max-width:260px">${esc(r.seuilDescription)}</td>
      <td style="font-size:11.5px">${esc(r.actionAttendue)}</td>
      <td style="font-size:11.5px;color:var(--muted)">${esc(r.responsable || '—')}</td>
      <td class="n">${r.alertesOuvertes || '—'}</td>
      <td><div class="actions-ligne">
        <button class="btn ghost mini" onclick="formulaireRegle(${r.id})">Modifier</button>
        <button class="btn ghost mini danger" onclick="supprimerRegle(${r.id})">Supprimer</button>
      </div></td></tr>`).join('') : '<tr><td colspan="9">' + vide('Aucune règle d\'alerte.') + '</td></tr>'}
    </tbody></table></div>
    <div class="form-actions"><button class="btn" onclick="formulaireRegle()">Nouvelle règle</button></div>`, '', 'tight')}

  ${section('Étapes du parcours')}
  ${carte('', `<div style="display:flex;gap:7px;flex-wrap:wrap">${etapes.map(e =>
    `<span class="chip c-${e.code === 'Sortie' ? 'neutral' : 'brand'}">E${e.rang} · ${esc(e.libelle)}</span>`).join('')}</div>`)}

  <div id="droits">${section("Droits d'accès")}</div>
  ${carte('', `<div class="tw"><table class="t matrix" id="matrice">
    <thead><tr><th>Rôle</th>${droits.briques.map(b => `<th class="n" style="font-size:9px">${esc(b)}</th>`).join('')}
      ${droits.evenements.map(ev => `<th class="n" style="font-size:9px">Notifié : ${esc(ev.libelle.toLowerCase())}</th>`).join('')}
      <th class="n">Membres</th><th></th></tr></thead>
    <tbody>${droits.roles.map(r => `<tr data-role="${r.id}">
      <td><b style="font-weight:600">${esc(r.nom)}</b>${r.description ? `<br><span style="font-size:10.5px;color:var(--faint)">${esc(r.description)}</span>` : ''}</td>
      ${r.droits.map((d, i) => `<td class="m"><select class="cell-select" data-brique="${esc(droits.briques[i])}">
        ${Object.keys(droits.legende).map(k => `<option value="${k}"${k === d ? ' selected' : ''}>${esc(droits.legende[k].split(' —')[0])}</option>`).join('')}
      </select></td>`).join('')}
      ${droits.evenements.map(ev => `<td class="m"><input type="checkbox" data-evenement="${esc(ev.code)}"${r.notifications.includes(ev.code) ? ' checked' : ''}></td>`).join('')}
      <td class="n">${r.membres || '—'}</td>
      <td><div class="actions-ligne">
        <button class="btn ghost mini" onclick="formulaireRole(${r.id})">Renommer</button>
        <button class="btn ghost mini danger" onclick="supprimerRole(${r.id})">Supprimer</button>
      </div></td>
    </tr>`).join('')}</tbody></table></div>
    <div class="form-actions">
      <button class="btn" onclick="formulaireRole()">Nouveau rôle</button>
      <span class="grow"></span>
      <button class="btn primary" id="btnDroits">Enregistrer la matrice</button></div>
    <p style="font-size:11.5px;color:var(--faint);padding:0 15px 12px;line-height:1.6;margin:0">
      ${Object.entries(droits.legende).map(([k, v]) => `<b style="color:var(--ink)">${esc(v.split(' —')[0])}</b> : ${esc(v.split(' — ')[1] || '')}`).join(' · ')}.</p>`, '', 'tight')}

  <div id="utilisateurs">${section('Utilisateurs', droits.utilisateurs.length + ' comptes')}</div>
  ${carte('', `<div class="tw"><table class="t">
    <thead><tr><th>Utilisateur</th><th>Courriel</th><th>Rôle</th><th>État</th><th></th></tr></thead>
    <tbody>${droits.utilisateurs.length ? droits.utilisateurs.map(u => `<tr class="${u.actif ? '' : 'inactive'}">
      <td>${who(u.nomComplet, '', true)}</td>
      <td style="font-size:11.5px">${esc(u.email)}</td>
      <td>${u.role ? `<span class="chip c-brand">${esc(u.role)}</span>` : '<span class="chip c-warn">sans rôle</span>'}</td>
      <td>${u.actif ? '<span class="chip c-ok">actif</span>' : '<span class="chip c-neutral">inactif</span>'}</td>
      <td><div class="actions-ligne">
        <button class="btn ghost mini" onclick="formulaireUtilisateur(${u.id})">Modifier</button>
        <button class="btn ghost mini danger" onclick="supprimerUtilisateur(${u.id})">Supprimer</button>
      </div></td></tr>`).join('') : '<tr><td colspan="5">' + vide('Aucun utilisateur.') + '</td></tr>'}
    </tbody></table></div>
    <div class="form-actions"><button class="btn" onclick="formulaireUtilisateur()">Nouvel utilisateur</button></div>
    ${aSavoir(['Les membres d\'un rôle reçoivent les notifications cochées dans la matrice',
      ['Voir les notifications', 'notifications']], true)}`, '', 'tight')}`;

  $('#btnParams').onclick = async () => {
    let modifies = 0;
    for (const el of $$('#vue input[data-cle]')) {
      const original = parametres.find(p => p.cle === el.dataset.cle);
      if (original && original.valeur !== el.value.trim()) {
        try {
          await API.put('/referentiel/parametres/' + encodeURIComponent(el.dataset.cle), { valeur: el.value.trim() });
          modifies++;
        } catch (e) { toast(`${el.dataset.cle} : ${e.message}`, 'err'); }
      }
    }
    toast(modifies ? `${modifies} paramètre${modifies > 1 ? 's' : ''} enregistré${modifies > 1 ? 's' : ''}.` : 'Aucune modification.', 'ok');
    if (modifies) rafraichir();
  };

  $('#btnDroits').onclick = async () => {
    let modifies = 0;
    for (const tr of $$('#matrice tbody tr')) {
      const role = droits.roles.find(r => r.id === Number(tr.dataset.role));
      const nouveaux = {};
      $$('select[data-brique]', tr).forEach(s => { nouveaux[s.dataset.brique] = s.value; });
      const notifications = $$('input[data-evenement]:checked', tr).map(c => c.dataset.evenement);
      const avant = droits.briques.map((b, i) => role.droits[i]).join();
      const apres = droits.briques.map(b => nouveaux[b]).join();
      if (avant === apres && [...role.notifications].sort().join() === [...notifications].sort().join()) continue;
      try {
        await API.put('/referentiel/roles/' + role.id, {
          nom: role.nom, description: role.description, ordre: role.ordre, droits: nouveaux, notifications,
        });
        modifies++;
      } catch (e) { toast(`${role.nom} : ${e.message}`, 'err'); }
    }
    toast(modifies ? `${modifies} rôle${modifies > 1 ? 's' : ''} mis à jour.` : 'Aucune modification.', 'ok');
    if (modifies) rafraichir();
  };
};

/* ---------- paramètres ---------- */
function formulaireParametre(cle = null) {
  const p = cle ? REF.parametres.find(x => x.cle === cle) : null;
  ouvrirModale(p ? 'Modifier le paramètre' : 'Nouveau paramètre', p ? p.cle : null, `
    <form id="frmParam" onsubmit="return false">
      <div class="form-grid">
        ${p ? '' : champ({ nom: 'cle', label: 'Clé', requis: true, exemple: 'capacite.expert' ,
          aide: 'Minuscules, chiffres, points et tirets bas.' })}
        ${champ({ nom: 'libelle', label: 'Libellé', requis: true, valeur: p?.libelle })}
        ${champ({ nom: 'valeur', label: 'Valeur', requis: true, valeur: p?.valeur })}
        ${champ({ nom: 'unite', label: 'Unité', valeur: p?.unite, exemple: 'jours, dossiers, heures…' })}
        ${champ({ nom: 'categorie', label: 'Catégorie', valeur: p?.categorie, exemple: 'Charge' })}
        ${champ({ nom: 'description', label: 'Description', type: 'textarea', valeur: p?.description, span: true })}
      </div>
      ${p?.utiliseParApplication ? `<div class="banner info" style="margin-top:12px">Paramètre lu par l'application : s'il est supprimé, la valeur ${esc(p.valeurParDefaut)} s'applique.</div>` : ''}
      <div class="form-actions"><span class="grow"></span>
        <button class="btn" onclick="fermerModale()">Annuler</button>
        <button class="btn primary" id="btnParam">${p ? 'Enregistrer' : 'Ajouter'}</button></div>
    </form>`);

  $('#btnParam').onclick = async () => {
    const d = lireFormulaire($('#frmParam'));
    const erreurs = {};
    if (!p && !d.cle) erreurs.cle = 'Obligatoire.';
    if (!d.libelle) erreurs.libelle = 'Obligatoire.';
    if (d.valeur == null) erreurs.valeur = 'Obligatoire.';
    marquerErreurs($('#frmParam'), erreurs);
    if (Object.keys(erreurs).length) return;
    d.valeur = String(d.valeur);
    d.unite ??= ''; d.categorie ??= ''; d.description ??= '';
    try {
      if (p) await API.put('/referentiel/parametres/' + encodeURIComponent(p.cle), d);
      else await API.post('/referentiel/parametres', d);
      fermerModale(); toast(p ? 'Paramètre modifié.' : 'Paramètre ajouté.', 'ok'); rafraichir();
    } catch (e) { toast(e.message, 'err', 'Enregistrement impossible'); }
  };
}

async function supprimerParametre(cle) {
  const p = REF.parametres.find(x => x.cle === cle);
  const suite = p?.utiliseParApplication ? ` La valeur par défaut (${p.valeurParDefaut}) s'appliquera.` : '';
  if (!confirm(`Supprimer le paramètre « ${p?.libelle || cle} » ?${suite}`)) return;
  try {
    await API.del('/referentiel/parametres/' + encodeURIComponent(cle));
    toast('Paramètre supprimé.', 'ok'); rafraichir();
  } catch (e) { toast(e.message, 'err', 'Suppression impossible'); }
}

/* ---------- règles d'alerte ---------- */
function saisieRegle(r, modifs = {}) {
  return {
    code: r.code, libelle: r.libelle, severite: r.severiteCode, active: r.active, condition: r.condition,
    jalonReference: r.jalonReference, jalonAttendu: r.jalonAttendu, seuil: r.seuil, seuilMax: r.seuilMax,
    joursOuvres: r.joursOuvres, etapeMin: r.etapeMin, etapeMax: r.etapeMax, acteur: r.acteur, statut: r.statut,
    actionAttendue: r.actionAttendue, responsable: r.responsable, fondement: r.fondement, ordre: r.ordre,
    ...modifs,
  };
}

async function activerRegle(id, active) {
  const r = REF.regles.find(x => x.id === id);
  try {
    const res = await API.put('/referentiel/regles/' + id, saisieRegle(r, { active }));
    toast(`${r.code} ${active ? 'activée' : 'désactivée'} — ${res.alertesOuvertes} alertes ouvertes.`, 'ok');
    rafraichir(); majCompteurs();
  } catch (e) { toast(e.message, 'err'); rafraichir(); }
}

/** Champs utiles à chaque type de condition : les autres sont masqués. */
const CHAMPS_CONDITION = {
  delaidepuisjalon: ['jalonReference', 'jalonAttendu', 'seuil', 'seuilMax', 'joursOuvres'],
  echeanceproche: ['jalonReference', 'seuil'],
  consommationheures: ['seuil'],
  acteurmanquant: ['acteur'],
  statutsecondaire: ['statut'],
};

function formulaireRegle(id = null) {
  const r = id ? REF.regles.find(x => x.id === id) : null;
  const cat = REF.catalogue;
  const val = v => v == null ? null : String(v).charAt(0).toUpperCase() + String(v).slice(1);
  const prochain = 'R' + String(Math.max(0, ...REF.regles.map(x => Number(String(x.code).replace(/\D/g, '')) || 0)) + 1).padStart(2, '0');
  const etapes = cat.etapes.map(e => ({ code: e.code, libelle: `E${e.rang} · ${e.libelle}` }));

  ouvrirModale(r ? `Modifier la règle ${r.code}` : 'Nouvelle règle d\'alerte', r ? r.libelle : null, `
    <form id="frmRegle" onsubmit="return false">
      <div class="form-grid">
        ${champ({ nom: 'code', label: 'Code', requis: true, valeur: r?.code || prochain,
          aide: r ? 'Le code ne change pas : il identifie les alertes déjà levées.' : '' })}
        ${champ({ nom: 'severite', label: 'Sévérité', type: 'select', vide: false, valeur: val(r?.severiteCode) || 'Vigilance',
          options: [{ code: 'Critique', libelle: 'Critique' }, { code: 'Vigilance', libelle: 'Vigilance' }, { code: 'Information', libelle: 'Information' }] })}
        ${champ({ nom: 'libelle', label: 'Intitulé', requis: true, valeur: r?.libelle, span: true })}
        ${champ({ nom: 'condition', label: 'Condition', type: 'select', vide: false,
          valeur: val(r?.condition) || 'DelaiDepuisJalon', options: cat.conditions, span: true })}
        ${champ({ nom: 'jalonReference', label: 'Jalon de référence', type: 'select', valeur: r?.jalonReference,
          options: cat.jalons, vide: 'Choisir…' })}
        ${champ({ nom: 'jalonAttendu', label: 'Tant que ce jalon est vide', type: 'select', valeur: r?.jalonAttendu,
          options: cat.jalons, vide: 'Sans condition' })}
        ${champ({ nom: 'seuil', label: 'Seuil', type: 'number', min: 0, valeur: r?.seuil, aide: 'Jours, ou % pour la consommation des heures.' })}
        ${champ({ nom: 'seuilMax', label: 'Jusqu\'à (inclus)', type: 'number', min: 0, valeur: r?.seuilMax, aide: 'Vide : sans limite.' })}
        ${champ({ nom: 'joursOuvres', label: 'Compter en jours ouvrés', type: 'checkbox', valeur: r?.joursOuvres })}
        ${champ({ nom: 'acteur', label: 'Acteur attendu', type: 'select', valeur: val(r?.acteur), options: cat.acteurs, vide: 'Choisir…' })}
        ${champ({ nom: 'statut', label: 'Statut surveillé', type: 'select', valeur: val(r?.statut), options: cat.statuts, vide: 'Choisir…' })}
        ${champ({ nom: 'etapeMin', label: 'À partir de l\'étape', type: 'select', valeur: val(r?.etapeMin), options: etapes, vide: 'Toutes' })}
        ${champ({ nom: 'etapeMax', label: 'Jusqu\'à l\'étape', type: 'select', valeur: val(r?.etapeMax), options: etapes, vide: 'Toutes' })}
        ${champ({ nom: 'actionAttendue', label: 'Action attendue', requis: true, valeur: r?.actionAttendue, span: true })}
        ${champ({ nom: 'responsable', label: 'Responsable', valeur: r?.responsable, exemple: 'AAP, Coordination…' })}
        ${champ({ nom: 'fondement', label: 'Fondement', valeur: r?.fondement, exemple: 'Délai réglementaire…' })}
        ${champ({ nom: 'active', label: 'Règle active', type: 'checkbox', valeur: r ? r.active : true })}
      </div>
      <div class="form-actions"><span class="grow"></span>
        <button class="btn" onclick="fermerModale()">Annuler</button>
        <button class="btn primary" id="btnRegle">${r ? 'Enregistrer' : 'Créer la règle'}</button></div>
    </form>`);

  if (r) $('#f_code').disabled = true;

  const adapter = () => {
    const utiles = CHAMPS_CONDITION[$('#f_condition').value.toLowerCase()] || [];
    ['jalonReference', 'jalonAttendu', 'seuil', 'seuilMax', 'joursOuvres', 'acteur', 'statut'].forEach(nom => {
      const bloc = $(`[data-champ="${nom}"]`, $('#frmRegle')) || $(`#f_${nom}`).closest('.field');
      bloc.hidden = !utiles.includes(nom);
    });
  };
  $('#f_condition').onchange = adapter;
  adapter();

  $('#btnRegle').onclick = async () => {
    const d = lireFormulaire($('#frmRegle'));
    if (r) d.code = r.code;
    const utiles = CHAMPS_CONDITION[String(d.condition).toLowerCase()] || [];
    ['jalonReference', 'jalonAttendu', 'seuil', 'seuilMax', 'acteur', 'statut'].forEach(k => { if (!utiles.includes(k)) d[k] = null; });
    if (!utiles.includes('joursOuvres')) d.joursOuvres = false;
    d.ordre = r?.ordre ?? null;
    try {
      const res = r ? await API.put('/referentiel/regles/' + r.id, d) : await API.post('/referentiel/regles', d);
      fermerModale();
      toast(`Règle ${res.code} enregistrée — ${res.alertesOuvertes} alertes ouvertes après recalcul.`, 'ok');
      rafraichir(); majCompteurs();
    } catch (e) {
      if (e.data?.errors) marquerErreurs($('#frmRegle'), Object.fromEntries(Object.entries(e.data.errors).map(([k, v]) => [k, v[0]])));
      toast(e.message, 'err', 'Règle non enregistrée');
    }
  };
}

async function supprimerRegle(id) {
  const r = REF.regles.find(x => x.id === id);
  if (!confirm(`Supprimer la règle ${r.code} « ${r.libelle} » ? Ses ${r.alertesOuvertes} alertes ouvertes seront closes.\nPour la suspendre sans la perdre, décochez « Active ».`)) return;
  try {
    await API.del('/referentiel/regles/' + id);
    toast(`Règle ${r.code} supprimée.`, 'ok'); rafraichir(); majCompteurs();
  } catch (e) { toast(e.message, 'err', 'Suppression impossible'); }
}

/* ---------- rôles et utilisateurs ---------- */
function formulaireRole(id = null) {
  const r = id ? REF.droits.roles.find(x => x.id === id) : null;
  ouvrirModale(r ? 'Modifier le rôle' : 'Nouveau rôle', null, `
    <form id="frmRole" onsubmit="return false">
      <div class="form-grid">
        ${champ({ nom: 'nom', label: 'Nom du rôle', requis: true, valeur: r?.nom, span: true })}
        ${champ({ nom: 'description', label: 'Description', valeur: r?.description, span: true })}
      </div>
      <div class="form-actions"><span class="grow"></span>
        <button class="btn" onclick="fermerModale()">Annuler</button>
        <button class="btn primary" id="btnRole">${r ? 'Enregistrer' : 'Ajouter'}</button></div>
    </form>`);

  $('#btnRole').onclick = async () => {
    const d = lireFormulaire($('#frmRole'));
    if (!d.nom) { marquerErreurs($('#frmRole'), { nom: 'Obligatoire.' }); return; }
    try {
      if (r) await API.put('/referentiel/roles/' + r.id, { ...d, ordre: r.ordre });
      else await API.post('/referentiel/roles', { ...d, droits: {}, notifications: [] });
      fermerModale(); toast(r ? 'Rôle modifié.' : 'Rôle ajouté : réglez ses droits dans la matrice.', 'ok'); rafraichir();
    } catch (e) { toast(e.message, 'err', 'Enregistrement impossible'); }
  };
}

async function supprimerRole(id) {
  const r = REF.droits.roles.find(x => x.id === id);
  if (!confirm(`Supprimer le rôle « ${r.nom} » ?${r.membres ? ` Ses ${r.membres} utilisateurs resteront, sans rôle.` : ''}`)) return;
  try {
    await API.del('/referentiel/roles/' + id);
    toast('Rôle supprimé.', 'ok'); rafraichir();
  } catch (e) { toast(e.message, 'err', 'Suppression impossible'); }
}

function formulaireUtilisateur(id = null) {
  const u = id ? REF.droits.utilisateurs.find(x => x.id === id) : null;
  ouvrirModale(u ? 'Modifier l\'utilisateur' : 'Nouvel utilisateur', null, `
    <form id="frmUtil" onsubmit="return false">
      <div class="form-grid">
        ${champ({ nom: 'nom', label: 'Nom', requis: true, valeur: u?.nom })}
        ${champ({ nom: 'prenom', label: 'Prénom', valeur: u?.prenom })}
        ${champ({ nom: 'email', label: 'Courriel', type: 'email', requis: true, valeur: u?.email })}
        ${champ({ nom: 'roleAccesId', label: 'Rôle', type: 'select', valeur: u?.roleAccesId,
          options: REF.droits.roles.map(r => ({ code: r.id, libelle: r.nom })), vide: 'Sans rôle' })}
        ${champ({ nom: 'intervenantId', label: 'Fiche intervenant', type: 'select', valeur: u?.intervenantId,
          options: optionsIntervenants(), vide: 'Aucune' })}
        ${champ({ nom: 'actif', label: 'Compte actif', type: 'checkbox', valeur: u ? u.actif : true })}
      </div>
      <div class="form-actions"><span class="grow"></span>
        <button class="btn" onclick="fermerModale()">Annuler</button>
        <button class="btn primary" id="btnUtil">${u ? 'Enregistrer' : 'Ajouter'}</button></div>
    </form>`);

  $('#btnUtil').onclick = async () => {
    const d = lireFormulaire($('#frmUtil'));
    const erreurs = {};
    if (!d.nom) erreurs.nom = 'Obligatoire.';
    if (!d.email) erreurs.email = 'Obligatoire.';
    marquerErreurs($('#frmUtil'), erreurs);
    if (Object.keys(erreurs).length) return;
    try {
      if (u) await API.put('/referentiel/utilisateurs/' + u.id, d);
      else await API.post('/referentiel/utilisateurs', d);
      fermerModale(); toast(u ? 'Utilisateur modifié.' : 'Utilisateur ajouté.', 'ok'); rafraichir();
    } catch (e) { toast(e.message, 'err', 'Enregistrement impossible'); }
  };
}

async function supprimerUtilisateur(id) {
  const u = REF.droits.utilisateurs.find(x => x.id === id);
  if (!confirm(`Supprimer l'utilisateur ${u.nomComplet} ?`)) return;
  try {
    await API.del('/referentiel/utilisateurs/' + id);
    toast('Utilisateur supprimé.', 'ok'); rafraichir();
  } catch (e) { toast(e.message, 'err', 'Suppression impossible'); }
}

function formulaireGrille() {
  ouvrirModale('Nouvelle grille tarifaire',
    "La grille précédente sera close la veille de la date d'effet.", `
    <form id="frmGrille" onsubmit="return false">
      <div class="form-grid">
        ${champ({ nom: 'code', label: 'Code', requis: true, exemple: '2027' })}
        ${champ({ nom: 'libelle', label: 'Libellé', requis: true, span: true,
          exemple: 'Grille applicable aux parcours démarrés à partir du…' })}
        ${champ({ nom: 'dateEffet', label: "Date d'effet", type: 'date', requis: true })}
        ${champ({ nom: 'forfaitArchitecture', label: 'Forfait architecture (€)', type: 'number', pas: '0.01', valeur: 350 })}
        ${champ({ nom: 'tarifHoraireIndividuel', label: 'Individuel (€/h)', type: 'number', pas: '0.01', valeur: 75 })}
        ${champ({ nom: 'tarifHoraireCollectif', label: 'Collectif (€/h)', type: 'number', pas: '0.01', valeur: 40 })}
        ${champ({ nom: 'tarifHoraireComplementFormatif', label: 'Compléments (€/h)', type: 'number', pas: '0.01', valeur: 30 })}
        ${champ({ nom: 'fraisJury', label: 'Frais de jury (€)', type: 'number', pas: '0.01', valeur: 350 })}
        ${champ({ nom: 'plafondHeuresIndividuel', label: 'Plafond individuel (h)', type: 'number', valeur: 30 })}
        ${champ({ nom: 'plafondHeuresCollectif', label: 'Plafond collectif (h)', type: 'number', valeur: 20 })}
        ${champ({ nom: 'plafondHeuresComplementFormatif', label: 'Plafond compléments (h)', type: 'number', valeur: 70 })}
        ${champ({ nom: 'plafondMontantTotal', label: 'Plafond mobilisable (€)', type: 'number', pas: '0.01', valeur: 5850 })}
        ${champ({ nom: 'coutHoraireIntervenant', label: 'Coût intervenant (€/h)', type: 'number', pas: '0.01', valeur: 30 })}
        ${champ({ nom: 'heuresArchitectureParDossier', label: 'Heures architecture / dossier', type: 'number', pas: '0.5', valeur: 3 })}
        ${champ({ nom: 'coutHoraireArchitecte', label: 'Coût architecte (€/h)', type: 'number', pas: '0.01', valeur: 35 })}
      </div>
      <div class="form-actions"><span class="grow"></span>
        <button class="btn" onclick="fermerModale()">Annuler</button>
        <button class="btn primary" id="btnGrille">Créer la grille</button></div>
    </form>`);

  $('#btnGrille').onclick = async () => {
    const d = lireFormulaire($('#frmGrille'));
    if (!d.code || !d.libelle || !d.dateEffet) { toast('Code, libellé et date d\'effet sont obligatoires.', 'err'); return; }
    try {
      await API.post('/referentiel/grilles', d);
      fermerModale(); toast('Grille créée.', 'ok'); rafraichir();
    } catch (e) { toast(e.message, 'err', 'Création impossible'); }
  };
}

async function reaffecterGrilles() {
  try {
    const r = await API.post('/referentiel/grilles/reaffecter');
    toast(`${r.modifies} dossier${r.modifies > 1 ? 's' : ''} réaffecté${r.modifies > 1 ? 's' : ''} sur ${r.examines} examinés. ` +
      `${r.ignoresCarFactures} ignoré${r.ignoresCarFactures > 1 ? 's' : ''} car déjà facturé${r.ignoresCarFactures > 1 ? 's' : ''}.`,
      'ok', 'Réaffectation terminée');
    rafraichir();
  } catch (e) { toast(e.message, 'err', 'Réaffectation impossible'); }
}
