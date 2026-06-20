// PlayerManager -- criação, movimento, spawn/respawn de players
// v5: sistema gear-based + Equipment Mastery (maestria individual por peça, inspirada no Albion).
//
// Maestria:
//   • Armas  → XP por skill ofensiva/suporte resolvida (MASTERY_XP_PER_USE)
//   • Armaduras → XP por absorver dano com a peça equipada (MASTERY_XP_PER_HIT)
//   • Ao atingir maestria máxima: XP excedente vira "Fama Amarela pendente"
//   • Fama Amarela → convertida em bônus permanentes pagando ouro ao Instrutor NPC
const { v4: uuidv4 } = require('uuid');
const {
  MAX_HP, MAX_MANA, MAX_STAMINA, MAX_SPEED, MANA_REGEN_PER_SEC,
  STAMINA_REGEN_PER_SEC, RESPAWN_MS, MAP_W, MAP_H, MAX_LEVEL,
  MAX_DURABILITY, DURABILITY_DEATH_PENALTY, CRAFT_FOCUS_MAX,
  DEATH_DESTROY_RATES, DEATH_RESOURCE_LOSS_RATE,
  REPAIR_BASE_VALUES, REPAIR_COST_RATE,
  MASTERY_MAX_LEVEL, MASTERY_XP_TABLE,
  MASTERY_CLOTH_MANA_PER_LEVEL, MASTERY_LEATHER_DODGE_PER_LEVEL, MASTERY_PLATE_DR_PER_LEVEL,
  YELLOW_FAME_MAX_LEVEL, YELLOW_FAME_XP_TABLE, YELLOW_FAME_GOLD_TABLE,
  YELLOW_FAME_CLOTH_MANA_PER_LEVEL, YELLOW_FAME_LEATHER_DODGE_PER_LEVEL, YELLOW_FAME_PLATE_DR_PER_LEVEL,
} = require('../config/constants');
const GEAR = require('../config/gear.json');

// XP necessário para passar do level atual para o próximo.
function xpNeededForLevel(level) {
  return Math.floor(100 * Math.pow(level, 1.5));
}

const PLAYER_RADIUS = 22;

// Entrada de maestria zerada (level 1, sem XP, sem Fama Amarela).
function _emptyMastery() {
  return { level: 1, xp: 0, xpMax: MASTERY_XP_TABLE[0], yellowFame: { pending: 0, level: 0 } };
}

// Todos os IDs de gear conhecidos (para pré-criar entradas de maestria).
const ALL_WEAPON_IDS = Object.keys(GEAR.weapons);
const ALL_ARMOR_IDS  = Object.keys(GEAR.armors);
const ALL_GEAR_IDS   = [...ALL_WEAPON_IDS, ...ALL_ARMOR_IDS];

