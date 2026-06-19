// PlayerManager -- criação, movimento, spawn/respawn de players
// v4: sistema gear-based (sem classes fixas). Skills derivadas do equipamento.
const { v4: uuidv4 } = require('uuid');
const {
  MAX_HP, MAX_MANA, MAX_STAMINA, MAX_SPEED, MANA_REGEN_PER_SEC,
  STAMINA_REGEN_PER_SEC, RESPAWN_MS, MAP_W, MAP_H, MAX_LEVEL,
  MAX_DURABILITY, CRAFT_FOCUS_MAX,
} = require('../config/constants');
const GEAR = require('../config/gear.json');

// XP necessário para passar do level atual para o próximo.
function xpNeededForLevel(level) {
  return Math.floor(100 * Math.pow(level, 1.5));
}

const PLAYER_RADIUS = 22;

// Estatísticas base derivadas do gear equipado.
// Soma os stats de todas as peças de equipamento.
function computeGearStats(equipment) {
  const stats = { maxHp: 0, maxMana: 0, speed: 0, damageReduction: 0 };
  if (!equipment) return stats;

  for (const [slot, armorId] of Object.entries(equipment)) {
    if (slot === 'weapon' || !armorId) continue;
    const armorDef = GEAR.armors[armorId];
    if (!armorDef || !armorDef.stats) continue;
    for (const [k, v] of Object.entries(armorDef.stats)) {
      if (k in stats) stats[k] += v;
    }
  }
  return stats;
}

class PlayerManager {
  constructor(world) {
    this.world = world;
  }

  createPlayer(socketId, { name }) {
    // Equipamento inicial: espada enferrujada. Player pode trocar ao coletar itens.
    const starterEquipment = {
      weapon: 'sword',          // família de arma (referência ao gear.json)
      chest:  null,
      head:   null,
      boots:  null,
    };

    // Skill selecionada por slot (padrão = primeira opção de cada slot).
    const starterSkills = {
      weapon_Q: 'skill_slash',
      weapon_W: 'skill_heavy_blow',
      weapon_E: 'skill_execute',
      chest_R:  null,
      head_D:   null,
    };

    const gearStats = computeGearStats(starterEquipment);
    const maxHp    = MAX_HP   + (gearStats.maxHp   || 0);
    const maxMana  = MAX_MANA + (gearStats.maxMana  || 0);
    const speed    = MAX_SPEED + (gearStats.speed   || 0);

    const state = {
      id:       socketId,
      name:     name || 'Aventureiro',

      // Posição
      x: MAP_W / 2 + (Math.random() - 0.5) * 400,
      y: MAP_H / 2 + (Math.random() - 0.5) * 400,

      // Recursos
      hp: maxHp, maxHp,
      mana: maxMana, maxMana,
      stamina: MAX_STAMINA, maxStamina: MAX_STAMINA,
      speed, maxSpeed: speed,

      // Estado de combate
      dead:       false,
      casting:    null,
      cooldowns:  {},
      shield:     0,
      dodgeChance: 0,
      damageBonus: 1,
      damageReduction: gearStats.damageReduction || 0,
      damageMult: 1,

      // Status effects ativos: { [effectType]: { endsAt, ...params } }
      statusEffects: {},
      // Diminishing Returns: { [ccType]: { count, lastAppliedAt } }
      ccHistory: {},

      // Progressão
      level:  1,
      xp:     0,
      xpMax:  xpNeededForLevel(1),
      gold:   0,

      // Gear-based
      equipment:     starterEquipment,   // { weapon, chest, head, boots }
      selectedSkills: starterSkills,      // { weapon_Q, weapon_W, weapon_E, chest_R, head_D }

      // Inventário
      inventory: [],

      // Crafting & Gathering
      craftingFocus: CRAFT_FOCUS_MAX,
      gatheringSkills: {
        mining:      { level: 1, xp: 0, xpMax: xpNeededForLevel(1) },
        woodcutting: { level: 1, xp: 0, xpMax: xpNeededForLevel(1) },
        herbalism:   { level: 1, xp: 0, xpMax: xpNeededForLevel(1) },
        hunting:     { level: 1, xp: 0, xpMax: xpNeededForLevel(1) },
        fishing:     { level: 1, xp: 0, xpMax: xpNeededForLevel(1) },
      },
      craftingSkills: {
        smithing:     { level: 1, xp: 0, xpMax: xpNeededForLevel(1) },
        leatherwork:  { level: 1, xp: 0, xpMax: xpNeededForLevel(1) },
        alchemy:      { level: 1, xp: 0, xpMax: xpNeededForLevel(1) },
        fletching:    { level: 1, xp: 0, xpMax: xpNeededForLevel(1) },
        runecrafting: { level: 1, xp: 0, xpMax: xpNeededForLevel(1) },
      },

      // Extras
      guildId:       null,
      lastSeenAt:    Date.now(),
      rejectedMoves: 0,
    };

    this.world.addPlayer(state);
    return state;
  }

