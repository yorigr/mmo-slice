// ============================================================
// MMO v2 — Servidor com Zonas Isoladas e Reconexão
// Stack: Express + Socket.IO v4 + ZoneManager + SessionManager
//
// Melhorias em relação ao v1:
//   - ZoneManager: zonas isoladas (rooms do Socket.IO); cada zona tem seu
//     próprio game loop, monstros e estado. Base para dungeons, instâncias, PvP.
//   - SessionManager: reconexão por token em até 30s preserva posição/XP/gold.
//   - PvM combat: jogadores agora PODEM atacar e matar monstros (bug crítico fixado).
//   - XP e loot: onMonsterDeath() agora é chamado corretamente.
//   - Auto-respawn: MONSTER_SPAWN_INTERVAL_MS usado para repopular zonas.
//   - Broadcasts escopados: io.to(zoneId) em vez de io.emit (global).
// ============================================================
require('dotenv').config();
const express         = require('express');
const { createServer } = require('http');
const { Server }      = require('socket.io');
const cors            = require('cors');
const path            = require('path');
const { v4: uuidv4 } = require('uuid');

const { PORT, MAP_W, MAP_H } = require('./config/constants');
const { ZoneManager }        = require('./managers/ZoneManager');
const SessionManager         = require('./managers/SessionManager');

// ----- App -----
const app  = express();
const http = createServer(app);
const io   = new Server(http, { cors: { origin: '*' } });

app.use(cors());
app.use(express.static(path.join(__dirname, '../public')));

// ----- Managers globais -----
const zones    = new ZoneManager(io);
const sessions = new SessionManager();

// Mapa socketId → sessionToken para salvar estado na desconexão
const socketTokens = new Map();

// Pré-cria a zona padrão (inicia game loop + popula com monstros)
zones.getOrCreate('overworld');