// Estatísticas base derivadas do gear equipado.
// Peças com durabilidade 0 estão quebradas e não fornecem stats.
function computeGearStats(equipment, durability = {}) {
  const stats = { maxHp: 0, maxMana: 0, speed: 0, damageReduction: 0 };
  if (!equipment) return stats;

  for (const [slot, armorId] of Object.entries(equipment)) {
    if (slot === 'weapon' || !armorId) continue;
    if ((durability[slot] ?? MAX_DURABILITY) === 0) continue;  // quebrado → sem stats
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
      boots_F:  null,
    };

    // Durabilidade inicial: todas as peças em 100%
    const starterDurability = { weapon: MAX_DURABILITY, chest: MAX_DURABILITY, head: MAX_DURABILITY, boots: MAX_DURABILITY };

    const gearStats = computeGearStats(starterEquipment, starterDurability);
    const maxHp    = MAX_HP   + (gearStats.maxHp   || 0);
    const maxMana  = MAX_MANA + (gearStats.maxMana  || 0);
    const speed    = MAX_SPEED + (gearStats.speed   || 0);

    // Maestria de equipamento: entrada para cada peça de gear conhecida.
    // Armas ganham XP por usar skills; armaduras ganham XP por absorver dano.
    const equipmentMastery = Object.fromEntries(
      ALL_GEAR_IDS.map(id => [id, _emptyMastery()])
    );

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
      masteryDodgeBonus: 0,   // bônus de dodge acumulado por maestria de leather
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
      equipment:      starterEquipment,   // { weapon, chest, head, boots }
      selectedSkills: starterSkills,      // { weapon_Q, weapon_W, weapon_E, chest_R, head_D, boots_F }
      durability:     starterDurability,  // { weapon, chest, head, boots } — 0–100 por slot

      // Maestria individual por peça de equipamento
      equipmentMastery,

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

  // ── Cálculo de Stats ─────────────────────────────────────────────────────────

  /**
   * Recalcula maxHp, maxMana, damageReduction e masteryDodgeBonus do player
   * levando em conta gear + durabilidade + maestria de armaduras equipadas.
   * Deve ser chamado após equipar/desposar gear, após reparo e após level up de maestria.
   */
  _recalcStats(player) {
    const gs = computeGearStats(player.equipment, player.durability);
    const ms = this._computeArmorMasteryBonuses(player);
    player.maxHp = MAX_HP + (gs.maxHp || 0) + Math.round(player.level * 5) + (ms.maxHp || 0);
    player.maxMana = MAX_MANA + (gs.maxMana || 0) + (ms.maxMana || 0);
    player.damageReduction = (gs.damageReduction || 0) + (ms.damageReduction || 0);
    player.masteryDodgeBonus = ms.dodgeChance || 0;
  }

  /**
   * Computa bônus de stats derivados da maestria de armaduras atualmente equipadas.
   * Retorna { maxHp, maxMana, damageReduction, dodgeChance }.
   */
  _computeArmorMasteryBonuses(player) {
    const bonuses = { maxHp: 0, maxMana: 0, damageReduction: 0, dodgeChance: 0 };
    const em = player.equipmentMastery;
    if (!em) return bonuses;

    for (const [slot, gearId] of Object.entries(player.equipment)) {
      if (slot === 'weapon' || !gearId) continue;
      const armorDef = GEAR.armors[gearId];
      if (!armorDef) continue;
      const mastery = em[gearId];
      if (!mastery) continue;

      // level - 1 porque nível 1 = sem bônus ainda (mesmo que no Albion)
      const mastLevels = mastery.level - 1;
      const yfLevels   = mastery.yellowFame?.level || 0;

      switch (armorDef.armorType) {
        case 'cloth':
          bonuses.maxMana += mastLevels * MASTERY_CLOTH_MANA_PER_LEVEL
                           + yfLevels  * YELLOW_FAME_CLOTH_MANA_PER_LEVEL;
          break;
        case 'leather':
          bonuses.dodgeChance += mastLevels * MASTERY_LEATHER_DODGE_PER_LEVEL
                               + yfLevels  * YELLOW_FAME_LEATHER_DODGE_PER_LEVEL;
          break;
        case 'plate':
          bonuses.damageReduction += mastLevels * MASTERY_PLATE_DR_PER_LEVEL
                                   + yfLevels  * YELLOW_FAME_PLATE_DR_PER_LEVEL;
          break;
      }
    }
    return bonuses;
  }

  // ── Maestria ─────────────────────────────────────────────────────────────────

  /**
   * Concede XP de maestria ao gearId especificado (arma ou armadura).
   *
   * • Se ainda não atingiu MASTERY_MAX_LEVEL: XP normal, com level up automático.
   * • Se já está no máximo: XP vira "Fama Amarela pendente".
   * • Se yellowFame.level já está no YELLOW_FAME_MAX_LEVEL: descarta (tudo maxado).
   *
   * Emite:
   *   mastery:xp         — progresso de XP normal
   *   mastery:levelup    — quando a maestria sobe de nível
   *   mastery:yellow_fame — quando XP vai para Fama Amarela pendente
   */
  gainMasteryXp(player, gearId, xp) {
    if (!player.equipmentMastery) return;
    const m = player.equipmentMastery[gearId];
    if (!m) return;

    // Completamente maxado: nada a ganhar
    if (m.level >= MASTERY_MAX_LEVEL && m.yellowFame.level >= YELLOW_FAME_MAX_LEVEL) return;

    const isArmor = !!GEAR.armors[gearId];

    if (m.level < MASTERY_MAX_LEVEL) {
      // ── Fase de maestria normal ──────────────────────────────────────────────
      m.xp += xp;
      let leveledUp = false;

      while (m.xp >= m.xpMax && m.level < MASTERY_MAX_LEVEL) {
        m.xp  -= m.xpMax;
        m.level += 1;
        leveledUp = true;

        // xpMax para o próximo nível (índice = novo nível - 1)
        m.xpMax = m.level < MASTERY_MAX_LEVEL
          ? MASTERY_XP_TABLE[m.level - 1]
          : 0;  // sentinela — maxado

        this.world.io.to(player.id).emit('mastery:levelup', {
          gearId,
          level:   m.level,
          xp:      m.xp,
          xpMax:   m.level < MASTERY_MAX_LEVEL ? m.xpMax : null,
          isMaxed: m.level >= MASTERY_MAX_LEVEL,
        });
      }

      // XP restante após atingir o máximo vai para Fama Amarela pendente
      if (m.level >= MASTERY_MAX_LEVEL && m.xp > 0) {
        if (m.yellowFame.level < YELLOW_FAME_MAX_LEVEL) {
          m.yellowFame.pending += m.xp;
        }
        m.xp = 0;
      }

      // Recalcula stats de armadura quando maestria sobe de nível
      if (leveledUp && isArmor) this._recalcStats(player);

      // Emite progresso de XP (se ainda não maxado)
      if (m.level < MASTERY_MAX_LEVEL) {
        this.world.io.to(player.id).emit('mastery:xp', {
          gearId, level: m.level, xp: m.xp, xpMax: m.xpMax,
        });
      }

    } else {
      // ── Fase de Fama Amarela: XP excedente acumula como pendente ────────────
      if (m.yellowFame.level < YELLOW_FAME_MAX_LEVEL) {
        m.yellowFame.pending += xp;
        this.world.io.to(player.id).emit('mastery:yellow_fame', {
          gearId,
          pending:  m.yellowFame.pending,
          level:    m.yellowFame.level,
          xpNeeded: YELLOW_FAME_XP_TABLE[m.yellowFame.level] ?? null,
        });
      }
    }
  }

  /**
   * Converte Fama Amarela pendente em nível permanente, cobrando ouro.
   * Deve ser chamado apenas quando o player está próximo do NPC Instrutor.
   *
   * @param {object} player
   * @param {string} gearId - ID do equipamento (ex: 'sword', 'cloth_chest')
   * @returns {{ ok?, error?, ... }}
   */
  convertYellowFame(player, gearId) {
    if (!gearId) return { error: 'missing_gear_id' };
    const m = player.equipmentMastery?.[gearId];
    if (!m) return { error: 'unknown_gear' };
    if (m.level < MASTERY_MAX_LEVEL) return { error: 'mastery_not_maxed', level: m.level };
    if (m.yellowFame.level >= YELLOW_FAME_MAX_LEVEL) return { error: 'yellow_fame_maxed' };

    const nextIdx  = m.yellowFame.level;          // 0-indexed para as tabelas
    const xpNeeded = YELLOW_FAME_XP_TABLE[nextIdx];
    const goldCost = YELLOW_FAME_GOLD_TABLE[nextIdx];

    if (m.yellowFame.pending < xpNeeded)
      return { error: 'not_enough_pending', pending: m.yellowFame.pending, xpNeeded };
    if (player.gold < goldCost)
      return { error: 'not_enough_gold', cost: goldCost, gold: player.gold };

    // Debita XP pendente e ouro
    m.yellowFame.pending -= xpNeeded;
    player.gold          -= goldCost;
    m.yellowFame.level   += 1;

    // Recalcula stats se for armadura (armas têm bônus dinâmico no CombatEngine)
    if (GEAR.armors[gearId]) this._recalcStats(player);

    return {
      ok:              true,
      gearId,
      yellowFameLevel: m.yellowFame.level,
      gold:            player.gold,
      goldSpent:       goldCost,
      pending:         m.yellowFame.pending,
      // próximo nível (null se maxado)
      nextXpNeeded:    m.yellowFame.level < YELLOW_FAME_MAX_LEVEL
                         ? YELLOW_FAME_XP_TABLE[m.yellowFame.level] : null,
      nextGoldCost:    m.yellowFame.level < YELLOW_FAME_MAX_LEVEL
                         ? YELLOW_FAME_GOLD_TABLE[m.yellowFame.level] : null,
    };
  }

  // ── Skills e Gear ────────────────────────────────────────────────────────────

  // Retorna o array de skills ativas (até 5) baseado no equipment atual.
  // Peças com durabilidade 0 estão quebradas: skill do slot retorna null.
  getActiveSkillIds(player) {
    const s   = player.selectedSkills || {};
    const dur = player.durability || {};
    const weaponOk = (dur.weapon ?? MAX_DURABILITY) > 0;
    const chestOk  = (dur.chest  ?? MAX_DURABILITY) > 0;
    const headOk   = (dur.head   ?? MAX_DURABILITY) > 0;
    const bootsOk  = (dur.boots  ?? MAX_DURABILITY) > 0;
    return [
      weaponOk ? (s.weapon_Q || null) : null,
      weaponOk ? (s.weapon_W || null) : null,
      weaponOk ? (s.weapon_E || null) : null,
      chestOk  ? (s.chest_R  || null) : null,
      headOk   ? (s.head_D   || null) : null,
      bootsOk  ? (s.boots_F  || null) : null,
    ];
  }

  // Valida se o player pode usar uma skill (tem ela equipada e a peça não está quebrada).
  playerHasSkill(player, skillId) {
    if (!skillId) return false;
    const s   = player.selectedSkills || {};
    const dur = player.durability || {};

    // Mapeia cada skill ao slot de equipment que a fornece
    const weaponSkills = [s.weapon_Q, s.weapon_W, s.weapon_E];
    if (weaponSkills.includes(skillId)) return (dur.weapon ?? MAX_DURABILITY) > 0;
    if (s.chest_R === skillId) return (dur.chest ?? MAX_DURABILITY) > 0;
    if (s.head_D  === skillId) return (dur.head  ?? MAX_DURABILITY) > 0;
    if (s.boots_F === skillId) return (dur.boots ?? MAX_DURABILITY) > 0;
    return false;
  }

  // ── Movimento ───────────────────────────────────────────────────────────────

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

  // ── Regen ───────────────────────────────────────────────────────────────────

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

  // ── Remoção ─────────────────────────────────────────────────────────────────

  removePlayer(socketId) {
    this.world.removePlayer(socketId);
  }

  // ── Morte ──────────

  /**
   * Processa destruicao e drop de itens ao morrer.
   * Chamado por CombatEngine antes de scheduleRespawn.
   *
   * @param {object} player    - State do player que morreu
   * @param {string} zoneType  - 'safe' | 'yellow' | 'red' | 'black'
   * @returns {{ destroyed: Array, dropped: Array, kept: Array }}
   */
  handlePlayerDeath(player, zoneType = 'yellow') {
    const rate = (DEATH_DESTROY_RATES[zoneType] ?? DEATH_DESTROY_RATES.yellow);
    const canLoot = (zoneType === 'red' || zoneType === 'black');

    const destroyed = [];
    const dropped   = [];
    const kept      = [];

    if (rate === 0) return { destroyed, dropped, kept };

    // Mapa de slot de equipment -> slots de skill relacionados
    const SLOT_TO_SKILLS = {
      weapon: ['weapon_Q', 'weapon_W', 'weapon_E'],
      chest:  ['chest_R'],
      head:   ['head_D'],
      boots:  ['boots_F'],
    };

    // Processa cada peca de equipment individualmente
    for (const [slot, gearId] of Object.entries(player.equipment)) {
      if (!gearId) continue;

      if (Math.random() < rate) {
        player.equipment[slot] = null;
        for (const sk of (SLOT_TO_SKILLS[slot] || [])) {
          player.selectedSkills[sk] = null;
        }
        destroyed.push({ slot, gearId });

      } else if (canLoot) {
        const itemId = require('uuid').v4();
        this.world.addItem({
          id:   itemId,
          type: gearId,
          x:    player.x + (Math.random() - 0.5) * 60,
          y:    player.y + (Math.random() - 0.5) * 60,
        });
        player.equipment[slot] = null;
        for (const sk of (SLOT_TO_SKILLS[slot] || [])) {
          player.selectedSkills[sk] = null;
        }
        dropped.push({ slot, gearId, itemId });

      } else {
        kept.push({ slot, gearId });
      }
    }

    // Destroi 10% de cada stack de material no inventario
    for (const item of player.inventory) {
      if (item.qty && item.qty > 0) {
        const loss = Math.ceil(item.qty * DEATH_RESOURCE_LOSS_RATE);
        item.qty = Math.max(0, item.qty - loss);
      }
    }
    player.inventory = player.inventory.filter(i => !i.qty || i.qty > 0);

    // Penalidade de durabilidade nas pecas mantidas
    if (!player.durability) player.durability = {};
    for (const { slot } of kept) {
      player.durability[slot] = Math.max(0,
        (player.durability[slot] ?? MAX_DURABILITY) - DURABILITY_DEATH_PENALTY
      );
    }
    for (const { slot } of [...destroyed, ...dropped]) {
      player.durability[slot] = 0;
    }

    return { destroyed, dropped, kept };
  }

  scheduleRespawn(player) {
    setTimeout(() => {
      if (!player.dead) return;
      player.dead     = false;
      player.hp       = Math.ceil(player.maxHp * 0.3);
      player.mana     = 0;
      player.shield   = 0;
      player.casting  = null;
      player.statusEffects = {};

      player.x = MAP_W / 2 + (Math.random() - 0.5) * 200;
      player.y = MAP_H / 2 + (Math.random() - 0.5) * 200;

      this.world.io.to(player.id).emit('player:revived', {
        hp: player.hp,
        x:  player.x,
        y:  player.y,
      });
    }, RESPAWN_MS);
  }

  // ── Level Up ─────────────────────────────────────────────────────────────────

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

  // ── Equip / Select ───────────────────────────────────────────────────────────

  // Equipa uma peca de gear e recalcula stats + skills disponiveis.
  equipItem(player, slot, gearId) {
    const validSlots = ['weapon', 'chest', 'head', 'boots'];
    if (!validSlots.includes(slot)) return { error: 'invalid_slot' };

    if (slot === 'weapon') {
      if (!GEAR.weapons[gearId]) return { error: 'unknown_weapon' };
      player.equipment.weapon = gearId;
      player.durability.weapon = MAX_DURABILITY;

      const wDef = GEAR.weapons[gearId];
      player.selectedSkills.weapon_Q = wDef.slots.Q.options[0];
      player.selectedSkills.weapon_W = wDef.slots.W.options[0];
      player.selectedSkills.weapon_E = wDef.slots.E.options[0];
    } else {
      const armorSlotMap = { chest: 'chest_R', head: 'head_D', boots: 'boots_F' };
      if (!GEAR.armors[gearId]) return { error: 'unknown_armor' };
      const aDef = GEAR.armors[gearId];
      if (aDef.slot !== slot) return { error: 'wrong_slot' };

      player.equipment[slot] = gearId;
      player.durability[slot] = MAX_DURABILITY;

      const skillSlot = armorSlotMap[slot];
      if (skillSlot) player.selectedSkills[skillSlot] = aDef.skill.options[0];
    }

    // Recalcula stats (gear + maestria de armaduras)
    this._recalcStats(player);
    return { ok: true };
  }

  // Desequipa a peca de gear de um slot e reseta a(s) skill(s) associada(s).
  // Limpa equipment[slot], zera as selectedSkills do slot e recalcula stats.
  unequipItem(player, slot) {
    const validSlots = ['weapon', 'chest', 'head', 'boots'];
    if (!validSlots.includes(slot)) return { error: 'invalid_slot' };

    player.equipment[slot] = null;

    if (slot === 'weapon') {
      player.selectedSkills.weapon_Q = null;
      player.selectedSkills.weapon_W = null;
      player.selectedSkills.weapon_E = null;
    } else {
      const armorSlotMap = { chest: 'chest_R', head: 'head_D', boots: 'boots_F' };
      const skillSlot = armorSlotMap[slot];
      if (skillSlot) player.selectedSkills[skillSlot] = null;
    }

    // Recalcula stats (peca removida deixa de contribuir)
    this._recalcStats(player);
    return { ok: true };
  }

  // Retorna as opcoes de skill por peca de gear equipada.
  // Para a arma: { [gearId]: { Q:[...], W:[...], E:[...] } }
  // Para armaduras: { [gearId]: { R|D|F: [...] } }
  // Usado pelo painel de habilidades (Skill Tree) no cliente.
  getGearOptions(player) {
    const opts = {};
    for (const [slot, gearId] of Object.entries(player.equipment)) {
      if (!gearId) continue;
      const def = slot === 'weapon' ? GEAR.weapons[gearId] : GEAR.armors[gearId];
      if (!def) continue;
      if (slot === 'weapon') {
        opts[gearId] = {
          Q: def.slots?.Q?.options || [],
          W: def.slots?.W?.options || [],
          E: def.slots?.E?.options || [],
        };
      } else {
        const slotKey = { chest: 'R', head: 'D', boots: 'F' }[slot];
        opts[gearId] = { [slotKey]: def.skill?.options || [] };
      }
    }
    return opts;
  }

  // Altera a skill selecionada em um slot.
  selectSkill(player, slotKey, skillId) {
    const validSlots = ['weapon_Q', 'weapon_W', 'weapon_E', 'chest_R', 'head_D', 'boots_F'];
    if (!validSlots.includes(slotKey)) return { error: 'invalid_slot' };

    const weaponId = player.equipment?.weapon;
    const wDef     = weaponId ? GEAR.weapons[weaponId] : null;
    const slotName = slotKey.split('_')[1];

    if (['Q', 'W', 'E'].includes(slotName)) {
      if (!wDef || !wDef.slots[slotName]?.options.includes(skillId))
        return { error: 'skill_not_available' };
    } else {
      const armorMap = { R: 'chest', D: 'head', F: 'boots' };
      const armorId  = player.equipment?.[armorMap[slotName]];
      const aDef     = armorId ? GEAR.armors[armorId] : null;
      if (!aDef || !aDef.skill?.options.includes(skillId))
        return { error: 'skill_not_available' };
    }

    player.selectedSkills[slotKey] = skillId;
    return { ok: true };
  }

  // ── Reparo ───────────────────────────────────────────────────────────────────

  /**
   * Repara um ou todos os slots de equipment, cobrando ouro.
   * Deve ser chamado apenas quando o player esta proximo do NPC Ferreiro.
   */
  repairItem(player, slot) {
    if (!player.durability) player.durability = {};

    const validSlots = ['weapon', 'chest', 'head', 'boots'];
    const slotsToRepair = slot === 'all'
      ? validSlots.filter(s => player.equipment[s])
      : (validSlots.includes(slot) ? [slot] : []);

    if (slotsToRepair.length === 0)
      return { error: slot === 'all' ? 'nothing_equipped' : 'invalid_slot' };

    let totalCost = 0;
    const repairs = [];
    for (const s of slotsToRepair) {
      const gearId = player.equipment[s];
      if (!gearId) continue;
      const curDur = player.durability[s] ?? MAX_DURABILITY;
      if (curDur >= MAX_DURABILITY) continue;
      const baseValue = REPAIR_BASE_VALUES[gearId] || 100;
      const cost = Math.ceil(baseValue * (1 - curDur / MAX_DURABILITY) * REPAIR_COST_RATE);
      repairs.push({ slot: s, gearId, fromDurability: curDur, cost });
      totalCost += cost;
    }

    if (repairs.length === 0) return { error: 'already_repaired' };
    if (player.gold < totalCost) return { error: 'not_enough_gold', cost: totalCost, gold: player.gold };

    player.gold -= totalCost;
    for (const r of repairs) {
      player.durability[r.slot] = MAX_DURABILITY;
    }

    // Recalcula stats (pecas antes quebradas voltam a contribuir)
    this._recalcStats(player);
    return { ok: true, totalCost, repairs, gold: player.gold };
  }

  // ── Colisoes ─────────────────────────────────────────────────────────────────

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
