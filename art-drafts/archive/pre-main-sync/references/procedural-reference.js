const ctx = document.querySelector('canvas').getContext('2d');
const TAU=Math.PI*2; let zoom=1,time=0;
const hash=(n)=>{const s=Math.sin(n*127.1+311.7)*43758.5453;return s-Math.floor(s);};
const SHAPES = {
  slim:    { hh: 0.36, nx: 0.92, ny: 0.95, ped: 0.2,  tl: 0.48, tH: 0.38, dH: 0.18, aH: 0.12, tail: 'fork',   eye: 0.11 },
  oval:    { hh: 0.50, nx: 0.95, ny: 1.0,  ped: 0.2,  tl: 0.45, tH: 0.42, dH: 0.22, aH: 0.15, tail: 'fork',   eye: 0.11 },
  disc:    { hh: 0.66, nx: 0.85, ny: 1.05, ped: 0.18, tl: 0.36, tH: 0.40, dH: 0.16, aH: 0.14, tail: 'lunate', eye: 0.1 },
  tall:    { hh: 0.70, nx: 0.8,  ny: 1.0,  ped: 0.18, tl: 0.38, tH: 0.38, dH: 0.72, aH: 0.72, tail: 'fan',    eye: 0.1, trailing: true },
  round:   { hh: 0.80, nx: 1.05, ny: 1.15, ped: 0.22, tl: 0.30, tH: 0.30, dH: 0.12, aH: 0.10, tail: 'fan',    eye: 0.14 },
  long:    { hh: 0.20, nx: 0.6,  ny: 0.8,  ped: 0.3,  tl: 0.40, tH: 0.32, dH: 0.20, aH: 0.14, tail: 'fork',   eye: 0.07 },
  fat:     { hh: 0.56, nx: 0.98, ny: 1.05, ped: 0.2,  tl: 0.38, tH: 0.40, dH: 0.20, aH: 0.14, tail: 'fan',    eye: 0.09 },
  torpedo: { hh: 0.36, nx: 0.8,  ny: 0.95, ped: 0.14, tl: 0.45, tH: 0.50, dH: 0.22, aH: 0.18, tail: 'lunate', eye: 0.08 },
  shark:   { hh: 0.30, nx: 0.5,  ny: 0.7,  ped: 0.14, tl: 0.60, tH: 0.55, dH: 0.42, aH: 0.12, tail: 'shark',  eye: 0.055 },
};
const SPECIES = {
  minnow:    { shape: 'slim',    c: ['#cfe9ff', '#5b8fc4', '#f4fbff'], fin: 'rgba(156,199,238,.85)', pat: 'line',   min: 3,  max: 14 },
  clown:     { shape: 'oval',    c: ['#ff8c2b', '#c94c0c', '#ffd0a0'], fin: '#ff7a1a', pat: 'bands',   min: 7,  max: 22 },
  tang:      { shape: 'disc',    c: ['#2f86ff', '#1340a8', '#8cc2ff'], fin: '#1d5fe0', pat: 'tang',    min: 9,  max: 28, tailCol: '#ffd23f' },
  angel:     { shape: 'tall',    c: ['#ffe55c', '#d9a300', '#fff6c2'], fin: '#ffd21f', pat: 'stripes', min: 13, max: 38 },
  puffer:    { shape: 'round',   c: ['#e9d585', '#9c8640', '#fffbe3'], fin: '#d8c06a', pat: 'spots',   min: 16, max: 48, spot: '#5a4818' },
  parrot:    { shape: 'oval',    c: ['#3ee0a8', '#1a8f8a', '#c8ffe9'], fin: '#ff7ac8', pat: 'scales',  min: 20, max: 60 },
  snapper:   { shape: 'oval',    c: ['#ff5f6f', '#b4253a', '#ffd0d4'], fin: '#ff7a88', pat: 'none',    min: 22, max: 64 },
  barracuda: { shape: 'long',    c: ['#b4c3d1', '#5d6e80', '#eef4f9'], fin: '#8a9bab', pat: 'bars',    min: 34, max: 130 },
  grouper:   { shape: 'fat',     c: ['#9a7650', '#5d432a', '#e2cba9'], fin: '#7d5d3c', pat: 'spots',   min: 48, max: 180, spot: '#3a2814' },
  tuna:      { shape: 'torpedo', c: ['#4d6d9c', '#1d2f52', '#e3e9f2'], fin: '#5a7bb0', pat: 'finlets', min: 70, max: 600 },
};
const SHARK_SP = { shape: 'shark', c: ['#62788b', '#283746', '#f2f5f7'], fin: '#4a5f72', pat: 'shark' };
const PLAYER_SP = { shape: 'oval', c: ['#3df2d0', '#0a8f99', '#e2fff8'], fin: '#ffc93c', pat: 'player', eye: 0.15 };

