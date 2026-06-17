// ============================================================================
// MMO VERTICAL SLICE v2 — SERVIDOR AUTORITATIVO + SISTEMA DE CASTING
// Novo nesta versao: abilities com cast time, interrupcao por dano, mana.
// A logica de combate e fatorada em funcoes puras para ser testavel sem rede.
// ============================================================================

const express = require('express');
const { createServer } = require('http');
const { Server } = require('socket.io');
const path = require('path');

const app = express();
const httpServer = createServer(app);
const io = new Server(httpServer, { cors: { origin: '*' } });
app.use(express.static(path.join(__dirname, 'public')));

// ----------------------------------------------------------------------------
// CONSTANTES
// ----------------------------------------------------------------------------
const TICK_RATE = 20;
const TICK_MS = 1000 / TICK_RATE;
const MAP_W = 800, MAP_H = 600;
const MAX_SPEED = 200;
const MAX_HP = 100;
const MAX_MANA = 100;
const MANA_REGEN_PER_SEC = 12;
const RESPAWN_MS = 3000;
const PLAYER_RADIUS = 18; // raio para colisão player-player

// Definicao de abilities — mesma ideia do SKILL_DEFINITIONS.md, em miniatura.
// castTime 0 = instantaneo. interruptible = cancela se tomar dano durante o cast.
const ABILITIES = {
  slash: { name: 'Slash',      type: 'melee',  castTime: 0,   cooldown: 600,  mana: 0,  range: 60,  damage: 12, interruptible: false },
  heavy: { name: 'Heavy Blow', type: 'melee',  castTime: 400, cooldown: 1500, mana: 15, range: 60,  damage: 30, interruptible: true  },
  bolt:  { name: 'Frost Bolt', type: 'ranged', castTime: 900, cooldown: 1200, mana: 20, range: 320, damage: 22, interruptible: true  },
};

// ----------------------------------------------------------------------------
// ESTADO
// ----------------------------------------------------------------------------
const players = new Map();

function spawnPlayer(id, name) {
  return {
    id, name: name || 'Player',
    x: Math.random() * (MAP_W - 100) + 50,
    y: Math.random() * (MAP_H - 100) + 50,
    hp: MAX_HP, mana: MAX_MANA, dead: false,
    cooldowns: {},
    casting: null,
    lastSeenAt: Date.now(),
    rejectedMoves: 0,
  };
}

function clamp(v, lo, hi) { return Math.min(hi, Math.max(lo, v)); }
function dist(a, b) { return Math.hypot(a.x - b.x, a.y - b.y); }

// ----------------------------------------------------------------------------
// MOVIMENTO (inalterado — ja comprovado na v1)
// ----------------------------------------------------------------------------
function handleMove(player, msg, now = Date.now()) {
  if (player.dead) return;
  const dt = Math.max((now - player.lastSeenAt) / 1000, 0.001);
  player.lastSeenAt = now;
  const tx = clamp(Number(msg.x), 0, MAP_W);
  const ty = clamp(Number(msg.y), 0, MAP_H);
  if (Number.isNaN(tx) || Number.isNaN(ty)) return;
  const dx = tx - player.x, dy = ty - player.y;
  const d = Math.hypot(dx, dy);
  const maxDist = MAX_SPEED * dt * 1.5 + 5;
  if (d > maxDist) {
    player.rejectedMoves++;
    const r = maxDist / d;
    player.x += dx * r; player.y += dy * r;
  } else { player.x = tx; player.y = ty; }
  resolvePlayerCollisions(player);
}

// ----------------------------------------------------------------------------
// APLICAR DANO (caminho unico de dano — tambem dispara interrupcao de cast)
// ----------------------------------------------------------------------------
function applyDamage(target, amount, events) {
  if (target.dead) return;
  target.hp = Math.max(0, target.hp - amount);
  if (target.casting) {
    const ab = ABILITIES[target.casting.abilityId];
    if (ab && ab.interruptible) {
      target.casting = null;
      if (events) events.interrupts.push({ id: target.id, ability: ab.name });
    }
  }
  if (target.hp <= 0 && !target.dead) {
    target.dead = true;
    target.casting = null;
    if (events) events.deaths.push({ id: target.id });
    scheduleRespawn(target);
  }
}

