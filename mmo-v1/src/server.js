// ============================================================
// MMO v1 — Servidor Phase 1
// Stack: Express + Socket.IO + WorldManager + Managers
// Para rodar: npm install && node src/server.js
// ============================================================
require('dotenv').config();
const express    = require('express');
const { createServer } = require('http');
const { Server } = require('socket.io');
const cors       = require('cors');
const path       = require('path');

const { PORT }        = require('./config/constants');
const WorldManager    = require('./managers/WorldManager');
const PlayerManager   = require('./managers/PlayerManager');
const CombatEngine    = require('./managers/CombatEngine');
const MonsterManager  = require('./managers/MonsterManager');

// ----- App -----
const app  = express();
const http = createServer(app);
const io   = new Server(http, { cors: { origin: '*' } });
app.use(cors());
app.use(express.static(path.join(__dirname, '../public')));

// ----- Managers -----
const world   = new WorldManager(io);
const players = new PlayerManager(world);
const combat  = new CombatEngine(world, players);
const monsters = new MonsterManager(world, combat);

// Liga o AI tick ao loop do world
world.on = world.on || (() => {}); // guard
world._origTick = world._tick.bind(world);
world._tick = function() {
  const now = Date.now();
  const dtSec = (now - this._lastTick) / 1000;
  players.regenTick(dtSec);
  combat.resolveDueCasts(now);
  monsters.aiTick(now);
  this._origTick();
};

// ----- Socket.IO Events -----
io.on('connection', (socket) => {
  console.log(`[+] ${socket.id}`);

  socket.on('player:join', ({ name, playerClass }) => {
    const state = players.createPlayer(socket.id, { name, playerClass });
    socket.emit('player:joined', {
      id: socket.id,
      world: { w: 2400, h: 1800 },
      abilities: combat.getSkillCatalog(),
      state,
    });
    console.log(`  join: ${name} (${playerClass})`);
  });

  socket.on('player:move', ({ x, y }) => {
    players.handleMove(socket.id, { x, y });
  });

  socket.on('skill:use', ({ skillId, tx, ty }) => {
    const result = combat.startCast(socket.id, skillId, tx, ty);
    socket.emit('skill:result', { skillId, ...result });
  });

  socket.on('item:pickup', ({ itemId }) => {
    const item = world.items.get(itemId);
    const p    = world.getPlayer(socket.id);
    if (!item || !p || p.dead) return;
    if (Math.hypot(item.x - p.x, item.y - p.y) > 60) return;
    if (p.inventory.length >= 30) { socket.emit('error', { msg: 'Inventário cheio' }); return; }
    p.inventory.push({ id: item.id, type: item.type });
    world.removeItem(itemId);
    socket.emit('item:picked', { item });
  });

  socket.on('chat:send', ({ channel, message }) => {
    const p = world.getPlayer(socket.id);
    if (!p || !message || message.length > 200) return;
    const payload = { from: p.name, message: message.slice(0, 200), ts: Date.now() };
    if (channel === 'global') io.emit('chat:message', { channel: 'global', ...payload });
    else socket.emit('chat:message', { channel, ...payload }); // fallback
  });

  socket.on('disconnect', () => {
    players.removePlayer(socket.id);
    console.log(`[-] ${socket.id}`);
  });
});

// ----- Spawn monstros iniciais -----
function populateWorld() {
  for (let i = 0; i < 15; i++) monsters.spawnRandom();
  console.log('[World] 15 monstros spawnados');
}

// ----- Start -----
world.start();
http.listen(PORT, () => {
  console.log(`\nMMO v1 rodando em http://localhost:${PORT}`);
  populateWorld();
});