function bodyPath(hl, hh, sh, bend) {
  const ped = sh.ped;
  ctx.beginPath();
  ctx.moveTo(hl, 0);
  ctx.bezierCurveTo(hl * sh.nx, -hh * sh.ny, -hl * 0.3, -hh * 1.12 + bend * 0.3, -hl * 0.86, -hh * ped + bend);
  ctx.lineTo(-hl * 0.86, hh * ped + bend);
  ctx.bezierCurveTo(-hl * 0.3, hh * 1.12 + bend * 0.3, hl * sh.nx, hh * sh.ny, hl, 0);
  ctx.closePath();
}

function drawTail(sh, hl, hh, col) {
  const tl = hl * sh.tl, th = hl * sh.tH;
  ctx.fillStyle = col;
  ctx.beginPath();
  if (sh.tail === 'fork') {
    ctx.moveTo(hl * 0.08, -hh * 0.2);
    ctx.quadraticCurveTo(-tl * 0.45, -th * 0.45, -tl, -th);
    ctx.quadraticCurveTo(-tl * 0.6, -th * 0.15, -tl * 0.5, 0);
    ctx.quadraticCurveTo(-tl * 0.6, th * 0.15, -tl, th);
    ctx.quadraticCurveTo(-tl * 0.45, th * 0.45, hl * 0.08, hh * 0.2);
  } else if (sh.tail === 'lunate') {
    ctx.moveTo(hl * 0.06, -hh * 0.14);
    ctx.quadraticCurveTo(-tl * 0.5, -th * 0.25, -tl, -th);
    ctx.quadraticCurveTo(-tl * 0.5, -th * 0.2, -tl * 0.36, 0);
    ctx.quadraticCurveTo(-tl * 0.5, th * 0.2, -tl, th);
    ctx.quadraticCurveTo(-tl * 0.5, th * 0.25, hl * 0.06, hh * 0.14);
  } else if (sh.tail === 'fan') {
    ctx.moveTo(hl * 0.06, -hh * 0.22);
    ctx.lineTo(-tl * 0.85, -th);
    ctx.quadraticCurveTo(-tl * 1.3, 0, -tl * 0.85, th);
    ctx.lineTo(hl * 0.06, hh * 0.22);
  } else {
    ctx.moveTo(hl * 0.06, -hh * 0.16);
    ctx.quadraticCurveTo(-tl * 0.5, -th * 0.45, -tl, -th);
    ctx.quadraticCurveTo(-tl * 0.62, -th * 0.2, -tl * 0.44, hh * 0.08);
    ctx.quadraticCurveTo(-tl * 0.56, th * 0.3, -tl * 0.62, th * 0.58);
    ctx.quadraticCurveTo(-tl * 0.3, th * 0.22, hl * 0.06, hh * 0.16);
  }
  ctx.closePath();
  ctx.fill();
}

function drawFins(sh, hl, hh, w, col) {
  const dH = hl * sh.dH, aH = hl * sh.aH;
  ctx.fillStyle = col;
  ctx.beginPath();
  if (sh.tail === 'shark') {
    ctx.moveTo(hl * 0.22, -hh * 0.6);
    ctx.quadraticCurveTo(hl * 0.04, -hh * 0.9 - dH * 0.6, -hl * 0.14, -hh * 0.75 - dH);
    ctx.quadraticCurveTo(-hl * 0.1, -hh * 0.85, -hl * 0.3, -hh * 0.55);
    ctx.moveTo(-hl * 0.62, -hh * 0.3);
    ctx.lineTo(-hl * 0.7, -hh * 0.3 - dH * 0.22);
    ctx.lineTo(-hl * 0.76, -hh * 0.25);
  } else if (sh.trailing) {
    ctx.moveTo(hl * 0.3, -hh * 0.6);
    ctx.quadraticCurveTo(hl * 0.0, -hh * 0.8 - dH, -hl * 0.95 + w * hl * 0.25, -hh * 0.6 - dH * 1.15);
    ctx.quadraticCurveTo(-hl * 0.55, -hh * 0.6, -hl * 0.7, -hh * 0.25);
    ctx.moveTo(hl * 0.1, hh * 0.6);
    ctx.quadraticCurveTo(-hl * 0.1, hh * 0.8 + aH, -hl * 0.95 + w * hl * 0.25, hh * 0.6 + aH * 1.15);
    ctx.quadraticCurveTo(-hl * 0.55, hh * 0.6, -hl * 0.7, hh * 0.25);
  } else {
    ctx.moveTo(hl * 0.28, -hh * 0.6);
    ctx.quadraticCurveTo(hl * 0.05, -hh * 0.8 - dH * 1.3, -hl * 0.4, -hh * 0.7 - dH);
    ctx.quadraticCurveTo(-hl * 0.5, -hh * 0.7, -hl * 0.62, -hh * 0.35);
    ctx.moveTo(-hl * 0.1, hh * 0.6);
    ctx.quadraticCurveTo(-hl * 0.3, hh * 0.75 + aH * 1.2, -hl * 0.55, hh * 0.6 + aH);
    ctx.quadraticCurveTo(-hl * 0.6, hh * 0.55, -hl * 0.68, hh * 0.3);
  }
  ctx.fill();
}

