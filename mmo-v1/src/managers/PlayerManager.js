// PlayerManager — criação, movimento, spawn/respawn de players
const { v4: uuidv4 } = require('uuid');
const {
  MAX_HP, MAX_MANA, MAX_STAMINA, MAX_SPEED, MANA_REGEN_PER_SEC,
  STAMINA_REGEN_PER_SEC, RESPAWN_MS, MAP_W, MAP_H, CLASS_MODIFIERS,
} = require('../config/constants');

class PlayerManager {
  constructor(world) {
    this.world = world;
  }

  createPlayer(socketId, { name, playerClass = 'warrior' }) {
    const mod = CLASS_MODIFIERS[playerClass] || CLASS_MODIFIERS.warrior;
    const maxHp   = Math.round(MAX_HP   * mod.hp);
    const maxMana = Math.round(MAX_MANA * mod.mana);
    const speed   = Math.round(MAX_SPEED * mod.speed);

    const state = {
      id: socketId,
      name: name || 'Player',
      class: playerClass,
      x: Math.random() * (MAP_W - 200) + 100,
      y: Math.random() * (MAP_H - 200) + 100,
      hp: maxHp, maxHp,
      mana: maxMana, maxMana,
      stamina: MAX_STAMINA, maxStamina: MAX_STAMINA,
      speed,
      dead: false,
      casting: null,
      cooldowns: {},
      lastSeenAt: Date.now(),
      rejectedMoves: 0,
      level: 1,
      xp: 0,
      gold: 0,
      inventory: [],
      guildId: null,
    };

    this.world.addPlayer(state);
    return state;
  }

  handleMove(playerId, { x, y }, now = Date.now()) {
    const p = this.world.getPlayer(playerId);
    if (!p || p.dead) return;

    const dt = Math.max((now - p.lastSeenAt) / 1000, 0.001);
    p.lastSeenAt = now;

    const tx = Math.max(0, Math.min(MAP_W, Number(x)));
    const ty = Math.max(0, Math.min(MAP_H, Number(y)));
    if (Number.isNaN(tx) || Number.isNaN(ty)) return;

    const dx = tx - p.x, dy = ty - p.y;
    const d = Math.hypot(dx, dy);
    const maxDist = p.speed * dt * 1.5 + 5;

    if (d > maxDist) {
      p.rejectedMoves++;
      const r = maxDist / d;
      p.x += dx * r; p.y += dy * r;
    } else {
      p.x = tx; p.y = ty;
    }
  }

  removePlayer(socketId) {
    this.world.removePlayer(socketId);
  }

  scheduleRespawn(player) {
    setTimeout(() => {
      if (!this.world.getPlayer(player.id)) return;
      const mod = CLASS_MODIFIERS[player.class] || CLASS_MODIFIERS.warrior;
      Object.assign(player, {
        hp: Math.round(MAX_HP * mod.hp),
        mana: Math.round(MAX_MANA * mod.mana),
        dead: false,
        casting: null,
        x: Math.random() * (MAP_W - 200) + 100,
        y: Math.random() * (MAP_H - 200) + 100,
      });
    }, RESPAP�_MS);
  }

  // Chamado a cada tick pelo WorldManager
  regenTick(dtSec) {
    for (const p of this.world.players.values()) {
      if (p.dead) continue;
      if (p.mana < p.maxMana)
        p.mana = Math.min(p.maxMana, p.mana + MANA_REGEN_PER_SEC * dtSec);
      if (p.stamina < p.maxStamina)
        p.stamina = Math.min(p.maxStamina, p.stamina + STAMINA_REGEN_PER_SEC * dtSec);
    }
  }
}

module.exports = PlayerManager;