// ----------------------------------------------------------------------------
// INICIAR CAST — valida cooldown, mana, estado. Consome mana no INICIO
// (ser interrompido custa a mana: esse e o risco, estilo hardcore).
// ----------------------------------------------------------------------------
function startCast(player, abilityId, tx, ty, now = Date.now()) {
  if (player.dead) return { rejected: 'dead' };
  const ab = ABILITIES[abilityId];
  if (!ab) return { rejected: 'unknown_ability' };
  if (player.casting) return { rejected: 'already_casting' };
  if ((player.cooldowns[abilityId] || 0) > now) return { rejected: 'cooldown' };
  if (player.mana < ab.mana) return { rejected: 'no_mana' };

  player.mana -= ab.mana;
  player.cooldowns[abilityId] = now + ab.cooldown;

  if (ab.castTime === 0) {
    const events = newEvents();
    resolveAbility(player, abilityId, tx, ty, events);
    return { resolved: true, events };
  }
  player.casting = { abilityId, endsAt: now + ab.castTime, tx, ty };
  return { casting: true, endsAt: player.casting.endsAt };
}

// ----------------------------------------------------------------------------
// RESOLVER ABILITY — aplica o efeito (no fim do cast ou imediato)
// ----------------------------------------------------------------------------
function resolveAbility(caster, abilityId, tx, ty, events) {
  const ab = ABILITIES[abilityId];
  if (!ab || caster.dead) return;
  if (ab.type === 'melee') {
    for (const t of players.values()) {
      if (t.id === caster.id || t.dead) continue;
      if (dist(caster, t) <= ab.range) {
        applyDamage(t, ab.damage, events);
        events.hits.push({ from: caster.id, to: t.id, ability: ab.name, damage: ab.damage, hp: t.hp });
      }
    }
  } else if (ab.type === 'ranged') {
    let best = null, bestD = Infinity;
    for (const t of players.values()) {
      if (t.id === caster.id || t.dead) continue;
      const d = dist(caster, t);
      if (d <= ab.range && d < bestD) { best = t; bestD = d; }
    }
    if (best) {
      applyDamage(best, ab.damage, events);
      events.hits.push({ from: caster.id, to: best.id, ability: ab.name, damage: ab.damage, hp: best.hp });
    }
  }
}

// ----------------------------------------------------------------------------
// RESOLVER CASTS PRONTOS — chamado todo tick
// ----------------------------------------------------------------------------
function resolveDueCasts(now, events) {
  for (const p of players.values()) {
    if (p.casting && now >= p.casting.endsAt) {
      const { abilityId, tx, ty } = p.casting;
      p.casting = null;
      resolveAbility(p, abilityId, tx, ty, events);
    }
  }
}

function regenMana(now, dtSec) {
  for (const p of players.values()) {
    if (!p.dead && p.mana < MAX_MANA) p.mana = Math.min(MAX_MANA, p.mana + MANA_REGEN_PER_SEC * dtSec);
  }
}

function scheduleRespawn(player) {
  setTimeout(() => {
    if (!players.has(player.id)) return;
    Object.assign(player, { hp: MAX_HP, mana: MAX_MANA, dead: false, casting: null,
      x: Math.random() * (MAP_W - 100) + 50, y: Math.random() * (MAP_H - 100) + 50 });
  }, RESPAWN_MS);
}

function newEvents() { return { hits: [], interrupts: [], deaths: [] }; }