function drawPattern(f, sp, hl, hh) {
  switch (sp.pat) {
    case 'line':
      ctx.fillStyle = 'rgba(255,255,255,0.6)';
      ctx.fillRect(-hl, -hh * 0.12, hl * 2, hh * 0.16);
      ctx.fillStyle = 'rgba(40,80,140,0.35)';
      ctx.fillRect(-hl, -hh, hl * 2, hh * 0.45);
      break;
    case 'bands':
      for (const bx of [hl * 0.42, -hl * 0.12, -hl * 0.64]) {
        const bw = hl * 0.17;
        ctx.fillStyle = '#1b1210';
        ctx.beginPath(); ctx.ellipse(bx, 0, bw * 0.75, hh * 1.3, 0.08, 0, TAU); ctx.fill();
        ctx.fillStyle = '#ffffff';
        ctx.beginPath(); ctx.ellipse(bx, 0, bw * 0.5, hh * 1.3, 0.08, 0, TAU); ctx.fill();
      }
      break;
    case 'tang':
      ctx.strokeStyle = '#0a1638';
      ctx.lineWidth = hh * 0.26;
      ctx.lineCap = 'round';
      ctx.beginPath();
      ctx.moveTo(hl * 0.42, -hh * 0.4);
      ctx.quadraticCurveTo(-hl * 0.1, -hh * 0.05, -hl * 0.55, -hh * 0.55);
      ctx.moveTo(-hl * 0.05, -hh * 0.2);
      ctx.quadraticCurveTo(-hl * 0.4, hh * 0.35, -hl * 0.75, hh * 0.05);
      ctx.stroke();
      break;
    case 'stripes':
      ctx.fillStyle = 'rgba(30,30,30,0.85)';
      for (const bx of [hl * 0.35, -hl * 0.1, -hl * 0.55]) {
        ctx.save(); ctx.translate(bx, 0); ctx.rotate(0.25);
        ctx.fillRect(-hl * 0.05, -hh * 1.5, hl * 0.1, hh * 3);
        ctx.restore();
      }
      break;
    case 'spots':
      ctx.fillStyle = sp.spot;
      ctx.globalAlpha = 0.75;
      ctx.beginPath();
      for (const s of f.spots) { ctx.moveTo(s[0] * hl + s[2] * hl, s[1] * hh); ctx.arc(s[0] * hl, s[1] * hh, s[2] * hl, 0, TAU); }
      ctx.fill();
      ctx.globalAlpha = 1;
      break;
    case 'scales': {
      ctx.strokeStyle = 'rgba(0,60,60,0.25)';
      ctx.lineWidth = hl * 0.025;
      const st = hl * 0.16;
      ctx.beginPath();
      for (let x = -hl * 0.8; x < hl * 0.5; x += st) {
        for (let y = -hh; y < hh; y += st * 0.8) {
          const ox = (Math.round(y / (st * 0.8)) & 1) * st * 0.5;
          ctx.moveTo(x + ox + st * 0.5, y); ctx.arc(x + ox, y, st * 0.5, 0, Math.PI * 0.9);
        }
      }
      ctx.stroke();
      ctx.fillStyle = 'rgba(255,120,200,0.55)';
      ctx.beginPath(); ctx.ellipse(hl * 0.72, hh * 0.15, hl * 0.22, hh * 0.35, 0, 0, TAU); ctx.fill();
      break;
    }
    case 'bars':
      ctx.fillStyle = 'rgba(40,55,70,0.32)';
      for (let i = 0; i < 9; i++) ctx.fillRect(hl * 0.45 - i * hl * 0.15, -hh, hl * 0.07, hh * 0.95);
      break;
    case 'finlets':
      ctx.fillStyle = 'rgba(255,255,255,0.25)';
      ctx.fillRect(-hl, hh * 0.05, hl * 2, hh * 0.12);
      break;
    case 'player':
      ctx.strokeStyle = '#ffd447';
      ctx.lineWidth = hh * 0.17;
      ctx.lineCap = 'round';
      ctx.beginPath();
      ctx.moveTo(hl * 0.5, -hh * 0.52);
      ctx.quadraticCurveTo(-hl * 0.1, -hh * 0.3, -hl * 0.88, -hh * 0.05);
      ctx.stroke();
      ctx.fillStyle = 'rgba(255,255,255,0.55)';
      ctx.beginPath();
      for (const [sx, sy, sr] of [[-0.2, 0.25, 0.05], [-0.45, 0.1, 0.04], [0.05, 0.38, 0.035]]) {
        ctx.moveTo(sx * hl + sr * hl, sy * hh); ctx.arc(sx * hl, sy * hh, sr * hl, 0, TAU);
      }
      ctx.fill();
      break;
    case 'shark':
      break;
  }
}

