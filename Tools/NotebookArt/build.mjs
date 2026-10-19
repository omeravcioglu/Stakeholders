// Renders the "founder's notebook" UI art for Unity UI Toolkit.
//
//   node Tools/NotebookArt/build.mjs            (from the project root)
//
// Every sprite is defined here as SVG and rasterized by headless Edge (or Chrome) at 2x into
// Assets/Stakeholders/Resources/StakeholdersUI/Notebook/. Notebook.uss draws them at half size
// (-unity-slice-scale: 0.5px), so they stay sharp on 4K screens. Frames are
// white-filled so the style sheet can tint them (-unity-background-image-tint-color); their 9-slice
// margins are listed in Tools/NotebookArt/slices.json and used in Notebook.uss.

import { mkdirSync, writeFileSync, existsSync, rmSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { tmpdir } from 'node:os';

const HERE = dirname(fileURLToPath(import.meta.url));
const PROJECT = resolve(HERE, '..', '..');
const OUT = join(PROJECT, 'Assets', 'Stakeholders', 'Resources', 'StakeholdersUI', 'Notebook');
const SVG_DIR = join(tmpdir(), 'notebook-art-svg');
const SCALE = 2;

const INK = '#2B2622';
const WHITE = '#FFFFFF';

// ------------------------------------------------------------------ helpers

function svg(w, h, body) {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${w * SCALE}" height="${h * SCALE}" viewBox="0 0 ${w} ${h}">${body}</svg>`;
}

const n = (v) => +v.toFixed(3);

// Rounded rect with a different elliptical radius per corner, like CSS "a b c d / e f g h".
function wobbly(x, y, w, h, r) {
  const [tlx, tly] = r.tl, [trx, tr_y] = r.tr, [brx, bry] = r.br, [blx, bly] = r.bl;
  return [
    `M${n(x + tlx)} ${n(y)}`,
    `L${n(x + w - trx)} ${n(y)}`,
    `A${trx} ${tr_y} 0 0 1 ${n(x + w)} ${n(y + tr_y)}`,
    `L${n(x + w)} ${n(y + h - bry)}`,
    `A${brx} ${bry} 0 0 1 ${n(x + w - brx)} ${n(y + h)}`,
    `L${n(x + blx)} ${n(y + h)}`,
    `A${blx} ${bly} 0 0 1 ${n(x)} ${n(y + h - bly)}`,
    `L${n(x)} ${n(y + tly)}`,
    `A${tlx} ${tly} 0 0 1 ${n(x + tlx)} ${n(y)}`,
    'Z',
  ].join(' ');
}

// A frame: optional offset shadow (same silhouette), then a white body with an ink outline.
function frame({ canvas, body, radii, stroke = 2.5, strokeColor = INK, fill = WHITE, shadowAt = null, shadow = INK, shadowOpacity = 1 }) {
  const [cw, ch] = canvas;
  const [bx, by, bw, bh] = body;
  let out = '';
  if (shadowAt) out += `<path d="${wobbly(shadowAt[0], shadowAt[1], bw, bh, radii)}" fill="${shadow}" fill-opacity="${shadowOpacity}"/>`;
  const s = stroke;
  out += `<path d="${wobbly(bx + s / 2, by + s / 2, bw - s, bh - s, radii)}" fill="${fill}" stroke="${strokeColor}" stroke-width="${s}" stroke-linejoin="round"/>`;
  return svg(cw, ch, out);
}

// ------------------------------------------------------------------ frames (9-slice, tintable)

const R_BTN = { tl: [28, 5], tr: [5, 14], br: [26, 5], bl: [5, 14] };
const R_BTN_SM = { tl: [18, 4], tr: [4, 9], br: [16, 4], bl: [4, 9] };
const R_CARD = { tl: [18, 8], tr: [6, 20], br: [22, 6], bl: [8, 18] };
const R_MODAL = { tl: [22, 10], tr: [8, 24], br: [26, 8], bl: [10, 22] };
const R_BOX = { tl: [10, 5], tr: [4, 12], br: [12, 4], bl: [5, 10] };
const R_TOAST = { tl: [12, 6], tr: [5, 14], br: [14, 5], bl: [6, 12] };
const R_BUBBLE = { tl: [11, 8], tr: [8, 10], br: [11, 7], bl: [7, 10] };
const R_STAMP = { tl: [6, 6], tr: [3, 7], br: [7, 3], bl: [3, 6] };
const R_TAG = { tl: [6, 4], tr: [3, 6], br: [6, 3], bl: [3, 6] };

const tabPath = 'M64 1.25 L15.25 1.25 A14 16 0 0 0 1.25 17.25 L1.25 34.75 A16 12 0 0 0 17.25 46.75 L64 46.75';

// slices in reference px (the PNG is 2x): left, top, right, bottom
const frames = [
  { name: 'btn', slices: [31, 17, 32, 21], svg: frame({ canvas: [99, 44], body: [0, 0, 96, 40], radii: R_BTN, shadowAt: [3, 4] }) },
  { name: 'btn_pressed', slices: [31, 17, 32, 21], svg: frame({ canvas: [99, 44], body: [2, 3, 96, 40], radii: R_BTN, shadowAt: [3, 4] }) },
  { name: 'btn_flat', slices: [31, 17, 32, 21], svg: frame({ canvas: [99, 44], body: [0, 0, 96, 40], radii: R_BTN }) },
  { name: 'btn_sm', slices: [20, 11, 20, 14], svg: frame({ canvas: [74, 33], body: [0, 0, 72, 30], radii: R_BTN_SM, stroke: 2, shadowAt: [2, 3] }) },
  { name: 'btn_sm_pressed', slices: [20, 11, 20, 14], svg: frame({ canvas: [74, 33], body: [1, 2, 72, 30], radii: R_BTN_SM, stroke: 2, shadowAt: [2, 3] }) },
  { name: 'btn_sm_flat', slices: [20, 11, 20, 14], svg: frame({ canvas: [74, 33], body: [0, 0, 72, 30], radii: R_BTN_SM, stroke: 2 }) },
  { name: 'card', slices: [21, 23, 30, 27], svg: frame({ canvas: [125, 126], body: [0, 0, 120, 120], radii: R_CARD, shadowAt: [5, 6], shadowOpacity: 0.14 }) },
  { name: 'card_flat', slices: [21, 23, 25, 21], svg: frame({ canvas: [120, 120], body: [0, 0, 120, 120], radii: R_CARD }) },
  { name: 'modal', slices: [25, 27, 37, 35], svg: frame({ canvas: [208, 170], body: [0, 0, 200, 160], radii: R_MODAL, stroke: 3, shadowAt: [8, 10], shadowOpacity: 0.35 }) },
  { name: 'box', slices: [12, 14, 14, 12], svg: frame({ canvas: [48, 48], body: [0, 0, 48, 48], radii: R_BOX, stroke: 2 }) },
  { name: 'inset', slices: [12, 14, 14, 12], svg: frame({ canvas: [48, 48], body: [0, 0, 48, 48], radii: R_BOX, stroke: 1.6, strokeColor: '#C9B99C' }) },
  { name: 'toast', slices: [14, 16, 19, 18], svg: frame({ canvas: [63, 48], body: [0, 0, 60, 44], radii: R_TOAST, stroke: 2, shadowAt: [3, 4], shadowOpacity: 0.25 }) },
  { name: 'bubble', slices: [13, 12, 13, 12], svg: frame({ canvas: [30, 24], body: [0, 0, 30, 24], radii: R_BUBBLE, stroke: 2 }) },
  { name: 'stamp', slices: [9, 10, 10, 9], svg: frame({ canvas: [40, 28], body: [0, 0, 40, 28], radii: R_STAMP, stroke: 2.5, strokeColor: WHITE, fill: 'none' }) },
  { name: 'tag', slices: [8, 8, 8, 8], svg: frame({ canvas: [32, 22], body: [0, 0, 32, 22], radii: R_TAG, stroke: 1.8 }) },
  {
    name: 'tab', slices: [19, 19, 4, 15],
    svg: svg(64, 48, `<path d="${tabPath} Z" fill="${WHITE}"/><path d="${tabPath}" fill="none" stroke="${INK}" stroke-width="2.5" stroke-linejoin="round" stroke-linecap="round"/>`),
  },
  {
    name: 'sticky', slices: [10, 10, 10, 26],
    svg: svg(128, 136,
      `<defs><filter id="b" x="-20%" y="-200%" width="140%" height="500%"><feGaussianBlur stdDeviation="4"/></filter></defs>` +
      `<ellipse cx="70" cy="124" rx="56" ry="6" fill="${INK}" fill-opacity="0.38" filter="url(#b)"/>` +
      `<path d="M6 2 L122 2 Q124 2 124 4 L124 120 Q124 122 122 122 L6 122 Q4 122 4 120 L4 4 Q4 2 6 2 Z" fill="${WHITE}"/>`),
  },
];

// ------------------------------------------------------------------ tiles (repeat)

function gridTile() {
  let body = '';
  for (let k = 0; k < 224; k += 28) {
    body += `<rect x="${k}" y="0" width="1" height="224" fill="#E7DDC9"/>`;
    body += `<rect x="0" y="${k}" width="224" height="1" fill="#E7DDC9"/>`;
  }
  return svg(224, 224, body);
}

const tiles = [
  { name: 'paper_grid', svg: gridTile() },
  {
    name: 'hatch',
    svg: svg(16, 16, [-16, 0, 16].map((k) => `<polygon points="${k},0 ${k + 8},0 ${k + 8 - 16},16 ${k - 16},16" fill="${WHITE}" fill-opacity="0.42"/>`).join('')),
  },
  { name: 'dash', svg: svg(12, 2, `<rect x="0" y="0.25" width="6.5" height="1.5" rx="0.75" fill="#CDBFA6"/>`) },
  { name: 'ruled', svg: svg(8, 26, `<rect x="0" y="25" width="8" height="1" fill="#CFE3FF"/>`) },
  { name: 'squiggle', svg: svg(150, 12, `<path d="M2 7 Q16 2 30 7 T58 7 T86 6 T114 7 T148 7" fill="none" stroke="#F07A5F" stroke-width="3" stroke-linecap="round"/>`) },
  { name: 'highlight', svg: svg(200, 24, `<path d="M7 5 Q60 2 120 3.5 T194 4 Q199 11 195 19 Q130 22 70 20.5 T6 21 Q1 13 7 5 Z" fill="#FFD84D"/>`) },
];

// ------------------------------------------------------------------ icons (white, tinted in USS)

const st = (w = 2) => `fill="none" stroke="${WHITE}" stroke-width="${w}" stroke-linecap="round" stroke-linejoin="round"`;
const icon = (body) => svg(24, 24, body);
const p = (d, w = 2) => `<path d="${d}" ${st(w)}/>`;
const dot = (cx, cy, r = 1.3) => `<circle cx="${cx}" cy="${cy}" r="${r}" fill="${WHITE}"/>`;

const icons = [
  { name: 'dashboard', svg: icon(p('M3.5 17.5 A8.5 8.5 0 1 1 20.5 17.5', 2.2) + p('M12 17 L16.2 10.2', 2.4) + dot(12, 17, 1.8) + p('M6.5 11 L7.8 12 M12 6.3 V8 M17.5 11 L16.2 12', 1.8)) },
  { name: 'situations', svg: icon(p('M4 5.5 Q4 4 5.5 4 H18.5 Q20 4 20 5.5 V14.5 Q20 16 18.5 16 H10.5 L6.5 20 V16 H5.5 Q4 16 4 14.5 Z', 2.1) + p('M12 7 V11', 2.4) + dot(12, 13.6)) },
  { name: 'products', svg: icon(p('M3.5 8 L12 4 L20.5 8 L20.5 16.8 L12 21 L3.5 16.8 Z', 2.1) + p('M3.5 8 L12 12 L20.5 8 M12 12 V21 M7.8 6 L16.2 10', 1.9)) },
  { name: 'market', svg: icon(`<circle cx="10.2" cy="10.2" r="6.4" ${st(2.2)}/>` + p('M15 15 L20.5 20.5', 3) + p('M7.4 8.6 Q8.2 6.8 10.2 6.6', 1.6)) },
  { name: 'suppliers', svg: icon(p('M3 20.5 V11 L8 14 V11 L13 14 V8.5 H20.5 V20.5 Z', 2.1) + p('M16.5 8.5 V4 H19 V8.5 M16.5 2.5 Q18 1 19.5 2.5 M2 20.5 H22', 1.8)) },
  { name: 'marketing', svg: icon(p('M3.5 10 V14.5 H7 L15.5 19 V5.5 L7 10 Z', 2.1) + p('M7 14.5 L8.5 19.5 H10.8 L9.8 15.5 M18.5 9 Q20.6 12 18.5 15.5', 1.9)) },
  { name: 'operations', svg: icon(p('M4.5 3.5 H7 L10.8 17.2 H20', 2.1) + `<circle cx="12.5" cy="19.6" r="2" ${st(1.9)}/>` + p('M10 8.2 L17 6.4 L18.8 13.2 L11.8 15 Z', 1.9)) },
  { name: 'finance', svg: icon(p('M5 5 Q5 3.5 6.5 3.5 H19 V17 H7 Q5 17 5 19 Q5 20.5 7 20.5 H19 V17', 2.1) + p('M5 5 V19 M9 8 H15.5 M9 11.5 H14', 1.8)) },
  { name: 'growth', svg: icon(p('M2.5 20.5 H7.5 V15.5 H12.5 V10.5 H17.5 V5.5 H20.5', 2.1) + p('M19.5 5.5 V1.8', 1.8) + `<path d="M19.5 2 L23 3.2 L19.5 4.4 Z" fill="${WHITE}" stroke="${WHITE}" stroke-width="1.2" stroke-linejoin="round"/>` + p('M2.5 20.5 V22', 1.6)) },
  { name: 'competition', svg: icon(p('M4 4 L16 16 M20 4 L8 16', 2.2) + p('M13.6 18.4 L18.4 13.6 M5.6 13.6 L10.4 18.4', 2.2) + p('M16.6 16.6 L20.2 20.2 M7.4 16.6 L3.8 20.2', 2.8)) },
  { name: 'partners', svg: icon(`<circle cx="9" cy="8" r="3.4" ${st()}/>` + `<circle cx="16.6" cy="9.2" r="2.8" ${st(1.9)}/>` + p('M2.8 20 Q3 13.2 9 13.2 Q15 13.2 15.2 20 M15.4 14 Q20.8 13.4 21.4 19.4')) },
  { name: 'board', svg: icon(p('M8.5 5.5 L14.5 11.5', 5) + p('M11.5 8.5 L4 16', 2.4) + p('M12 20.5 H21', 2.6)) },
  { name: 'career', svg: icon(p('M8 3.5 H16 V9 Q16 13 12 13 Q8 13 8 9 Z M8 5.5 H4.8 Q4.8 10 8.2 10 M16 5.5 H19.2 Q19.2 10 15.8 10 M12 13 V17 M9 17 H15 V20.5 H9 Z')) },
  { name: 'private', svg: icon(p('M2.5 9.5 Q2.5 7 6 7 Q9.3 7 12 9.2 Q14.7 7 18 7 Q21.5 7 21.5 9.5 Q21.5 15.5 17 15.5 Q13.8 15.5 12 12.8 Q10.2 15.5 7 15.5 Q2.5 15.5 2.5 9.5 Z') + `<ellipse cx="7.4" cy="10.8" rx="2" ry="1.5" fill="${WHITE}"/><ellipse cx="16.6" cy="10.8" rx="2" ry="1.5" fill="${WHITE}"/>`) },
  { name: 'cash', svg: icon(`<circle cx="12" cy="12" r="9" ${st()}/>` + p('M14.6 9.2 Q14.1 7.6 12 7.6 Q9.6 7.6 9.6 9.5 Q9.6 11.3 12 11.8 Q14.6 12.3 14.6 14.4 Q14.6 16.4 12 16.4 Q9.9 16.4 9.4 14.8 M12 6 V18', 1.8)) },
  { name: 'valuation', svg: icon(p('M3 20.5 H21 M4.5 16 L9.5 11 L13 14 L20 6.5 M15 6 H20.3 V11.3', 2.2)) },
  { name: 'satisfaction', svg: icon(`<circle cx="12" cy="12" r="9" ${st()}/>` + dot(9, 10) + dot(15, 10) + p('M8 14 Q12 17.5 16 14')) },
  { name: 'reputation', svg: icon(p('M12 3 L14.6 8.7 L20.8 9.4 L16.2 13.6 L17.5 19.8 L12 16.6 L6.5 19.8 L7.8 13.6 L3.2 9.4 L9.4 8.7 Z')) },
  { name: 'calendar', svg: icon(p('M4 6 Q4 5 5 5 H19 Q20 5 20 6 V19 Q20 20 19 20 H5 Q4 20 4 19 Z M4 9.5 H20 M8 3 V6.5 M16 3 V6.5') + dot(8.5, 13.5, 1.1) + dot(12, 13.5, 1.1) + dot(15.5, 13.5, 1.1) + dot(8.5, 16.8, 1.1)) },
  { name: 'pause', svg: icon(p('M8 5 Q7.6 12 8 19 M16 5 Q16.4 12 16 19', 3.2)) },
  { name: 'play', svg: icon(`<path d="M7 4.5 Q6.6 12 7 19.5 Q13.5 16 19.5 12 Q13.5 8 7 4.5 Z" fill="${WHITE}" stroke="${WHITE}" stroke-width="2" stroke-linejoin="round"/>`) },
  { name: 'menu', svg: icon(p('M4 7 Q12 6 20 7.5 M4 12 Q12 12.8 20 11.6 M4 17 Q12 16.2 20 17.4', 2.6)) },
  { name: 'send', svg: icon(p('M3.5 11.5 L20.5 4 L15 20.5 L11.5 13 Z M11.5 13 L20.5 4')) },
  { name: 'gear', svg: icon(`<circle cx="12" cy="12" r="3.4" ${st()}/><circle cx="12" cy="12" r="7" ${st()}/>` + p('M12 2.8 V5 M12 19 V21.2 M2.8 12 H5 M19 12 H21.2 M5.5 5.5 L7.1 7.1 M16.9 16.9 L18.5 18.5 M5.5 18.5 L7.1 16.9 M16.9 7.1 L18.5 5.5', 2.2)) },
  { name: 'help', svg: icon(`<circle cx="12" cy="12" r="9.2" ${st()}/>` + p('M9.3 9.4 Q9.4 6.6 12 6.6 Q14.7 6.6 14.7 9 Q14.7 11 12.2 12 Q11.8 12.3 11.8 14') + dot(11.9, 17.3, 1.2)) },
  { name: 'close', svg: icon(p('M6 6.5 Q12 12 17.5 18 M17.8 6 Q12 12.5 6.2 18', 2.4)) },
  { name: 'check', svg: icon(p('M4.5 12.5 L9.5 17.5 L19.5 6', 2.6)) },
  { name: 'plus', svg: icon(p('M12 5 V19 M5 12 H19', 2.4)) },
  { name: 'arrow_right', svg: icon(p('M4 12 Q12 11 19 12 M14 7 L20 12 L14 17', 2.2)) },
  { name: 'chevron_down', svg: icon(p('M6 9.5 L12 15.5 L18 9.5', 2.4)) },
  { name: 'lock', svg: icon(p('M5.5 11 H18.5 V20.5 H5.5 Z M8 11 V8 Q8 4 12 4 Q16 4 16 8 V11') + dot(12, 15.5, 1.4)) },
  { name: 'clock', svg: icon(`<circle cx="12" cy="12" r="9" ${st()}/>` + p('M12 7 V12 L15.5 14')) },
  { name: 'warning', svg: icon(p('M12 3.5 L21.5 20 H2.5 Z') + p('M12 9.5 V14', 2.2) + dot(12, 17, 1.3)) },
  { name: 'star', svg: icon(p('M12 3 L14.6 8.7 L20.8 9.4 L16.2 13.6 L17.5 19.8 L12 16.6 L6.5 19.8 L7.8 13.6 L3.2 9.4 L9.4 8.7 Z')) },
  { name: 'heart', svg: icon(p('M12 20 Q3 14 3.5 8.5 Q4.5 4 8.5 4.5 Q11 5 12 7.5 Q13 5 15.5 4.5 Q19.5 4 20.5 8.5 Q21 14 12 20 Z')) },
  { name: 'eye', svg: icon(p('M2.5 12 Q12 3 21.5 12 Q12 21 2.5 12 Z') + `<circle cx="12" cy="12" r="3" ${st()}/>`) },
  { name: 'mail', svg: icon(p('M3 6 H21 V18 H3 Z M3 6.5 L12 13 L21 6.5')) },
  { name: 'bell', svg: icon(p('M6 17 V11 Q6 5 12 5 Q18 5 18 11 V17 Z M4 17 H20 M10 20 Q12 22 14 20')) },
  { name: 'truck', svg: icon(p('M2.5 7 H14 V16.5 H2.5 Z M14 10 H18 L21.5 13.5 V16.5 H14') + `<circle cx="7" cy="18" r="1.8" ${st()}/><circle cx="17.5" cy="18" r="1.8" ${st()}/>`) },
  { name: 'sparkle', svg: icon(p('M12 3 Q13 11 21 12 Q13 13 12 21 Q11 13 3 12 Q11 11 12 3 Z')) },
  { name: 'flame', svg: icon(p('M12 21 Q5 19 6 12.5 Q7 8.5 10 6.5 Q10 10 12.5 10.5 Q12 6 14.5 3 Q20 8 18.5 14.5 Q17.5 20 12 21 Z')) },
  { name: 'globe', svg: icon(`<circle cx="12" cy="12" r="9" ${st()}/>` + p('M12 3 Q7 12 12 21 Q17 12 12 3 M3.5 9.5 H20.5 M3.5 14.5 H20.5', 1.8)) },
  { name: 'save', svg: icon(p('M4 5 Q4 4 5 4 H16 L20 8 V19 Q20 20 19 20 H5 Q4 20 4 19 Z M8 4 V9 H15 V4 M7 20 V14 H17 V20')) },
  { name: 'folder', svg: icon(p('M3.5 6 Q3.5 5 4.5 5 H10 L12 7.5 H19.5 Q20.5 7.5 20.5 8.5 V18.5 Q20.5 19.5 19.5 19.5 H4.5 Q3.5 19.5 3.5 18.5 Z')) },
  { name: 'quit', svg: icon(p('M5 21 V4 Q5 3 6 3 H14 Q15 3 15 4 V21 M2.5 21 H17.5 M18 12 H22.5 M20.5 10 L22.5 12 L20.5 14') + dot(12, 12.5, 1.1)) },
  { name: 'sound', svg: icon(p('M4 9.5 H8 L13 5.5 V18.5 L8 14.5 H4 Z M16 9 Q18 12 16 15 M18.5 7 Q22 12 18.5 17')) },
  { name: 'sound_off', svg: icon(p('M4 9.5 H8 L13 5.5 V18.5 L8 14.5 H4 Z M16.5 9.5 L21 14.5 M21 9.5 L16.5 14.5')) },
  { name: 'user', svg: icon(`<circle cx="12" cy="8" r="4" ${st()}/>` + p('M4 21 Q4 13.5 12 13.5 Q20 13.5 20 21')) },
];

// ------------------------------------------------------------------ colored icons (ink outline + marker fill; not tinted)

const inks = (w = 2) => `stroke="${INK}" stroke-width="${w}" stroke-linecap="round" stroke-linejoin="round"`;
const iconsColor = [
  { name: 'coin', svg: icon(`<circle cx="12" cy="12" r="9" fill="#FFD84D" ${inks()}/><path d="M14.6 9.2 Q14.1 7.6 12 7.6 Q9.6 7.6 9.6 9.5 Q9.6 11.3 12 11.8 Q14.6 12.3 14.6 14.4 Q14.6 16.4 12 16.4 Q9.9 16.4 9.4 14.8 M12 6 V18" fill="none" ${inks(1.8)}/>`) },
  { name: 'smiley', svg: icon(`<circle cx="12" cy="12" r="9" fill="#D4EFC0" ${inks()}/><circle cx="9" cy="10" r="1.3" fill="${INK}"/><circle cx="15" cy="10" r="1.3" fill="${INK}"/><path d="M8 14 Q12 17.5 16 14" fill="none" ${inks()}/>`) },
  { name: 'frown', svg: icon(`<circle cx="12" cy="12" r="9" fill="#FDE6DF" ${inks()}/><circle cx="9" cy="10" r="1.3" fill="${INK}"/><circle cx="15" cy="10" r="1.3" fill="${INK}"/><path d="M8 16 Q12 12.8 16 16" fill="none" ${inks()}/>`) },
  { name: 'star', svg: icon(`<path d="M12 3 L14.6 8.7 L20.8 9.4 L16.2 13.6 L17.5 19.8 L12 16.6 L6.5 19.8 L7.8 13.6 L3.2 9.4 L9.4 8.7 Z" fill="#FFD84D" ${inks()}/>`) },
  { name: 'trophy', svg: icon(`<path d="M8 3.5 H16 V9 Q16 13 12 13 Q8 13 8 9 Z" fill="#FFD84D" ${inks()}/><path d="M8 5.5 H4.8 Q4.8 10 8.2 10 M16 5.5 H19.2 Q19.2 10 15.8 10 M12 13 V17" fill="none" ${inks()}/><path d="M9 17 H15 V20.5 H9 Z" fill="#D8B27A" ${inks()}/>`) },
  { name: 'heart', svg: icon(`<path d="M12 20 Q3 14 3.5 8.5 Q4.5 4 8.5 4.5 Q11 5 12 7.5 Q13 5 15.5 4.5 Q19.5 4 20.5 8.5 Q21 14 12 20 Z" fill="#F07A5F" ${inks()}/>`) },
  { name: 'flame', svg: icon(`<path d="M12 21 Q5 19 6 12.5 Q7 8.5 10 6.5 Q10 10 12.5 10.5 Q12 6 14.5 3 Q20 8 18.5 14.5 Q17.5 20 12 21 Z" fill="#FFB25B" ${inks()}/>`) },
  { name: 'gavel', svg: icon(`<path d="M8.5 5.5 L14.5 11.5" stroke="${INK}" stroke-width="5" stroke-linecap="round"/><path d="M11.5 8.5 L4 16" stroke="${INK}" stroke-width="2.4" stroke-linecap="round"/><path d="M12 20.5 H21" stroke="#B89AF0" stroke-width="3" stroke-linecap="round"/>`) },
];

// ------------------------------------------------------------------ avatars: a tintable head + one face per personality

const HEAD_D = 'M32 11 C45 10.5 55.5 21 55 34.5 C54.5 47.5 44.5 57.5 31.5 57 C18.5 57.5 9 47 9.5 34 C10 21 19.5 11.5 32 11 Z';
const L = (d, w = 2.6) => `<path d="${d}" fill="none" stroke="${INK}" stroke-width="${w}" stroke-linecap="round" stroke-linejoin="round"/>`;
const F = (d, fill, w = 2) => `<path d="${d}" fill="${fill}" stroke="${INK}" stroke-width="${w}" stroke-linejoin="round"/>`;
const eyesDots = `<circle cx="24" cy="33" r="2.8" fill="${INK}"/><circle cx="40" cy="33" r="2.8" fill="${INK}"/>`;
const blush = `<ellipse cx="18.5" cy="41" rx="3.6" ry="2.1" fill="#F29A8A" opacity="0.6"/><ellipse cx="45.5" cy="41" rx="3.6" ry="2.1" fill="#F29A8A" opacity="0.6"/>`;
const smile = L('M24 43 Q32 50 40 43');
const face = (body) => svg(64, 64, body);
const dollar = (x) => L(`M${x + 2.5} 29.6 Q${x + 1.6} 28 ${x} 28 Q${x - 2.2} 28 ${x - 2.2} 30 Q${x - 2.2} 31.8 ${x} 32.3 Q${x + 2.5} 32.8 ${x + 2.5} 34.8 Q${x + 2.5} 36.8 ${x} 36.8 Q${x - 1.8} 36.8 ${x - 2.4} 35.3 M${x} 26.6 V38.2`, 1.7).replace(INK, '#23704A');

const avatars = [
  { name: 'head', svg: face(`<path d="${HEAD_D}" fill="${WHITE}" stroke="${INK}" stroke-width="2.8" stroke-linejoin="round"/>`) },
  { name: 'face_you', svg: face(L('M27 12.5 C26.5 6.5 31 4 34 7.5 C35.5 4 39.5 5 38.5 9.5') + blush + eyesDots + smile) },
  { name: 'face_greedy', svg: face(dollar(24) + dollar(40) + F('M22 41.5 Q32 54 42 41.5 Z', '#FFFCF5', 2.4) + L('M25 44.2 H39', 1.4)) },
  { name: 'face_loyal', svg: face(blush + L('M20 34.5 Q24 29 28 34.5') + L('M36 34.5 Q40 29 44 34.5') + smile + F('M46.5 44 C46.5 40.8 50.5 40.8 50.5 43.6 C50.5 40.8 54.5 40.8 54.5 44 C54.5 47 50.5 49.2 50.5 50.2 C50.5 49.2 46.5 47 46.5 44 Z', '#F07A5F', 1.4)) },
  { name: 'face_risktaker', svg: face(F('M15.5 29 H30 L29 35 Q23 39.5 17 35 Z', INK, 1.6) + F('M34 29 H48.5 L47 35 Q41 39.5 35 35 Z', INK, 1.6) + L('M30 30 Q32 28.2 34 30', 2) + `<path d="M19 31.5 L22.5 31.5 M37.5 31.5 L41 31.5" stroke="#FFFCF5" stroke-width="1.6" stroke-linecap="round"/>` + F('M22 41.5 Q32 54 42 41.5 Z', '#FFFCF5', 2.4) + L('M25 44.2 H39', 1.4)) },
  { name: 'face_conservative', svg: face(L('M16 21 Q29 11.5 47 18', 2.4) + `<circle cx="24" cy="33" r="6.2" fill="#FFFCF5" fill-opacity="0.45" stroke="${INK}" stroke-width="2.2"/><circle cx="40" cy="33" r="6.2" fill="#FFFCF5" fill-opacity="0.45" stroke="${INK}" stroke-width="2.2"/>` + L('M30.2 33 Q32 31 33.8 33', 2) + `<circle cx="24" cy="33.5" r="2.2" fill="${INK}"/><circle cx="40" cy="33.5" r="2.2" fill="${INK}"/>` + L('M26 45.5 H38')) },
  { name: 'face_workaholic', svg: face(eyesDots + L('M25 45.5 Q28 43 31 45.5 Q34 48 37 45.5 Q38.5 44.5 40 45.5', 2.4) + L('M20 38.5 Q24 40.5 28 38.5', 1.5) + L('M36 38.5 Q40 40.5 44 38.5', 1.5) + F('M51.5 18 Q55.8 25 51.5 27.6 Q47.2 25 51.5 18 Z', '#CFE3FF', 1.6)) },
  { name: 'face_lazy', svg: face(L('M19 32.5 Q24 36.5 29 32.5') + L('M35 32.5 Q40 36.5 45 32.5') + L('M29 45.5 Q32 47.5 35 45.5', 2.4) + L('M48.5 10 H53 L48.5 14.5 H53', 1.6).replace(INK, '#5E554C') + L('M55.5 3.5 H58.8 L55.5 6.8 H58.8', 1.4).replace(INK, '#5E554C')) },
  { name: 'face_manipulative', svg: face(L('M19 34.5 Q24 31 29 34.5') + L('M35 34.5 Q40 31 45 34.5') + `<circle cx="24" cy="35.2" r="1.7" fill="${INK}"/><circle cx="40" cy="35.2" r="1.7" fill="${INK}"/>` + L('M18 26 L29 28.5', 2.4) + L('M35 25.5 Q40 20.5 46.5 23.5', 2.4) + L('M25 45.5 Q33.5 47.5 40.5 41')) },
  { name: 'face_ambitious', svg: face(F('M19 15 L20.5 3.5 L26.5 9.5 L32 1.5 L37.5 9.5 L43.5 3.5 L45 15 Z', '#FFD84D', 2.2) + blush + eyesDots + L('M23 42.5 Q32 51.5 41 42', 2.8)) },
  { name: 'face_employee', svg: face(F('M12.5 26 Q13 9.5 32 9 Q51 9.5 51.5 26 Z', '#2F5DA8', 2.4) + L('M36 26 Q48 22.5 60 27') + blush + eyesDots + smile) },
];

// ------------------------------------------------------------------ fx (not sliced)

const fx = [
  {
    name: 'burst',
    svg: svg(300, 240,
      `<path d="M150 18 L166 70 L214 40 L196 92 L256 92 L208 124 L262 160 L200 158 L214 214 L166 178 L150 226 L134 178 L86 214 L100 158 L38 160 L92 124 L44 92 L104 92 L86 40 L134 70 Z" fill="#FFE58A" stroke="${INK}" stroke-width="3" stroke-linejoin="round"/>` +
      `<path d="M40 30 L50 40 M260 26 L250 38 M30 206 L44 196 M268 210 L254 198 M150 4 V12" fill="none" stroke="${INK}" stroke-width="2.4" stroke-linecap="round"/>`),
  },
  {
    name: 'tape',
    svg: svg(80, 24, `<path d="M4 3 L76 1 L78 5 L75 9 L79 13 L76 17 L78 21 L6 23 L3 19 L6 15 L2 11 L5 7 Z" fill="#FFFCF5" fill-opacity="0.75" stroke="${INK}" stroke-opacity="0.12" stroke-width="1"/>`),
  },
];

// ------------------------------------------------------------------ render

const groups = { frames, tiles, icons, icons_color: iconsColor, avatars, fx };

function findBrowser() {
  const candidates = [
    process.env.NOTEBOOK_BROWSER,
    'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
    'C:/Program Files/Microsoft/Edge/Application/msedge.exe',
    'C:/Program Files/Google/Chrome/Application/chrome.exe',
    '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
    '/usr/bin/google-chrome',
    '/usr/bin/chromium',
  ].filter(Boolean);
  const found = candidates.find((c) => existsSync(c));
  if (!found) throw new Error('No Edge or Chrome found. Set NOTEBOOK_BROWSER to a Chromium-based browser.');
  return found;
}

function sizeOf(svgText) {
  const m = /width="(\d+(?:\.\d+)?)" height="(\d+(?:\.\d+)?)"/.exec(svgText);
  return [Math.round(+m[1]), Math.round(+m[2])];
}

const browser = findBrowser();
const profile = join(tmpdir(), 'notebook-art-profile');
const only = process.argv[2];
const slices = {};
let count = 0;

for (const [group, list] of Object.entries(groups)) {
  mkdirSync(join(OUT, group), { recursive: true });
  mkdirSync(join(SVG_DIR, group), { recursive: true });
  for (const sprite of list) {
    if (only && sprite.name !== only && group !== only) continue;
    const svgPath = join(SVG_DIR, group, `${sprite.name}.svg`);
    const pngPath = join(OUT, group, `${sprite.name}.png`);
    writeFileSync(svgPath, sprite.svg);
    const [w, h] = sizeOf(sprite.svg);
    if (existsSync(pngPath)) rmSync(pngPath);
    const r = spawnSync(browser, [
      '--headless=new', '--disable-gpu', '--hide-scrollbars', '--force-device-scale-factor=1',
      '--default-background-color=00000000', `--window-size=${w},${h}`, `--user-data-dir=${profile}`,
      `--screenshot=${pngPath}`, pathToFileURL(svgPath).href,
    ], { stdio: 'ignore', timeout: 60000 });
    if (!existsSync(pngPath)) throw new Error(`Rendering failed for ${group}/${sprite.name} (exit ${r.status})`);
    if (sprite.slices) slices[`${group}/${sprite.name}`] = { left: sprite.slices[0], top: sprite.slices[1], right: sprite.slices[2], bottom: sprite.slices[3], size: [w / SCALE, h / SCALE] };
    count++;
    process.stdout.write(`${group}/${sprite.name}.png ${w}x${h}\n`);
  }
}

if (!only) writeFileSync(join(HERE, 'slices.json'), JSON.stringify(slices, null, 2) + '\n');
console.log(`Rendered ${count} sprites into ${OUT}`);
