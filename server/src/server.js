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

const { PORT, MAP_W, MAP_H, BLACKSMITH_X, BLACKSMITH_Y, BLACKSMITH_RANGE, TRAINER_X, TRAINER_Y, TRAINER_RANGE } = require('./config/constants');
const { ZoneManager }        = require('./managers/ZoneManager');
const SessionManager         = require('./managers/SessionManager');

// NPCs estáticos do overworld (posição fixa, sem IA).
// Enviados ao cliente via player:joined para renderização e detecção de proximidade.
const STATIC_NPCS = [
  { id: 'blacksmith_1', type: 'blacksmith', name: 'Ferreiro Aldric', x: BLACKSMITH_X, y: BLACKSMITH_Y },
  // Instrutor: NPC que converte Fama Amarela pendente em bônus permanentes (cobra ouro).
  { id: 'trainer_1',    type: 'trainer',    name: 'Instrutor Magnus', x: TRAINER_X,    y: TRAINER_Y    },
];

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
  socket.on('player:join', ({ name, sessionToken, zoneId: reqZoneId } = {}) => {
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

    // Cria player na zona (sem classe — gear-based)
    const { zone, state } = zones.joinZone(socket, zoneId, { name });

    // Aplica estado restaurado (preserva progressão, posição, recursos, gear)
    if (restoredState) {
      Object.assign(state, {
        x:              restoredState.x,
        y:              restoredState.y,
        hp:             Math.max(1, restoredState.hp),
        mana:           restoredState.mana,
        stamina:        restoredState.stamina,
        xp:             restoredState.xp,
        gold:           restoredState.gold,
        level:          restoredState.level,
        inventory:      restoredState.inventory      || [],
        equipment:      restoredState.equipment      || state.equipment,
        selectedSkills: restoredState.selectedSkills || state.selectedSkills,
        gatheringSkills: restoredState.gatheringSkills || state.gatheringSkills,
        craftingSkills:  restoredState.craftingSkills  || state.craftingSkills,
        craftingFocus:   restoredState.craftingFocus   ?? state.craftingFocus,
      });
    }

    socket.emit('player:joined', {
      id:           socket.id,
      sessionToken: newToken,
      world:        { w: MAP_W, h: MAP_H },
      // abilities: array de skills ativas (derivadas do gear)
      abilities:    zone.combat.getPlayerAbilities(state),
      // npcs: lista de NPCs estáticos da zona (posição + tipo para o cliente renderizar)
      npcs:         STATIC_NPCS,
      // gearOptions: opções de skill por peça equipada (para o painel de habilidades)
      gearOptions:  zone.players.getGearOptions(state),
      state,
    });

    console.log(`  join: ${name} → zona "${zoneId}"`);
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

    const { zone: newZone, state } = zones.joinZone(socket, zoneId, { name: player.name });

    // Aplica estado do player anterior (preserva gear e skills)
    Object.assign(state, {
      hp: player.hp, maxHp: player.maxHp,
      mana: player.mana, maxMana: player.maxMana,
      stamina: player.stamina,
      xp: player.xp, gold: player.gold, level: player.level,
      inventory:      player.inventory      || [],
      equipment:      player.equipment      || state.equipment,
      selectedSkills: player.selectedSkills || state.selectedSkills,
      gatheringSkills: player.gatheringSkills || state.gatheringSkills,
      craftingSkills:  player.craftingSkills  || state.craftingSkills,
    });

    socket.emit('player:joined', {
      id:           socket.id,
      sessionToken: newToken,
      world:        { w: MAP_W, h: MAP_H },
      abilities:    newZone.combat.getPlayerAbilities(state),
      npcs:         STATIC_NPCS,
      gearOptions:  newZone.players.getGearOptions(state),
      state,
    });

    console.log(`  zone:change ${player.name} → "${zoneId}"`);
  });

  // ── gear:equip ───────────────────────────────────────────────────────────────
  // Payload: { slot: 'weapon'|'chest'|'head'|'boots', gearId: string }
  // Equipa uma peça de gear. Skills do slot são resetadas para o padrão do gear.
  socket.on('gear:equip', ({ slot, gearId } = {}) => {
    const zone = zones.getZone(socket.id);
    if (!zone) return;
    const p = zone.world.getPlayer(socket.id);
    if (!p || p.dead) return;
    const result = zone.players.equipItem(p, slot, gearId);
    socket.emit('gear:equipped', { slot, gearId, abilities: zone.combat.getPlayerAbilities(p), ...result });
  });

  // ── gear:unequip ─────────────────────────────────────────────────────────────
  // Payload: { slot: 'weapon'|'chest'|'head'|'boots' }
  // Remove a peça de gear do slot e reseta a(s) skill(s) associada(s).
  socket.on('gear:unequip', ({ slot } = {}) => {
    const zone = zones.getZone(socket.id);
    if (!zone) return;
    const p = zone.world.getPlayer(socket.id);
    if (!p || p.dead) return;
    const result = zone.players.unequipItem(p, slot);
    socket.emit('gear:unequipped', { slot, abilities: zone.combat.getPlayerAbilities(p), ...result });
  });

  // ── item:drop ────────────────────────────────────────────────────────────────
  // Payload: { itemId } — remove o item do inventário (descartado no mundo / perdido).
  socket.on('item:drop', ({ itemId } = {}) => {
    const zone = zones.getZone(socket.id);
    if (!zone) return;
    const p = zone.world.getPlayer(socket.id);
    if (!p) return;
    const before = p.inventory.length;
    p.inventory = p.inventory.filter(i => i.id !== itemId);
    socket.emit('inventory:updated', { inventory: p.inventory, removed: before !== p.inventory.length });
  });

  // ── item:use ─────────────────────────────────────────────────────────────────
  // Payload: { itemId } — usa um consumível. Por ora apenas remove do inventário
  // (efeito de poção será implementado quando consumíveis forem definidos).
  socket.on('item:use', ({ itemId } = {}) => {
    const zone = zones.getZone(socket.id);
    if (!zone) return;
    const p = zone.world.getPlayer(socket.id);
    if (!p) return;
    p.inventory = p.inventory.filter(i => i.id !== itemId);
    socket.emit('inventory:updated', { inventory: p.inventory });
  });

  // ── skill:select ─────────────────────────────────────────────────────────────
  // Payload: { slotKey: 'weapon_Q'|..., skillId: string }
  // Altera qual skill está ativa em um slot de gear (fora de combate).
  socket.on('skill:select', ({ slotKey, skillId } = {}) => {
    const zone = zones.getZone(socket.id);
    if (!zone) return;
    const p = zone.world.getPlayer(socket.id);
    if (!p || p.dead) return;
    if (p.casting) { socket.emit('skill:select_result', { error: 'in_combat' }); return; }
    const result = zone.players.selectSkill(p, slotKey, skillId);
    socket.emit('skill:select_result', { slotKey, skillId, abilities: zone.combat.getPlayerAbilities(p), ...result });
  });

  // ── mastery:convert_yellow_fame ──────────────────────────────────────────────
  // Converte Fama Amarela pendente em nível permanente, cobrando ouro.
  // O player deve estar próximo do Instrutor NPC.
  // Payload: { gearId: string }  — ID do equipamento (ex: 'sword', 'cloth_chest')
  socket.on('mastery:convert_yellow_fame', ({ gearId } = {}) => {
    const zone = zones.getZone(socket.id);
    if (!zone) return;
    const p = zone.world.getPlayer(socket.id);
    if (!p || p.dead) return;

    // Valida proximidade ao Instrutor
    const dist = Math.hypot(p.x - TRAINER_X, p.y - TRAINER_Y);
    if (dist > TRAINER_RANGE) {
      socket.emit('mastery:convert_result', { error: 'too_far', dist: Math.round(dist) });
      return;
    }

    const result = zone.players.convertYellowFame(p, gearId);
    socket.emit('mastery:convert_result', result);
  });

  // ── repair:item ──────────────────────────────────────────────────────────────
  // Repara um ou todos os slots no NPC Ferreiro. Debita ouro e restaura durabilidade.
  // Payload: { slot: 'weapon'|'chest'|'head'|'boots'|'all' }
  // Requer: player dentro de BLACKSMITH_RANGE do BLACKSMITH_X/Y.
  socket.on('repair:item', ({ slot } = {}) => {
    const zone = zones.getZone(socket.id);
    if (!zone) return;
    const p = zone.world.getPlayer(socket.id);
    if (!p || p.dead) return;

    // Valida proximidade ao Ferreiro
    const dist = Math.hypot(p.x - BLACKSMITH_X, p.y - BLACKSMITH_Y);
    if (dist > BLACKSMITH_RANGE) {
      socket.emit('repair:result', { error: 'too_far', dist: Math.round(dist) });
      return;
    }

    const result = zone.players.repairItem(p, slot || 'all');
    socket.emit('repair:result', result);
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