function drawFish(f, isPlayer) {
  const sp = f.sp, sh = SHAPES[sp.shape];
  const hl = f.r * 1.3, hh = hl * sh.hh;
  const w = Math.sin(f.wag) * 0.3;
  const bend = w * hh * 0.45;
  const lw = Math.max(0.9 / zoom, hl * 0.03);
  let fs = f.faceS;
  if (Math.abs(fs) < 0.06) fs = fs < 0 ? -0.06 : 0.06;

  ctx.save();
  ctx.translate(f.x, f.y);
  ctx.scale(fs, 1);
  ctx.rotate(f.tilt);

  // tail
  ctx.save();
  ctx.translate(-hl * 0.84, bend);
  ctx.rotate(w * 1.3);
  drawTail(sh, hl, hh, sp.tailCol || sp.fin);
  ctx.restore();
  drawFins(sh, hl, hh, w, sp.fin);

  // body
  const g = ctx.createLinearGradient(0, -hh, 0, hh);
  g.addColorStop(0, sp.c[1]); g.addColorStop(0.45, sp.c[0]); g.addColorStop(1, sp.c[2]);
  bodyPath(hl, hh, sh, bend);
  ctx.fillStyle = g;
  ctx.fill();

  ctx.save();
  ctx.clip();
  drawPattern(f, sp, hl, hh);
  ctx.fillStyle = 'rgba(255,255,255,0.2)';
  ctx.beginPath(); ctx.ellipse(hl * 0.05, -hh * 0.5, hl * 0.55, hh * 0.17, -0.05, 0, TAU); ctx.fill();
  if (f.mouth > 0.04) {
    const m = f.mouth;
    ctx.fillStyle = '#3b0d16';
    ctx.beginPath();
    ctx.moveTo(hl * 1.05, -hh * 0.42 * m);
    ctx.lineTo(hl * (0.92 - 0.3 * m), hh * 0.05);
    ctx.lineTo(hl * 1.05, hh * 0.42 * m);
    ctx.closePath(); ctx.fill();
  }
  ctx.restore();

  bodyPath(hl, hh, sh, bend);
  ctx.lineWidth = lw;
  ctx.strokeStyle = 'rgba(0,18,36,0.38)';
  ctx.stroke();

  // gills
  ctx.strokeStyle = 'rgba(0,0,0,0.22)';
  ctx.lineWidth = lw * 0.9;
  ctx.beginPath();
  if (sh.tail === 'shark') {
    for (let i = 0; i < 4; i++) { const gx = hl * (0.4 - i * 0.06); ctx.moveTo(gx, -hh * 0.35); ctx.quadraticCurveTo(gx - hl * 0.03, 0, gx, hh * 0.3); }
  } else {
    ctx.arc(hl * 0.62, 0, hl * 0.28, Math.PI * 0.72, Math.PI * 1.28);
  }
  ctx.stroke();

  // pectoral fin
  ctx.save();
  if (sh.tail === 'shark') {
    ctx.translate(hl * 0.3, hh * 0.55);
    ctx.fillStyle = sp.fin;
    ctx.beginPath(); ctx.moveTo(0, 0); ctx.lineTo(-hl * 0.32, hh * 1.3); ctx.lineTo(-hl * 0.18, 0); ctx.closePath(); ctx.fill();
  } else {
    ctx.translate(hl * 0.22, hh * 0.25);
    ctx.rotate(0.6 + Math.sin(f.wag * 1.3) * 0.35);
    ctx.globalAlpha = 0.85;
    ctx.fillStyle = sp.fin;
    ctx.beginPath(); ctx.ellipse(-hl * 0.12, 0, hl * 0.17, Math.max(hh * 0.12, 0.5), 0, 0, TAU); ctx.fill();
  }
  ctx.restore();

  // eye
  const ex = hl * (sh.tail === 'shark' ? 0.62 : 0.56), ey = -hh * 0.22;
  const er = Math.max(hl * (sp.eye || sh.eye), 1.1);
  const angry = f.state === 'chase';
  if (isPlayer && f.blink > 0) {
    ctx.strokeStyle = '#0b0f14'; ctx.lineWidth = er * 0.35;
    ctx.beginPath(); ctx.moveTo(ex - er, ey); ctx.lineTo(ex + er, ey); ctx.stroke();
  } else if (isPlayer && f.chomp > 0) {
    ctx.strokeStyle = '#0b0f14'; ctx.lineWidth = er * 0.38; ctx.lineCap = 'round';
    ctx.beginPath(); ctx.moveTo(ex - er * 0.9, ey + er * 0.3); ctx.lineTo(ex, ey - er * 0.5); ctx.lineTo(ex + er * 0.9, ey + er * 0.3); ctx.stroke();
  } else {
    ctx.fillStyle = sh.tail === 'shark' ? '#0d1116' : '#ffffff';
    ctx.beginPath(); ctx.arc(ex, ey, er, 0, TAU); ctx.fill();
    if (sh.tail !== 'shark') {
      if (angry) { ctx.fillStyle = '#e0283c'; ctx.beginPath(); ctx.arc(ex + er * 0.22, ey, er * 0.66, 0, TAU); ctx.fill(); }
      ctx.fillStyle = '#0b0f14';
      ctx.beginPath(); ctx.arc(ex + er * 0.25, ey, er * (angry ? 0.36 : 0.6), 0, TAU); ctx.fill();
    }
    ctx.fillStyle = '#ffffff';
    ctx.beginPath(); ctx.arc(ex + er * 0.05, ey - er * 0.32, er * 0.26, 0, TAU); ctx.fill();
  }
  if (angry) {
    ctx.strokeStyle = '#1a0d0d'; ctx.lineWidth = er * 0.45; ctx.lineCap = 'round';
    ctx.beginPath(); ctx.moveTo(ex - er * 1.3, ey - er * 1.7); ctx.lineTo(ex + er * 1.1, ey - er * 0.95); ctx.stroke();
  }
  ctx.restore();
}