// ----- Socket.IO Events -----
io.on('connection', (socket) => {
  console.log(`[+] ${socket.id}`);

  // ── player:join ─────────────────────────────────────────────────────────────
  // Payload: { name, playerClass, sessionToken?, zoneId? }
  //   - sessionToken: se enviado e válido, restaura o estado salvo ao desconectar.
  //   - zoneId: zona desejada (padrão: 'overworld').
  //
  // Resposta: player:joined
  //   { id, sessionToken, world: {w,h}, abilities, state }
  //   O cliente deve armazenar sessionToken (Unity: PlayerPrefs) e reenviá-lo
  //   no próximo player:join para ativar a reconexão.
  socket.on('player:join', ({ name, playerClass, sessionToken, zoneId: reqZoneId } = {}) => {
    const zoneId = reqZoneId || 'overworld';

    // Tenta restaurar sessão (30s de janela)
    let restoredState = null;
    if (sessionToken) {
      const session = sessions.restore(sessionToken);
      if (session) {
        restoredState = session.state;
        console.log(`  [reconnect] ${name} (${socket.id}) → sessão restaurada`);
      }
    }

    // Gera novo token para esta sessão
    const newToken = uuidv4();
    socketTokens.set(socket.id, newToken);

    // Cria player na zona e entra no Socket.IO room
    const { zone, state } = zones.joinZone(socket, zoneId, { name, playerClass });

    // Aplica estado restaurado (preserva progressão, posição, recursos)
    if (restoredState) {
      Object.assign(state, {
        x:         restoredState.x,
        y:         restoredState.y,
        hp:        Math.max(1, restoredState.hp), // não ressuscita mortos na reconexão
        mana:      restoredState.mana,
        stamina:   restoredState.stamina,
        xp:        restoredState.xp,
        gold:      restoredState.gold,
        level:     restoredState.level,
        inventory: restoredState.inventory || [],
      });
    }

    socket.emit('player:joined', {
      id:           socket.id,
      sessionToken: newToken,
      world:        { w: MAP_W, h: MAP_H },
      // Array com apenas as skills da classe do jogador (5 skills) para o SkillBar do cliente
      abilities:    zone.combat.getClassSkills(state.class),
      state,
    });

    console.log(`  join: ${name} (${playerClass}) → zona "${zoneId}"`);
  });

  // ── player:move ─────────────────────────────────────────────────────────────
  socket.on('player:move', ({ x, y } = {}) => {
    const zone = zones.getZone(socket.id);
    if (zone) zone.players.handleMove(socket.id, { x, y });
  });

  // ── skill:use ────────────────────────────────────────────────────────────────
  socket.on('skill:use', ({ skillId, tx, ty } = {}) => {
    const zone = zones.getZone(socket.id);
    if (!zone) return;
    const result = zone.combat.startCast(socket.id, skillId, tx, ty);
    socket.emit('skill:result', { skillId, ...result });
  });

  // ── item:pickup ──────────────────────────────────────────────────────────────
  socket.on('item:pickup', ({ itemId } = {}) => {
    const zone = zones.getZone(socket.id);
    if (!zone) return;

    const item = zone.world.items.get(itemId);
    const p    = zone.world.getPlayer(socket.id);
    if (!item || !p || p.dead) return;
    if (Math.hypot(item.x - p.x, item.y - p.y) > 60) return;
    if (p.inventory.length >= 30) {
      socket.emit('error', { msg: 'Inventário cheio' });
      return;
    }

    p.inventory.push({ id: item.id, type: item.type });
    zone.world.removeItem(itemId);
    socket.emit('item:picked', { item });
  });

  // ── chat:send ────────────────────────────────────────────────────────────────
  socket.on('chat:send', ({ channel, message } = {}) => {
    const zone = zones.getZone(socket.id);
    if (!zone) return;

    const p = zone.world.getPlayer(socket.id);
    if (!p || !message || message.length > 200) return;

    const payload = { from: p.name, message: message.slice(0, 200), ts: Date.now() };

    if (channel === 'global') {
      // Chat global — todos os players em todos os zonas recebem
      io.emit('chat:message', { channel: 'global', ...payload });
    } else {
      // Chat local — apenas players na mesma zona
      io.to(zones.getZoneId(socket.id)).emit('chat:message', { channel: 'zone', ...payload });
    }
  });

  // ── zone:change ──────────────────────────────────────────────────────────────
  // Permite ao cliente pedir troca de zona (ex: entrar em dungeon, sair do overworld).
  // Estado não é restaurado automaticamente — cria um player novo na nova zona.
  socket.on('zone:change', ({ zoneId } = {}) => {
    if (!zoneId || typeof zoneId !== 'string') return;
    const currentZone = zones.getZone(socket.id);
    if (!currentZone) return;

    const player = currentZone.world.getPlayer(socket.id);
    if (!player) return;

    // Salva estado antes de trocar
    const token = socketTokens.get(socket.id);
    if (token) sessions.save(token, player, zones.getZoneId(socket.id));

    // Entra na nova zona com os dados atuais
    const newToken = uuidv4();
    socketTokens.set(socket.id, newToken);

    const { zone: newZone, state } = zones.joinZone(socket, zoneId, {
      name:        player.name,
      playerClass: player.class,
    });

    // Aplica stats do player anterior
    Object.assign(state, {
      hp: player.hp, maxHp: player.maxHp,
      mana: player.mana, maxMana: player.maxMana,
      stamina: player.stamina,
      xp: player.xp, gold: player.gold, level: player.level,
      inventory: player.inventory || [],
    });

    socket.emit('player:joined', {
      id:           socket.id,
      sessionToken: newToken,
      world:        { w: MAP_W, h: MAP_H },
      abilities:    newZone.combat.getClassSkills(state.class),
      state,
    });

    console.log(`  zone:change ${player.name} → "${zoneId}"`);
  });

  // ── ping_rtt ─────────────────────────────────────────────────────────────────
  // Medição de RTT — compatível com NetworkManager.cs
  socket.on('ping_rtt', (ts) => socket.emit('pong_rtt', ts));

  // ── disconnect ───────────────────────────────────────────────────────────────
  socket.on('disconnect', () => {
    const zone  = zones.getZone(socket.id);
    const token = socketTokens.get(socket.id);

    // Salva estado para possível reconexão
    if (zone && token) {
      const player = zone.world.getPlayer(socket.id);
      if (player) {
        sessions.save(token, player, zones.getZoneId(socket.id));
      }
    }

    zones.leaveZone(socket);
    socketTokens.delete(socket.id);
    console.log(`[-] ${socket.id}`);
  });
});

// ----- Diagnóstico (opcional: remove em produção) -----
setInterval(() => {
  const stats = zones.getStats();
  const total = Object.values(stats).reduce((s, z) => s + z.players, 0);
  if (total > 0) console.log('[Stats]', JSON.stringify(stats));
}, 30_000);

// ----- Start -----
http.listen(PORT, () => {
  console.log(`\nMMO v2 rodando em http://localhost:${PORT}`);
  console.log(`Zona "overworld" ativa.`);
});
