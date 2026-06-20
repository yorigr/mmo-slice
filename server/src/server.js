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

const { PORT, MAP_W, MAP_H, BLACKSMITH_X, BLACKSMITH_Y, BLACKSMITH_RANGE, TRAINER_X, TRAINER_Y, TRAINER_RANGE, MAX_INVENTORY_SLOTS, LOOT_DESPAWN_MS } = require('./config/constants');
const ITEMS = require('./config/items.json');
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
  // Remove a peça do slot, reseta skills e devolve o gearId ao inventário.
  socket.on('gear:unequip', ({ slot } = {}) => {
    const zone = zones.getZone(socket.id);
    if (!zone) return;
    const p = zone.world.getPlayer(socket.id);
    if (!p || p.dead) return;

    const validSlots = ['weapon', 'chest', 'head', 'boots'];
    if (!validSlots.includes(slot)) {
      socket.emit('gear:unequipped', { error: 'invalid_slot' }); return;
    }
    const gearId = p.equipment[slot];
    if (!gearId) {
      socket.emit('gear:unequipped', { error: 'slot_empty' }); return;
    }
    if (p.inventory.length >= MAX_INVENTORY_SLOTS) {
      socket.emit('gear:unequipped', { error: 'inventory_full' }); return;
    }

    // Remove do slot e adiciona ao inventário antes de recalcular stats
    const result = zone.players.unequipItem(p, slot);
    if (result.ok) {
      p.inventory.push({ id: uuidv4(), type: gearId });
    }
    socket.emit('gear:unequipped', {
      slot, gearId,
      abilities: zone.combat.getPlayerAbilities(p),
      inventory: p.inventory,
      ...result,
    });
  });

  // ── item:drop ────────────────────────────────────────────────────────────────
  // Payload: { itemId } — remove do inventário e spawna o item no chão da zona.
  // Outros jogadores verão o item via world:update (items são incluídos no snapshot).
  socket.on('item:drop', ({ itemId } = {}) => {
    const zone = zones.getZone(socket.id);
    if (!zone) return;
    const p = zone.world.getPlayer(socket.id);
    if (!p) return;

    const dropped = p.inventory.find(i => i.id === itemId);
    if (!dropped) { socket.emit('inventory:updated', { inventory: p.inventory, error: 'not_found' }); return; }

    // Remove do inventário
    p.inventory = p.inventory.filter(i => i.id !== itemId);

    // Spawna no chão próximo ao player (jitter ±20px para não sobrepor)
    const worldItem = {
      id:   uuidv4(),
      type: dropped.type,
      x:    p.x + (Math.random() - 0.5) * 40,
      y:    p.y + (Math.random() - 0.5) * 40,
    };
    zone.world.addItem(worldItem);
    // Despawna automaticamente após LOOT_DESPAWN_MS (igual ao loot de monstro)
    setTimeout(() => zone.world.removeItem(worldItem.id), LOOT_DESPAWN_MS);

    socket.emit('inventory:updated', { inventory: p.inventory });
  });

  // ── item:use ─────────────────────────────────────────────────────────────────
  // Payload: { itemId } — consome o item e aplica seu efeito (hp/mana de items.json).
  // Itens não-consumíveis retornam erro. Stackable com qty>1 decrementa em vez de remover.
  socket.on('item:use', ({ itemId } = {}) => {
    const zone = zones.getZone(socket.id);
    if (!zone) return;
    const p = zone.world.getPlayer(socket.id);
    if (!p || p.dead) return;

    const slot = p.inventory.find(i => i.id === itemId);
    if (!slot) { socket.emit('item:use_result', { error: 'not_found' }); return; }

    const def = ITEMS[slot.type];
    if (!def || def.type !== 'consumable' || !def.effect) {
      socket.emit('item:use_result', { error: 'not_consumable' }); return;
    }

    // Aplica efeito (HP e/ou mana, clampado ao máximo)
    const effect = {};
    if (def.effect.hp) {
      const before = p.hp;
      p.hp = Math.min(p.maxHp, p.hp + def.effect.hp);
      effect.hp = p.hp - before;
    }
    if (def.effect.mana) {
      const before = p.mana;
      p.mana = Math.min(p.maxMana, p.mana + def.effect.mana);
      effect.mana = p.mana - before;
    }

    // Remove do inventário (stackable com qty: decrementa; senão: remove)
    if (slot.qty && slot.qty > 1) {
      slot.qty -= 1;
    } else {
      p.inventory = p.inventory.filter(i => i.id !== itemId);
    }

    socket.emit('item:use_result', { ok: true, itemId, effect, hp: p.hp, mana: p.mana, inventory: p.inventory });
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
  // ping_rtt
  socket.on('ping_rtt', (ts) => socket.emit('pong_rtt', ts));
});

server.listen(PORT, () => {
  console.log(`[Servidor] Ouvindo na porta ${PORT}`);
});