function rockPath(r) {
  const pts = r.pts, n = pts.length;
  const P = (i) => { const [a, m] = pts[(i + n) % n]; return [r.x + Math.cos(a) * r.s * m * 1.25, r.y + Math.sin(a) * r.s * m * 0.8]; };
  ctx.beginPath();
  let [px, py] = P(0), [qx, qy] = P(1);
  ctx.moveTo((px + qx) / 2, (py + qy) / 2);
  for (let i = 1; i <= n; i++) {
    [px, py] = P(i); [qx, qy] = P(i + 1);
    ctx.quadraticCurveTo(px, py, (px + qx) / 2, (py + qy) / 2);
  }
  ctx.closePath();
}

function drawDecorItem(d) {
  switch (d.type) {
    case 'kelp': {
      const segs = 16, sl = d.h / segs;
      const L = [], R = [];
      let x = d.x, y = d.y, ang = -Math.PI / 2;
      const pts = [[x, y]];
      for (let i = 1; i <= segs; i++) {
        ang = -Math.PI / 2 + Math.sin(time * 0.8 + d.phase + i * 0.32) * 0.16 * (0.3 + i / segs);
        x += Math.cos(ang) * sl; y += Math.sin(ang) * sl;
        pts.push([x, y]);
      }
      for (let i = 0; i < pts.length; i++) {
        const a = pts[Math.max(0, i - 1)], b = pts[Math.min(pts.length - 1, i + 1)];
        const nx = -(b[1] - a[1]), ny = b[0] - a[0], nl = Math.hypot(nx, ny) || 1;
        const hw = d.w * (1 - (i / segs) * 0.75) * 0.5;
        L.push([pts[i][0] + (nx / nl) * hw, pts[i][1] + (ny / nl) * hw]);
        R.push([pts[i][0] - (nx / nl) * hw, pts[i][1] - (ny / nl) * hw]);
      }
      const col = d.tone < 0.5 ? '#2e8b57' : '#4a9a3c';
      ctx.fillStyle = col;
      ctx.beginPath();
      ctx.moveTo(L[0][0], L[0][1]);
      for (const p of L) ctx.lineTo(p[0], p[1]);
      for (let i = R.length - 1; i >= 0; i--) ctx.lineTo(R[i][0], R[i][1]);
      ctx.closePath(); ctx.fill();
      ctx.fillStyle = d.tone < 0.5 ? '#3aa06a' : '#62b04e';
      ctx.beginPath();
      for (let i = 2; i < pts.length - 1; i += 2) {
        const s = i % 4 === 0 ? 1 : -1, rot = s * 0.6 + Math.sin(time * 1.2 + d.phase + i) * 0.25;
        const lx = pts[i][0] + s * d.w * 0.9, ly = pts[i][1];
        ctx.moveTo(lx + Math.cos(rot) * d.w * 1.1, ly + Math.sin(rot) * d.w * 1.1);
        ctx.ellipse(lx, ly, d.w * 1.1, d.w * 0.38, rot, 0, TAU);
      }
      ctx.fill();
      break;
    }
    case 'rock': {
      const c = d.tone < 0.5 ? ['#a3a9b4', '#5d6370', '#2b2f38'] : ['#b19d86', '#6c5c4c', '#332b24'];
      const g = ctx.createLinearGradient(d.x - d.s * 0.7, d.y - d.s, d.x + d.s * 0.5, d.y + d.s * 0.4);
      g.addColorStop(0, c[0]); g.addColorStop(0.5, c[1]); g.addColorStop(1, c[2]);
      rockPath(d);
      ctx.fillStyle = g; ctx.fill();
      ctx.lineWidth = Math.max(1 / zoom, d.s * 0.03);
      ctx.strokeStyle = 'rgba(8,14,24,0.4)'; ctx.stroke();
      ctx.save();
      ctx.clip();
      ctx.fillStyle = 'rgba(255,255,255,0.13)';
      ctx.beginPath(); ctx.ellipse(d.x - d.s * 0.4, d.y - d.s * 0.62, d.s * 0.55, d.s * 0.2, -0.25, 0, TAU); ctx.fill();
      ctx.fillStyle = 'rgba(0,0,0,0.16)';
      ctx.beginPath();
      for (let i = 0; i < 7; i++) {
        const sx = d.x + (hash(d.x + i * 3.3) - 0.5) * d.s * 1.8, sy = d.y - hash(d.x * 1.3 + i) * d.s * 0.7, sr = d.s * (0.04 + hash(d.x + i) * 0.06);
        ctx.moveTo(sx + sr, sy); ctx.arc(sx, sy, sr, 0, TAU);
      }
      ctx.fill();
      ctx.fillStyle = d.tone < 0.5 ? 'rgba(90,175,100,0.55)' : 'rgba(150,170,80,0.5)';
      ctx.beginPath(); ctx.ellipse(d.x - d.s * 0.05, d.y - d.s * 0.82, d.s * 0.75, d.s * 0.2, 0, 0, TAU); ctx.fill();
      ctx.restore();
      break;
    }
    case 'branch': {
      ctx.strokeStyle = d.col; ctx.lineCap = 'round';
      ctx.save(); ctx.translate(d.x, d.y);
      const sway = Math.sin(time * 0.7 + d.x * 0.01) * 0.02;
      ctx.rotate(sway);
      for (let l = 0; l < d.levels.length; l++) {
        ctx.lineWidth = d.s * 0.13 * Math.pow(0.7, l);
        ctx.beginPath();
        for (const s of d.levels[l]) { ctx.moveTo(s[0], s[1]); ctx.lineTo(s[2], s[3]); }
        ctx.stroke();
      }
      ctx.fillStyle = d.tip;
      ctx.beginPath();
      for (const s of d.levels[d.levels.length - 1]) { ctx.moveTo(s[2] + d.s * 0.035, s[3]); ctx.arc(s[2], s[3], d.s * 0.035, 0, TAU); }
      ctx.fill();
      ctx.restore();
      break;
    }
    case 'fan': {
      const sway = Math.sin(time * 0.6 + d.phase) * 0.05;
      ctx.save(); ctx.translate(d.x, d.y); ctx.rotate(sway);
      ctx.fillStyle = d.col; ctx.globalAlpha = 0.35;
      ctx.beginPath(); ctx.moveTo(0, 0);
      for (const [a, m] of d.ribs) ctx.lineTo(Math.cos(a) * d.s * m, Math.sin(a) * d.s * m);
      ctx.closePath(); ctx.fill();
      ctx.globalAlpha = 1; ctx.strokeStyle = d.col; ctx.lineWidth = d.s * 0.03;
      ctx.beginPath();
      for (const [a, m] of d.ribs) { ctx.moveTo(0, 0); ctx.quadraticCurveTo(Math.cos(a + 0.1) * d.s * m * 0.5, Math.sin(a + 0.1) * d.s * m * 0.5, Math.cos(a) * d.s * m, Math.sin(a) * d.s * m); }
      ctx.stroke();
      ctx.lineWidth = d.s * 0.015;
      ctx.beginPath();
      for (const k of [0.4, 0.62, 0.82]) ctx.arc(0, 0, d.s * k, -Math.PI + 0.4, -0.4);
      ctx.stroke();
      ctx.restore();
      break;
    }
    case 'brain': {
      const g = ctx.createRadialGradient(d.x - d.s * 0.2, d.y - d.s * 0.4, d.s * 0.05, d.x, d.y, d.s * 0.8);
      g.addColorStop(0, '#ffffff'); g.addColorStop(0.15, d.col); g.addColorStop(1, 'rgba(40,20,40,1)');
      ctx.save();
      ctx.beginPath(); ctx.ellipse(d.x, d.y, d.s * 0.7, d.s * 0.48, 0, Math.PI, TAU); ctx.closePath();
      ctx.fillStyle = g; ctx.fill(); ctx.clip();
      ctx.strokeStyle = 'rgba(0,0,0,0.22)'; ctx.lineWidth = d.s * 0.035;
      ctx.beginPath();
      for (let k = 0; k < 5; k++) {
        const yy = d.y - d.s * (0.08 + k * 0.09);
        ctx.moveTo(d.x - d.s * 0.7, yy);
        for (let xx = -0.7; xx <= 0.7; xx += 0.07) ctx.lineTo(d.x + xx * d.s, yy + Math.sin(xx * 18 + d.seed + k) * d.s * 0.025);
      }
      ctx.stroke();
      ctx.restore();
      break;
    }
    case 'tube': {
      for (const [dx, h, w] of d.tubes) {
        const x = d.x + dx;
        const g = ctx.createLinearGradient(x - w, 0, x + w, 0);
        g.addColorStop(0, d.col); g.addColorStop(0.5, '#ffffff55'); g.addColorStop(1, d.col);
        ctx.fillStyle = d.col;
        ctx.beginPath(); ctx.roundRect(x - w, d.y - h, w * 2, h, w * 0.6); ctx.fill();
        ctx.fillStyle = g; ctx.globalAlpha = 0.5; ctx.fill(); ctx.globalAlpha = 1;
        ctx.fillStyle = 'rgba(30,10,20,0.7)';
        ctx.beginPath(); ctx.ellipse(x, d.y - h + w * 0.25, w * 0.75, w * 0.3, 0, 0, TAU); ctx.fill();
      }
      break;
    }
    case 'anem': {
      const col = `hsl(${d.hue},${d.deep ? 90 : 75}%,${d.deep ? 65 : 62}%)`;
      ctx.fillStyle = `hsl(${d.hue},45%,35%)`;
      ctx.beginPath(); ctx.ellipse(d.x, d.y - d.s * 0.12, d.s * 0.32, d.s * 0.2, 0, 0, TAU); ctx.fill();
      ctx.strokeStyle = col; ctx.lineWidth = d.s * 0.07; ctx.lineCap = 'round';
      ctx.beginPath();
      const tips = [];
      for (let i = 0; i < d.n; i++) {
        const a = -Math.PI + 0.35 + (i / (d.n - 1)) * (Math.PI - 0.7);
        const sw = Math.sin(time * 1.4 + d.phase + i * 0.6) * 0.3;
        const ex = d.x + Math.cos(a + sw * 0.4) * d.s * 0.75, ey = d.y - d.s * 0.2 + Math.sin(a) * d.s * 0.75;
        ctx.moveTo(d.x + Math.cos(a) * d.s * 0.2, d.y - d.s * 0.2);
        ctx.quadraticCurveTo(d.x + Math.cos(a) * d.s * 0.5 + sw * d.s * 0.2, d.y - d.s * 0.2 + Math.sin(a) * d.s * 0.4, ex, ey);
        tips.push([ex, ey]);
      }
      ctx.stroke();
      ctx.fillStyle = `hsl(${d.hue},100%,85%)`;
      ctx.beginPath();
      for (const [x, y] of tips) { ctx.moveTo(x + d.s * 0.05, y); ctx.arc(x, y, d.s * 0.05, 0, TAU); }
      ctx.fill();
      break;
    }
    case 'grass': {
      ctx.strokeStyle = '#3f9a55'; ctx.lineWidth = d.s * 0.07; ctx.lineCap = 'round';
      ctx.beginPath();
      for (let i = 0; i < d.n; i++) {
        const bx = d.x + (i - d.n / 2) * d.s * 0.12;
        const sw = Math.sin(time * 1.3 + d.phase + i) * d.s * 0.25;
        ctx.moveTo(bx, d.y);
        ctx.quadraticCurveTo(bx + sw * 0.3, d.y - d.s * 0.6, bx + sw, d.y - d.s * (0.8 + (i % 3) * 0.15));
      }
      ctx.stroke();
      break;
    }
  }
}