  // Retorna o array de skills ativas (até 5) baseado no equipment atual.
  // Usado em player:joined para informar o cliente.
  getActiveSkillIds(player) {
    const s = player.selectedSkills || {};
    return [
      s.weapon_Q || null,
      s.weapon_W || null,
      s.weapon_E || null,
      s.chest_R  || null,
      s.head_D   || null,
    ];
  }

  // Valida se o player pode usar uma skill (tem ela no gear selecionado).
  playerHasSkill(player, skillId) {
    if (!skillId) return false;
    const s = player.selectedSkills || {};
    return Object.values(s).includes(skillId);
  }

  handleMove(playerId, { x, y }, now = Date.now()) {
    const p = this.world.getPlayer(playerId);
    if (!p || p.dead) return;

    // Verifica status effects que impedem movimento
    const se = p.statusEffects || {};
    if ((se.stun     && se.stun.endsAt     > now) ||
        (se.root     && se.root.endsAt     > now) ||
        (se.knockback && se.knockback.endsAt > now)) {
      return;  // movimento bloqueado por CC
    }

    const dt = Math.max((now - p.lastSeenAt) / 1000, 0.001);
    p.lastSeenAt = now;

    const tx = Math.max(0, Math.min(MAP_W, Number(x)));
    const ty = Math.max(0, Math.min(MAP_H, Number(y)));
    if (Number.isNaN(tx) || Number.isNaN(ty)) return;

    const dx = tx - p.x, dy = ty - p.y;
    const d = Math.hypot(dx, dy);

    // Haste modifica velocidade efetiva
    let effectiveSpeed = p.speed;
    if (se.haste && se.haste.endsAt > now) effectiveSpeed = Math.round(p.speed * se.haste.factor);
    if (se.slow  && se.slow.endsAt  > now) effectiveSpeed = Math.round(p.speed * se.slow.factor);

    const maxDist = effectiveSpeed * dt * 1.5 + 5;

    if (d > maxDist) {
      p.rejectedMoves++;
      const r = maxDist / d;
      p.x += dx * r; p.y += dy * r;
    } else {
      p.x = tx; p.y = ty;
    }
    this._resolveCollisions(p);
  }

  // Regenação chamada pelo ZoneManager a cada tick (itera todos os players da zona).
  regenTick(dtSec) {
    const now = Date.now();
    for (const player of this.world.players.values()) {
      this._regenPlayer(player, dtSec, now);
    }
  }

  _regenPlayer(player, dtSec, now) {
    if (player.dead) return;

    if (player.mana < player.maxMana)
      player.mana = Math.min(player.maxMana, player.mana + MANA_REGEN_PER_SEC * dtSec);

    if (player.stamina < player.maxStamina)
      player.stamina = Math.min(player.maxStamina, player.stamina + STAMINA_REGEN_PER_SEC * dtSec);

    // Expiração de status effects
    const se = player.statusEffects || {};
    for (const type of Object.keys(se)) {
      const effect = se[type];
      if (effect.endsAt && effect.endsAt <= now) delete se[type];
    }
  }

  removePlayer(socketId) {
    this.world.removePlayer(socketId);
  }

  scheduleRespawn(player) {
    setTimeout(() => {
      if (!player.dead) return; // já ressuscitado por outro jogador
      player.dead     = false;
      player.hp       = Math.ceil(player.maxHp * 0.3); // ressuscita com 30% HP
      player.mana     = 0;
      player.shield   = 0;
      player.casting  = null;
      player.statusEffects = {};

      // Move para spawn
      player.x = MAP_W / 2 + (Math.random() - 0.5) * 200;
      player.y = MAP_H / 2 + (Math.random() - 0.5) * 200;

      this.world.io.to(player.id).emit('player:revived', {
        hp: player.hp,
        x:  player.x,
        y:  player.y,
      });
    }, RESPAWN_MS);
  }

