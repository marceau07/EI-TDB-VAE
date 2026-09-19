/* =========================================================================
   PILOTAGE VAE — EI GROUPE
   Couche de présentation. Toutes les données viennent de l'API : aucun calcul
   métier n'est fait ici, pour qu'un chiffre affiché en direction soit
   exactement celui que voit l'AAP dans son plan d'action.
   ========================================================================= */

/* ---------------------------------------------------------------- utilitaires */
const $ = (s, r) => (r || document).querySelector(s);
const $$ = (s, r) => [...(r || document).querySelectorAll(s)];
const esc = s => String(s ?? '').replace(/[&<>"']/g, c =>
  ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

const fmtDate = s => s ? new Date(s + (s.length === 10 ? 'T00:00:00' : ''))
  .toLocaleDateString('fr-FR', { day: '2-digit', month: 'short', year: '2-digit' }) : '—';
const fmtDateLongue = s => s ? new Date(s + (s.length === 10 ? 'T00:00:00' : ''))
  .toLocaleDateString('fr-FR', { day: '2-digit', month: 'long', year: 'numeric' }) : '—';
const eur = n => (n == null || isNaN(n)) ? '—' : Math.round(n).toLocaleString('fr-FR') + ' €';
const eurK = n => !n ? '0 €' : (Math.abs(n) >= 10000
  ? (n / 1000).toLocaleString('fr-FR', { maximumFractionDigits: 1 }) + ' k€'
  : Math.round(n).toLocaleString('fr-FR') + ' €');
const nf = n => (n == null || isNaN(n)) ? '—' : Number(n).toLocaleString('fr-FR', { maximumFractionDigits: 1 });
const pct = v => v == null ? '—' : Math.round(v * 100) + ' %';
const ratio = (n, d) => !d ? '—' : Math.round(n / d * 100) + ' %';
const jours = s => s ? Math.round((Date.now() - new Date(s + 'T00:00:00')) / 864e5) : null;

const AVC = ['#0E6D7D', '#009CB7', '#1B4C61', '#0A5766', '#3C8697', '#146E86'];
const avat = n => { let h = 0; for (const c of (n || '?')) h = (h * 31 + c.charCodeAt(0)) | 0; return AVC[Math.abs(h) % AVC.length]; };
const inits = n => (n || '?').split(/[\s-]+/).filter(Boolean).slice(0, 2).map(w => w[0]).join('').toUpperCase();
/** Compare une valeur d'enumeration sans dependre de la casse de serialisation. */
const estValeur = (a, b) => String(a ?? '').toLowerCase() === String(b ?? '').toLowerCase();

const who = (nom, sub, sm) => nom
  ? `<span class="who"><span class="av" style="background:${avat(nom)}${sm ? ';width:21px;height:21px;font-size:8.5px' : ''}">${esc(inits(nom))}</span><span class="who-t"><b>${esc(nom)}</b>${sub ? `<span>${esc(sub)}</span>` : ''}</span></span>`
  : '<span class="chip c-warn">non affecté</span>';

/* ---------------------------------------------------------------- client API */
const API = {
  async requete(chemin, options = {}) {
    const r = await fetch('/api' + chemin, {
      headers: options.body instanceof FormData ? {} : { 'Content-Type': 'application/json' },
      ...options,
    });

    if (r.status === 204) return null;

    const texte = await r.text();
    let data = null;
    try { data = texte ? JSON.parse(texte) : null; } catch { data = texte; }

    if (!r.ok) {
      // Les erreurs de validation d'ASP.NET Core arrivent en ProblemDetails :
      // on remonte le premier message utile plutôt qu'un code HTTP nu.
      const detail = data?.errors
        ? Object.values(data.errors).flat().join(' ')
        : data?.message || data?.detail || data?.title || `Erreur ${r.status}`;
      const err = new Error(detail);
      err.statut = r.status;
      err.data = data;
      throw err;
    }

    return data;
  },
  get: (c) => API.requete(c),
  post: (c, b) => API.requete(c, { method: 'POST', body: b instanceof FormData ? b : JSON.stringify(b ?? {}) }),
  put: (c, b) => API.requete(c, { method: 'PUT', body: JSON.stringify(b ?? {}) }),
  del: (c) => API.requete(c, { method: 'DELETE' }),
};

/* ---------------------------------------------------------------- messages */
function toast(message, type = 'ok', titre = null) {
  const el = document.createElement('div');
  el.className = 'toast ' + type;
  el.innerHTML = (titre ? `<b>${esc(titre)}</b>` : '') + esc(message);
  $('#toasts').append(el);
  setTimeout(() => { el.style.opacity = '0'; setTimeout(() => el.remove(), 250); },
    type === 'err' ? 8000 : 4200);
}

const chargement = (msg = 'Chargement…') => `<div class="loading">${esc(msg)}</div>`;
const vide = (msg) => `<div class="empty">${esc(msg)}</div>`;

/* ---------------------------------------------------------------- primitives */
function kpi(lab, val, sub, ton) {
  return `<div class="kpi ${ton || ''}"><span class="k-bar"></span>
    <span class="k-lab">${esc(lab)}</span><span class="k-val">${val}</span>
    ${sub ? `<span class="k-sub">${esc(sub)}</span>` : ''}</div>`;
}
function carte(titre, corps, indice, cls) {
  return `<div class="card ${cls === 'tight' ? '' : (cls || '')}">
    ${titre || indice ? `<div class="card-h"><h3>${esc(titre || '')}</h3>${indice ? `<span class="hint">${esc(indice)}</span>` : ''}</div>` : ''}
    <div class="card-b${cls === 'tight' ? ' tight' : ''}">${corps}</div></div>`;
}
/**
 * Encart « À savoir » : raccourcis vers les écrans liés. Chaque élément est
 * soit un texte, soit [libellé, vue] pour un lien interne, soit [libellé, null, js]
 * pour un lien qui exécute une action.
 */
function aSavoir(elements, bas) {
  const html = elements.filter(Boolean).map(e => {
    if (typeof e === 'string') return `<span>${e}</span>`;
    const [lib, vue, action] = e;
    return vue
      ? `<a href="#${esc(vue)}" onclick="aller('${esc(vue)}');return false;">${esc(lib)}</a>`
      : `<button class="lien" onclick="${esc(action)}">${esc(lib)}</button>`;
  }).join('<span class="sep">·</span>');
  return `<div class="a-savoir${bas ? ' bas' : ''}"><b>À savoir</b>${html}</div>`;
}
function section(t, note) {
  return `<div class="sect-h"><h2>${esc(t)}</h2><span class="rule"></span>${note ? `<span class="note">${esc(note)}</span>` : ''}</div>`;
}
function barres(lignes, opt = {}) {
  if (!lignes.length) return vide('Aucune donnée sur ce périmètre.');
  const max = Math.max(...lignes.map(l => l.v), 1);
  return `<div class="hbars${opt.large ? ' wide' : ''}">` + lignes.map(l => `
    <div class="hb"><span class="hb-l" title="${esc(l.l)}">${esc(l.l)}</span>
    <span class="hb-t"><i class="${l.cls || ''}" style="width:${Math.max(1, l.v / max * 100)}%"></i>${l.repere != null ? `<u style="left:${Math.min(99, l.repere / max * 100)}%"></u>` : ''}</span>
    <span class="hb-n">${l.n != null ? esc(l.n) : nf(l.v)}</span></div>`).join('') + '</div>';
}
function courbe(valeurs, w = 600, h = 62) {
  if (!valeurs.length) return '';
  const max = Math.max(...valeurs, 1), n = valeurs.length;
  const x = i => (i / Math.max(1, n - 1)) * (w - 6) + 3;
  const y = v => h - 5 - (v / max) * (h - 12);
  const pts = valeurs.map((v, i) => [x(i), y(v)]);
  const ligne = pts.map((p, i) => (i ? 'L' : 'M') + p[0].toFixed(1) + ' ' + p[1].toFixed(1)).join(' ');
  const aire = ligne + ` L${x(n - 1).toFixed(1)} ${h} L${x(0).toFixed(1)} ${h} Z`;
  const dernier = pts[n - 1];
  return `<svg class="spark" viewBox="0 0 ${w} ${h}" preserveAspectRatio="none" aria-hidden="true">
    <defs><linearGradient id="g${w}" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="var(--cyan)" stop-opacity=".28"/><stop offset="1" stop-color="var(--cyan)" stop-opacity="0"/>
    </linearGradient></defs>
    <line x1="0" y1="${h - 5}" x2="${w}" y2="${h - 5}" stroke="var(--line)" stroke-width="1"/>
    <path d="${aire}" fill="url(#g${w})"/>
    <path d="${ligne}" fill="none" stroke="var(--teal)" stroke-width="1.8" stroke-linejoin="round" vector-effect="non-scaling-stroke"/>
    <circle cx="${dernier[0].toFixed(1)}" cy="${dernier[1].toFixed(1)}" r="3" fill="var(--surface)" stroke="var(--teal)" stroke-width="2"/>
  </svg>`;
}
const lienExterne = (url, texte) => url
  ? `<a class="ext" href="${esc(url)}" target="_blank" rel="noopener">${esc(texte)}<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6M15 3h6v6M10 14 21 3"/></svg></a>`
  : '<span style="color:var(--faint)">—</span>';

/* ---------------------------------------------------------------- tiroir et boîte */
function ouvrirTiroir(html) {
  $('#drawer').innerHTML = html;
  $('#drawer').classList.add('on');
  $('#scrim').classList.add('on');
  document.body.classList.add('locked');
  $('#drawer').querySelector('button')?.focus();
}
function fermerTiroir() {
  $('#drawer').classList.remove('on');
  $('#scrim').classList.remove('on');
  document.body.classList.remove('locked');
}
function ouvrirModale(titre, sousTitre, corps) {
  $('#modalBox').innerHTML = `
    <div class="modal-h">
      <div style="flex:1;min-width:0"><h2>${esc(titre)}</h2>${sousTitre ? `<p>${esc(sousTitre)}</p>` : ''}</div>
      <button class="btn ghost" onclick="fermerModale()" aria-label="Fermer">✕</button>
    </div>
    <div class="modal-b">${corps}</div>`;
  $('#modal').classList.add('on');
  document.body.classList.add('locked');
  $('#modalBox').querySelector('input,select,textarea')?.focus();
}
function fermerModale() {
  $('#modal').classList.remove('on');
  if (!$('#drawer').classList.contains('on')) document.body.classList.remove('locked');
}
$('#scrim').onclick = fermerTiroir;
$('#modal').onclick = e => { if (e.target.id === 'modal') fermerModale(); };
document.addEventListener('keydown', e => {
  if (e.key !== 'Escape') return;
  if ($('#modal').classList.contains('on')) fermerModale();
  else fermerTiroir();
});

/* ---------------------------------------------------------------- formulaires */
/** Construit un champ. `type` : text, number, date, select, textarea, checkbox. */
function champ(o) {
  const id = 'f_' + o.nom;
  const requis = o.requis ? '<span class="req" title="obligatoire">*</span>' : '';

  if (o.type === 'checkbox') {
    return `<div class="field${o.span ? ' span2' : ''}"><label class="check">
      <input type="checkbox" id="${id}" name="${o.nom}" ${o.valeur ? 'checked' : ''}>
      <span>${esc(o.label)}${o.aide ? `<br><span class="hint">${esc(o.aide)}</span>` : ''}</span>
    </label></div>`;
  }

  let saisie;
  if (o.type === 'select') {
    const options = (o.options || []).map(x => {
      const v = x.valeur ?? x.code ?? x;
      const l = x.libelle ?? x.nom ?? x;
      return `<option value="${esc(v)}"${String(v) === String(o.valeur ?? '') ? ' selected' : ''}>${esc(l)}</option>`;
    }).join('');
    saisie = `<select id="${id}" name="${o.nom}">${o.vide !== false ? `<option value="">${esc(o.vide || '—')}</option>` : ''}${options}</select>`;
  } else if (o.type === 'textarea') {
    saisie = `<textarea id="${id}" name="${o.nom}" placeholder="${esc(o.exemple || '')}">${esc(o.valeur ?? '')}</textarea>`;
  } else {
    const attrs = [
      o.min != null ? `min="${o.min}"` : '', o.max != null ? `max="${o.max}"` : '',
      o.pas ? `step="${o.pas}"` : '', o.exemple ? `placeholder="${esc(o.exemple)}"` : '',
    ].filter(Boolean).join(' ');
    saisie = `<input type="${o.type || 'text'}" id="${id}" name="${o.nom}" value="${esc(o.valeur ?? '')}" ${attrs}>`;
  }

  return `<div class="field${o.span ? ' span2' : ''}" data-champ="${o.nom}">
    <label for="${id}">${esc(o.label)}${requis}</label>
    ${saisie}
    ${o.aide ? `<span class="hint">${esc(o.aide)}</span>` : ''}
    <span class="err" hidden></span></div>`;
}

/**
 * Menu déroulant à choix multiples. `options` : [{ code, libelle, sous }] ;
 * `valeurs` : codes cochés. La sélection se relit avec lireFormulaire, sous
 * forme de tableau de nombres.
 */
function champMulti(o) {
  const choisis = new Set((o.valeurs || []).map(String));
  const opts = o.options || [];
  const resume = opts.filter(x => choisis.has(String(x.code))).map(x => x.libelle);
  return `<div class="field${o.span ? ' span2' : ''}" data-champ="${o.nom}">
    <label>${esc(o.label)}</label>
    <div class="multi" data-multi="${o.nom}" data-vide="${esc(o.vide || 'Aucun')}">
      <button type="button" class="multi-btn" onclick="basculerMulti(this)" aria-haspopup="listbox">
        <span class="multi-resume">${resumeMulti(resume, o.vide)}</span>
        <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><path d="m6 9 6 6 6-6"/></svg>
      </button>
      <div class="multi-panneau" hidden>
        ${opts.length > 8 ? '<input class="multi-filtre" placeholder="Filtrer…" oninput="filtrerMulti(this)">' : ''}
        ${opts.length ? opts.map(x => `<label class="multi-opt" data-lib="${esc(String(x.libelle + ' ' + (x.sous || '')).toLowerCase())}">
          <input type="checkbox" value="${esc(x.code)}"${choisis.has(String(x.code)) ? ' checked' : ''} onchange="resumerMulti(this.closest('.multi'))">
          <span>${esc(x.libelle)}</span>${x.sous ? `<span class="code">${esc(x.sous)}</span>` : ''}</label>`).join('')
          : `<div class="multi-vide">${esc(o.siVide || 'Aucun choix disponible.')}</div>`}
      </div>
    </div>
    ${o.aide ? `<span class="hint">${esc(o.aide)}</span>` : ''}
    <span class="err" hidden></span></div>`;
}
function resumeMulti(libelles, vide) {
  if (!libelles.length) return `<span class="multi-placeholder">${esc(vide || 'Aucun')}</span>`;
  return libelles.length <= 2 ? esc(libelles.join(', ')) : `${esc(libelles[0])} <b>+ ${libelles.length - 1}</b>`;
}
function basculerMulti(bouton) {
  const panneau = bouton.nextElementSibling;
  const ouvrir = panneau.hidden;
  $$('.multi-panneau').forEach(p => { p.hidden = true; });
  panneau.hidden = !ouvrir;
  if (ouvrir) panneau.querySelector('.multi-filtre')?.focus();
}
function filtrerMulti(champ) {
  const q = champ.value.toLowerCase();
  $$('.multi-opt', champ.parentElement).forEach(l => { l.hidden = q && !l.dataset.lib.includes(q); });
}
function resumerMulti(multi) {
  const libelles = $$('input[type=checkbox]:checked', multi).map(c => c.nextElementSibling.textContent);
  multi.querySelector('.multi-resume').innerHTML = resumeMulti(libelles, multi.dataset.vide);
  multi.dispatchEvent(new Event('change', { bubbles: true }));
}
/** Valeurs cochées d'un menu à choix multiples. */
const valeursMulti = (nom, racine) =>
  $$(`[data-multi="${nom}"] input[type=checkbox]:checked`, racine).map(c => Number(c.value));
document.addEventListener('click', e => {
  if (!e.target.closest('.multi')) $$('.multi-panneau').forEach(p => { p.hidden = true; });
});

/** Relit un formulaire. Les champs vides deviennent null, jamais chaîne vide. */
function lireFormulaire(racine) {
  const out = {};
  $$('input,select,textarea', racine).forEach(el => {
    if (!el.name) return;
    if (el.type === 'checkbox') { out[el.name] = el.checked; return; }
    const v = el.value.trim();
    if (v === '') { out[el.name] = null; return; }
    out[el.name] = el.type === 'number' ? Number(v) : v;
  });
  $$('[data-multi]', racine).forEach(m => {
    out[m.dataset.multi] = $$('input[type=checkbox]:checked', m).map(c => Number(c.value));
  });
  return out;
}

function marquerErreurs(racine, erreurs) {
  $$('.field', racine).forEach(f => {
    f.classList.remove('invalid');
    const e = $('.err', f);
    if (e) { e.hidden = true; e.textContent = ''; }
  });
  Object.entries(erreurs || {}).forEach(([nom, message]) => {
    const f = $(`[data-champ="${nom}"]`, racine);
    if (!f) return;
    f.classList.add('invalid');
    const e = $('.err', f);
    if (e) { e.textContent = message; e.hidden = false; }
  });
}

/* ---------------------------------------------------------------- état global */
const ETAT = {
  filtres: {},
  nomenclatures: null,
  intervenants: [],
  certifications: [],
  etapes: [],
};

function paramsFiltres(extra = {}) {
  const p = new URLSearchParams();
  const f = { ...ETAT.filtres, ...extra };
  Object.entries(f).forEach(([k, v]) => { if (v !== '' && v != null) p.set(k, v); });
  const s = p.toString();
  return s ? '?' + s : '';
}

/* ---------------------------------------------------------------- navigation */
const IC = {
  home: 'M3 10.5 12 3l9 7.5M5.5 9.5V20h13V9.5',
  bell: 'M12 3a5.5 5.5 0 0 0-5.5 5.5c0 5-2 6.5-2 6.5h15s-2-1.5-2-6.5A5.5 5.5 0 0 0 12 3ZM10 20a2 2 0 0 0 4 0',
  flow: 'M4 6h6M4 12h10M4 18h5M16 4l4 4-4 4M14 14l4 4-4 4',
  users: 'M16 20v-2a4 4 0 0 0-4-4H7a4 4 0 0 0-4 4v2M9.5 10a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7ZM21 20v-2a4 4 0 0 0-3-3.87M16 3.13A4 4 0 0 1 16 11',
  clock: 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18ZM12 7v5l3.5 2',
  cap: 'M12 3 2 8l10 5 10-5-10-5ZM5 10.5V16c0 1.7 3.1 3 7 3s7-1.3 7-3v-5.5',
  euro: 'M17 5.5A6.5 6.5 0 0 0 7.2 9M17 18.5A6.5 6.5 0 0 1 7.2 15M4 10.5h9M4 13.5h9',
  gauge: 'M12 21a9 9 0 1 1 0-18 9 9 0 0 1 0 18ZM12 12l4-4',
  shield: 'M12 3 5 6v5.5c0 4 3 7.5 7 9.5 4-2 7-5.5 7-9.5V6l-7-3ZM9 12l2 2 4-4',
  grid: 'M4 4h7v7H4zM13 4h7v7h-7zM4 13h7v7H4zM13 13h7v7h-7z',
  book: 'M4 5.5A2.5 2.5 0 0 1 6.5 3H20v15H6.5A2.5 2.5 0 0 0 4 20.5v-15ZM4 20.5A2.5 2.5 0 0 1 6.5 18H20v3H6.5A2.5 2.5 0 0 1 4 20.5Z',
  team: 'M3 20v-1.5A3.5 3.5 0 0 1 6.5 15h4A3.5 3.5 0 0 1 14 18.5V20M8.5 12a3.25 3.25 0 1 0 0-6.5 3.25 3.25 0 0 0 0 6.5ZM17 20v-1.5a3.5 3.5 0 0 0-2-3.16M16 5.75A3.25 3.25 0 0 1 16 12',
  plug: 'M9 3v6M15 3v6M6 9h12v3a6 6 0 0 1-12 0V9ZM12 18v3',
  screen: 'M3 5h18v11H3zM8 20h8M12 16v4M10 8.5l4 2-4 2z',
  mail: 'M3 6.5A1.5 1.5 0 0 1 4.5 5h15A1.5 1.5 0 0 1 21 6.5v11a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 17.5v-11ZM3.5 6l8.5 7 8.5-7',
};

const NAV = [
  { grp: 'Synthèse', items: [{ id: 'direction', lab: 'Tableau de bord', ic: 'home' }] },
  {
    grp: 'Opérationnel', niveau: 'N1', items: [
      { id: 'alertes', lab: 'Alertes', ic: 'bell' },
      { id: 'notifications', lab: 'Notifications', ic: 'mail' },
      { id: 'pipeline', lab: 'Pipeline', ic: 'flow' },
      { id: 'candidats', lab: 'Candidats', ic: 'users' },
    ]
  },
  {
    grp: 'Management', niveau: 'N2', items: [
      { id: 'delais', lab: 'Délais & flux', ic: 'clock' },
      { id: 'charge', lab: 'Charge des équipes', ic: 'team' },
      { id: 'heures', lab: 'Heures', ic: 'gauge' },
      { id: 'qualite', lab: 'Qualité & conformité', ic: 'shield' },
    ]
  },
  {
    grp: 'Stratégique', niveau: 'N3', items: [
      { id: 'finance', lab: 'Pilotage financier', ic: 'euro' },
      { id: 'certifications', lab: 'Certifications', ic: 'cap' },
      { id: 'collective', lab: 'VAE collective', ic: 'grid' },
    ]
  },
  {
    grp: 'Administration', items: [
      { id: 'reseau', lab: 'Réseau VAE', ic: 'users' },
      { id: 'elearning', lab: 'E-learning', ic: 'screen' },
      { id: 'integrations', lab: 'Intégrations', ic: 'plug' },
      { id: 'regles', lab: 'Règles de gestion', ic: 'book' },
    ]
  },
];

const TITRES = {
  direction: ['Tableau de bord direction', 'Activité, pipeline, performance, finance, ressources et alertes.'],
  alertes: ['Alertes', 'Dossiers qui exigent une action, classés par sévérité.'],
  notifications: ['Notifications', 'Créations de candidats, affectations et parcours prescrits.'],
  pipeline: ['Pipeline des parcours', 'Position de chaque candidat sur les douze étapes du parcours.'],
  candidats: ['Base candidats', 'Une fiche par candidat.'],
  delais: ['Délais & flux', 'Taux de passage entre étapes et délais observés.'],
  charge: ['Charge des équipes', 'Charge des AAP et des accompagnateurs, capacité disponible, aide à l\'affectation.'],
  heures: ['Pilotage des heures', 'Heures prescrites, réalisées et restantes, par nature et par plafond.'],
  qualite: ['Qualité & conformité', 'Complétude des dossiers, résultats de jury et sorties.'],
  finance: ['Pilotage financier', 'Chiffre d\'affaires, marge et rentabilité, par grille tarifaire.'],
  certifications: ['Référentiel certifications', 'Fiches France Compétences, blocs, réseau habilité et couverture.'],
  collective: ['VAE collective', 'Projets d\'entreprise, cohortes et suivi collectif.'],
  reseau: ['Réseau VAE', 'AAP, accompagnateurs et experts métiers.'],
  elearning: ['E-learning', 'Catalogue EI Académie, compléments formatifs et suivi des modules par dossier.'],
  integrations: ['Intégrations', 'France Compétences, France VAE, site web et SharePoint.'],
  regles: ['Règles de gestion', 'Grilles tarifaires, paramètres, règles d\'alerte et droits d\'accès.'],
};

const SANS_FILTRES = new Set(['certifications', 'collective', 'reseau', 'integrations', 'regles', 'notifications', 'elearning']);
let VUE = 'direction';

let COMPTEURS = { alertes: 0, notifications: 0 };

function construireNav(alertesCritiques, notificationsNonLues) {
  if (alertesCritiques != null) COMPTEURS.alertes = alertesCritiques;
  if (notificationsNonLues != null) COMPTEURS.notifications = notificationsNonLues;
  $('#nav').innerHTML = NAV.map(g => `
    <div class="navgrp">
      <div class="navgrp-h">${g.niveau ? `<i>${g.niveau}</i>` : ''}<span>${esc(g.grp)}</span></div>
      ${g.items.map(it => `
        <button class="navbtn" data-v="${it.id}" ${it.id === VUE ? 'aria-current="page"' : ''}>
          <svg class="ic" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"><path d="${IC[it.ic]}"/></svg>
          <span>${esc(it.lab)}</span>
          ${it.id === 'alertes' && COMPTEURS.alertes ? `<span class="cnt hot">${COMPTEURS.alertes}</span>` : ''}
          ${it.id === 'notifications' && COMPTEURS.notifications ? `<span class="cnt">${COMPTEURS.notifications}</span>` : ''}
        </button>`).join('')}
    </div>`).join('');
  $$('.navbtn', $('#nav')).forEach(b => b.onclick = () => aller(b.dataset.v));
}

const VUES = {};

async function aller(v) {
  VUE = v;
  $$('.navbtn', $('#nav')).forEach(b =>
    b.dataset.v === v ? b.setAttribute('aria-current', 'page') : b.removeAttribute('aria-current'));

  const [titre, sous] = TITRES[v] || [v, ''];
  $('#vTitle').textContent = titre;
  $('#vSub').textContent = sous;
  $('#filters').style.display = SANS_FILTRES.has(v) ? 'none' : '';
  $('#vActions').innerHTML = '';
  location.hash = v;
  window.scrollTo({ top: 0, behavior: 'instant' });

  $('#vue').innerHTML = chargement();
  try {
    await VUES[v]();
  } catch (e) {
    console.error(e);
    $('#vue').innerHTML = `<div class="banner"><b>Impossible d'afficher cette vue.</b> ${esc(e.message)}</div>`;
  }
}

function rafraichir() { return aller(VUE); }

/** Met à jour les pastilles du menu après une action qui crée ou lève des alertes et notifications. */
async function majCompteurs() {
  try {
    const [s, n] = await Promise.all([API.get('/parcours/synthese'), API.get('/notifications/etat')]);
    construireNav(s.alertesCritiques, n.nonLues);
  } catch { /* les pastilles sont un confort */ }
}

/* ---------------------------------------------------------------- thème */
(function theme() {
  const b = $('#themeBtn');
  const modes = ['auto', 'light', 'dark'];
  const libelles = { auto: 'auto', light: 'clair', dark: 'sombre' };
  let i = modes.indexOf(localStorage.getItem('theme') || 'auto');
  if (i < 0) i = 0;

  const appliquer = () => {
    const m = modes[i];
    if (m === 'auto') document.documentElement.removeAttribute('data-theme');
    else document.documentElement.setAttribute('data-theme', m);
    b.textContent = 'Thème ' + libelles[m];
    localStorage.setItem('theme', m);
  };

  b.onclick = () => { i = (i + 1) % 3; appliquer(); };
  appliquer();
})();

/* ---------------------------------------------------------------- démarrage */
async function demarrer() {
  const sante = await API.get('/sante').catch(() => null);
  if (!sante || sante.statut !== 'ok') {
    document.body.innerHTML = `<div style="padding:60px 24px;max-width:640px;margin:0 auto;font-family:Poppins,sans-serif">
      <h1 style="font-size:20px;margin-bottom:10px">Base de données injoignable</h1>
      <p style="font-size:14px;line-height:1.6;color:#5F787F">
        L'application a démarré mais ne peut pas joindre PostgreSQL. Vérifiez la chaîne de connexion
        <code>ConnectionStrings:Postgres</code> et que le serveur est démarré, puis rechargez la page.</p></div>`;
    return;
  }

  // Les listes de valeurs et le référentiel sont chargés une fois : ils
  // alimentent les filtres et tous les formulaires de saisie.
  const [nomenclatures, etapes, intervenants, certifications] = await Promise.all([
    API.get('/referentiel/nomenclatures'),
    API.get('/referentiel/etapes'),
    API.get('/intervenants'),
    API.get('/certifications?avecDemandes=true'),
  ]);

  ETAT.nomenclatures = nomenclatures;
  ETAT.etapes = etapes.filter(e => e.code !== 'Sortie');
  ETAT.intervenants = intervenants;
  ETAT.certifications = certifications;

  remplirFiltres();
  const [synthese, etatNotif] = await Promise.all([
    API.get('/parcours/synthese'), API.get('/notifications/etat'),
  ]);
  construireNav(synthese.alertesCritiques, etatNotif.nonLues);
  $('#railInfo').innerHTML =
    `<b>${synthese.dossiersActifs}</b> dossiers actifs<br>${ETAT.certifications.length} certifications actives`;

  // Lien direct vers une fiche, utilisé dans les courriels : #fiche-123.
  const initiale = location.hash.slice(1);
  const fiche = /^fiche-(\d+)$/.exec(initiale);
  await aller(TITRES[initiale] ? initiale : (fiche ? 'candidats' : 'direction'));
  if (fiche) ouvrirFiche(Number(fiche[1]));
}

function remplirFiltres() {
  const opt = (v, l, sel) => `<option value="${esc(v)}"${sel ? ' selected' : ''}>${esc(l)}</option>`;

  const aaps = ETAT.intervenants.filter(i => estValeur(i.type, 'ArchitecteAccompagnateurParcours'));
  const accs = ETAT.intervenants.filter(i => estValeur(i.type, 'Accompagnateur'));

  $('#fAap').innerHTML = opt('', 'Tous les AAP') + aaps.map(i => opt(i.id, i.nomComplet)).join('');
  $('#fAcc').innerHTML = opt('', 'Tous') + accs.map(i => opt(i.id, i.nomComplet)).join('');
  $('#fCert').innerHTML = opt('', 'Toutes') +
    ETAT.certifications.map(c => opt(c.id, c.abrege || c.intitule)).join('');
  $('#fFin').innerHTML = opt('', 'Tous') +
    (ETAT.nomenclatures.dispositifs || []).map(d => opt(d.code, d.libelle)).join('');
  $('#fEtape').innerHTML = opt('', 'Toutes') +
    ETAT.etapes.map(e => opt(e.code, `E${e.rang} · ${e.libelle}`)).join('');

  // Les territoires viennent des dossiers eux-mêmes : inutile de proposer des
  // régions où le service n'a aucun candidat.
  API.get('/parcours').then(lignes => {
    const regions = [...new Set(lignes.map(l => l.region).filter(Boolean))].sort((a, b) => a.localeCompare(b, 'fr'));
    $('#fTerr').innerHTML = opt('', 'Tous') + regions.map(r => opt(r, r)).join('');
  }).catch(() => { $('#fTerr').innerHTML = opt('', 'Tous'); });

  const lier = (id, cle) => $('#' + id).addEventListener('change', e => {
    ETAT.filtres[cle] = e.target.value;
    rafraichir();
  });

  lier('fPeriode', 'mois'); lier('fAap', 'aapId'); lier('fCert', 'certificationId');
  lier('fAcc', 'accompagnateurId'); lier('fFin', 'financeur'); lier('fTerr', 'region');
  lier('fEtape', 'etape');

  let minuterie;
  $('#fQ').addEventListener('input', e => {
    clearTimeout(minuterie);
    minuterie = setTimeout(() => { ETAT.filtres.recherche = e.target.value; rafraichir(); }, 350);
  });

  $('#resetF').onclick = () => {
    ETAT.filtres = {};
    ['fAap', 'fCert', 'fAcc', 'fFin', 'fTerr', 'fEtape', 'fQ', 'fPeriode'].forEach(i => $('#' + i).value = '');
    rafraichir();
  };
}

window.addEventListener('hashchange', () => {
  const v = location.hash.slice(1);
  const fiche = /^fiche-(\d+)$/.exec(v);
  if (fiche) { ouvrirFiche(Number(fiche[1])); return; }
  if (v && v !== VUE && TITRES[v]) aller(v);
});