function drawJelly(j) {
  const pulse = 0.5 + 0.5 * Math.sin(j.phase);
  const bw = j.r * (1 + pulse * 0.14), bh = j.r * (0.82 - pulse * 0.14);
  ctx.strokeStyle = `hsla(${j.hue},90%,78%,0.5)`;
  ctx.lineWidth = Math.max(1 / zoom, j.r * 0.05);
  ctx.beginPath();
  for (let i = 0; i < 7; i++) {
    const x0 = j.x + (i / 6 - 0.5) * bw * 1.5;
    ctx.moveTo(x0, j.y);
    for (let s = 1; s <= 9; s++) {
      ctx.lineTo(x0 + Math.sin(time * 2.4 + s * 0.65 + i * 1.3) * j.r * 0.12 * (s / 4), j.y + s * j.r * 0.3);
    }
  }
  ctx.stroke();
  ctx.strokeStyle = `hsla(${j.hue},90%,85%,0.7)`;
  ctx.lineWidth = Math.max(1.5 / zoom, j.r * 0.13);
  ctx.beginPath();
  for (let i = 0; i < 3; i++) {
    const x0 = j.x + (i - 1) * bw * 0.22;
    ctx.moveTo(x0, j.y);
    ctx.bezierCurveTo(x0 + Math.sin(time * 1.6 + i) * j.r * 0.3, j.y + j.r * 0.8, x0 - Math.sin(time * 1.3 + i) * j.r * 0.3, j.y + j.r * 1.4, x0, j.y + j.r * 1.9);
  }
  ctx.stroke();
  const g = ctx.createRadialGradient(j.x, j.y - bh * 0.4, 0, j.x, j.y, bw);
  g.addColorStop(0, `hsla(${j.hue},100%,93%,0.92)`);
  g.addColorStop(0.55, `hsla(${j.hue},90%,70%,0.6)`);
  g.addColorStop(1, `hsla(${j.hue},85%,55%,0.3)`);
  ctx.fillStyle = g;
  ctx.beginPath();
  ctx.ellipse(j.x, j.y, bw, bh, 0, Math.PI, TAU);
  const sc = 6;
  for (let i = 0; i < sc; i++) {
    const xa = j.x + bw - (i + 0.5) * (2 * bw / sc), xb = j.x + bw - (i + 1) * (2 * bw / sc);
    ctx.quadraticCurveTo(xa, j.y + bh * 0.22, xb, j.y);
  }
  ctx.closePath(); ctx.fill();
  ctx.strokeStyle = `hsla(${j.hue},100%,92%,0.6)`;
  ctx.lineWidth = Math.max(1 / zoom, j.r * 0.04);
  ctx.stroke();
}