  checkLevelUp(player) {
    while (player.xp >= player.xpMax && player.level < MAX_LEVEL) {
      player.xp   -= player.xpMax;
      player.level += 1;
      player.xpMax  = xpNeededForLevel(player.level);

      // Bonus de atributos por level
      player.maxHp   = Math.round(MAX_HP   * (1 + player.level * 0.05));
      player.maxMana = Math.round(MAX_MANA  * (1 + player.level * 0.03));
      player.hp      = player.maxHp;
      player.mana    = player.maxMana;

      this.world.io.to(player.id).emit('player:levelup', {
        level:  player.level,
        maxHp:  player.maxHp,
        maxMana: player.maxMana,
        speed:  player.speed,
        xp:     player.xp,
        xpMax:  player.xpMax,
      });
    }
  }

  // Equipa uma peça de gear e recalcula stats + skills disponíveis.
  equipItem(player, slot, gearId) {
    const validSlots = ['weapon', 'chest', 'head', 'boots'];
    if (!validSlots.includes(slot)) return { error: 'invalid_slot' };

    if (slot === 'weapon') {
      // Valida que existe no gear.json
      if (!GEAR.weapons[gearId]) return { error: 'unknown_weapon' };
      player.equipment.weapon = gearId;

      // Reset skill selection para defaults da nova arma
      const wDef = GEAR.weapons[gearId];
      player.selectedSkills.weapon_Q = wDef.slots.Q.options[0];
      player.selectedSkills.weapon_W = wDef.slots.W.options[0];
      player.selectedSkills.weapon_E = wDef.slots.E.options[0];
    } else {
      const armorSlotMap = { chest: 'chest_R', head: 'head_D' };
      if (!GEAR.armors[gearId]) return { error: 'unknown_armor' };
      const aDef = GEAR.armors[gearId];
      if (aDef.slot !== slot) return { error: 'wrong_slot' };

      player.equipment[slot] = gearId;
      // Reset skill selection para default da nova armadura
      const skillSlot = armorSlotMap[slot];
      if (skillSlot) player.selectedSkills[skillSlot] = aDef.skill.options[0];
    }

    // Recalcula stats de gear
    const gs = computeGearStats(player.equipment);
    player.maxHp   = MAX_HP   + (gs.maxHp   || 0) + Math.round(player.level * 5);
    player.maxMana = MAX_MANA + (gs.maxMana  || 0);
    player.damageReduction = gs.damageReduction || 0;

    return { ok: true };
  }

  // Altera a skill selecionada em um slot.
  selectSkill(player, slotKey, skillId) {
    const validSlots = ['weapon_Q', 'weapon_W', 'weapon_E', 'chest_R', 'head_D'];
    if (!validSlots.includes(slotKey)) return { error: 'invalid_slot' };

    // Verifica que a skill está disponível no gear equipado
    const weaponId = player.equipment?.weapon;
    const wDef     = weaponId ? GEAR.weapons[weaponId] : null;
    const slotName = slotKey.split('_')[1];  // Q, W, E, R, D

    if (['Q', 'W', 'E'].includes(slotName)) {
      if (!wDef || !wDef.slots[slotName]?.options.includes(skillId))
        return { error: 'skill_not_available' };
    } else {
      // Armadura
      const armorMap = { R: 'chest', D: 'head' };
      const armorId  = player.equipment?.[armorMap[slotName]];
      const aDef     = armorId ? GEAR.armors[armorId] : null;
      if (!aDef || !aDef.skill?.options.includes(skillId))
        return { error: 'skill_not_available' };
    }

    player.selectedSkills[slotKey] = skillId;
    return { ok: true };
  }

  _resolveCollisions(p) {
    for (const other of this.world.players.values()) {
      if (other.id === p.id || other.dead) continue;
      const dx = p.x - other.x, dy = p.y - other.y;
      const d  = Math.hypot(dx, dy);
      const overlap = PLAYER_RADIUS * 2 - d;
      if (overlap > 0 && d > 0) {
        const push = overlap / 2;
        p.x += (dx / d) * push;
        p.y += (dy / d) * push;
      }
    }
    p.x = Math.max(PLAYER_RADIUS, Math.min(MAP_W - PLAYER_RADIUS, p.x));
    p.y = Math.max(PLAYER_RADIUS, Math.min(MAP_H - PLAYER_RADIUS, p.y));
  }
}

module.exports = PlayerManager;