// ----------------------------------------------------------------------------
// COLISÃO PLAYER-PLAYER — empurra jogadores que se sobrepõem
// ----------------------------------------------------------------------------
function resolvePlayerCollisions(mover) {
  const minDist = PLAYER_RADIUS * 2;
  for (const other of players.values()) {
    if (other.id === mover.id || other.dead) continue;
    const dx = mover.x - other.x;
    const dy = mover.y - other.y;
    const d = Math.hypot(dx, dy);
    if (d < minDist && d > 0) {
      const overlap = (minDist - d) / 2;
      const nx = dx / d, ny = dy / d;
      // Mover apenas o solicitante (o outro se move quando for a vez dele)
      mover.x = clamp(mover.x + nx * overlap * 2, 0, MAP_W);
      mover.y = clamp(mover.y + ny * overlap * 2, 0, MAP_H);
    }
  }
}

// ----------------------------------------------------------------------------
// SOCKET.IO
// ----------------------------------------------------------------------------
io.on('connection', (socket) => {
  socket.on('join', (data) => {
    players.set(socket.id, spawnPlayer(socket.id, data && data.name));
    socket.emit('joined', { id: socket.id, world: { w: MAP_W, h: MAP_H }, abilities: ABILITIES });
  });
  socket.on('move', (msg) => { const p = players.get(socket.id); if (p) handleMove(p, msg); });
  // Atalho legado: 'attack' executa slash contra o inimigo mais próximo
  socket.on('attack', () => {
    const p = players.get(socket.id);
    if (!p) return;
    let nearest = null, nearestD = Infinity;
    for (const t of players.values()) {
      if (t.id === p.id || t.dead) continue;
      const d = dist(p, t);
      if (d < nearestD) { nearest = t; nearestD = d; }
    }
    if (!nearest) return;
    const res = startCast(p, 'slash', nearest.x, nearest.y);
    if (res.events) emitEvents(res.events);
  });
  socket.on('cast', (msg) => {
    const p = players.get(socket.id);
    if (!p || !msg) return;
    const res = startCast(p, msg.abilityId, Number(msg.tx), Number(msg.ty));
    if (res.events) emitEvents(res.events);
    socket.emit('castResult', { abilityId: msg.abilityId, rejected: res.rejected || null,
      casting: !!res.casting, resolved: !!res.resolved });
  });
  socket.on('ping', (ts) => socket.emit('pong', ts));
  socket.on('disconnect', () => players.delete(socket.id));
});

function emitEvents(events) {
  if (events.hits.length) io.emit('hits', events.hits);
  if (events.interrupts.length) io.emit('interrupts', events.interrupts);
}

// ----------------------------------------------------------------------------
// GAME LOOP
// ----------------------------------------------------------------------------
let lastTick = Date.now();
setInterval(() => {
  const now = Date.now();
  const dtSec = (now - lastTick) / 1000; lastTick = now;
  const events = newEvents();
  resolveDueCasts(now, events);
  regenMana(now, dtSec);
  emitEvents(events);
  const snapshot = [];
  for (const p of players.values()) {
    snapshot.push({
      id: p.id, name: p.name, x: Math.round(p.x), y: Math.round(p.y),
      hp: Math.round(p.hp), mana: Math.round(p.mana), dead: p.dead,
      casting: p.casting ? { abilityId: p.casting.abilityId,
        remaining: Math.max(0, p.casting.endsAt - now),
        total: ABILITIES[p.casting.abilityId].castTime } : null,
    });
  }
  io.emit('state', { t: now, players: snapshot });
}, TICK_MS).unref(); // unref permite que testes saiam sem precisar matar o processo

const PORT = process.env.PORT || 3000;
if (require.main === module) {
  httpServer.listen(PORT, () => console.log(`MMO slice v2 em http://localhost:${PORT}`));
}

module.exports = {
  httpServer, players, ABILITIES,
  spawnPlayer, handleMove, startCast, applyDamage, resolveDueCasts, resolveAbility,  newEvents,
  CONFIG: { MAX_SPEED, MAX_HP, MAX_MANA, MAP_W, MAP_H },
};