function drawPearl(p) {
  const bob = Math.sin(time * 2 + p.ph) * p.r * 0.4;
  if (p.life < 3 && Math.floor(time * 8) % 2) return;
  const x = p.x, y = p.y + bob;
  const g = ctx.createRadialGradient(x - p.r * 0.35, y - p.r * 0.35, p.r * 0.05, x, y, p.r);
  g.addColorStop(0, '#ffffff'); g.addColorStop(0.5, '#ffe6f5'); g.addColorStop(1, '#c9a8e8');
  ctx.fillStyle = g;
  ctx.beginPath(); ctx.arc(x, y, p.r, 0, TAU); ctx.fill();
  ctx.save();
  ctx.translate(x, y); ctx.rotate(time * 1.5);
  ctx.fillStyle = 'rgba(255,255,255,0.9)';
  for (let i = 0; i < 4; i++) {
    ctx.rotate(Math.PI / 2);
    ctx.beginPath(); ctx.moveTo(0, -p.r * 0.15); ctx.lineTo(p.r * 2.2, 0); ctx.lineTo(0, p.r * 0.15); ctx.fill();
  }
  ctx.restore();
}


const mode = new URLSearchParams(location.search).get('mode') || 'fish';
ctx.fillStyle='#092c43';ctx.fillRect(0,0,1200,800);
function sample(sp,x,y,r){drawFish({sp,x,y,r,faceS:1,tilt:0,wag:0,mouth:0,state:'wander',blink:0,chomp:0,spots:Array.from({length:16},(_,i)=>[(hash(i*3)-.5)*1.5,(hash(i*5)-.5)*1.6,.025+hash(i*7)*.035])},sp===PLAYER_SP);}
if(mode==='fish'){
  sample(PLAYER_SP,228,205,88);sample(SPECIES.clown,628,205,84);sample(SPECIES.tang,1028,205,80);
  sample(SPECIES.puffer,228,605,80);sample(SPECIES.angel,628,605,66);sample(SHARK_SP,1028,605,92);
}else{
  drawJelly({x:280,y:126,r:66,hue:285,phase:0});
  drawPearl({x:920,y:208,r:45,ph:0,life:10});
  drawDecorItem({type:'kelp',x:235,y:750,h:304,w:25,phase:0,tone:.25});
  drawDecorItem({type:'kelp',x:295,y:750,h:236,w:20,phase:1,tone:.7});
  let levels=[];function branch(x,y,a,l,n){let xx=x+Math.cos(a)*l,yy=y+Math.sin(a)*l;(levels[n]=levels[n]||[]).push([x,y,xx,yy]);if(n<3)for(let j=0;j<2;j++)branch(xx,yy,a+(j?1:-1)*.5,l*.69,n+1)}branch(0,0,-Math.PI/2,72,0);
  drawDecorItem({type:'branch',x:872,y:724,s:115,col:'#ff6f91',tip:'#ffd6e8',levels});
  drawDecorItem({type:'tube',x:984,y:722,s:100,col:'#c77dff',tubes:[[-30,95,15],[0,138,18],[30,82,14]]});
  drawDecorItem({type:'brain',x:900,y:748,s:76,col:'#ff9f5a',seed:4});
}
