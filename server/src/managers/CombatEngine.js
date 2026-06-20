// CombatEngine -- lógica de combate (PvP e PvM)
// v5: gear-based skills + Status Effects completo + Diminishing Returns em CC
//     + Equipment Mastery (bônus de dano/cura dinâmico por maestria de arma).
//
// Status effects suportados:
//   stun, root, slow, knockback, antiHeal, defenseDown, bleed (DoT),
//   shield, haste, ccImmune, cleanse, purge
//
// Diminishing Returns (CC):
//   1ª aplicação → 100% duração
//   2ª aplicação → 50%
//   3ª aplicação → 25%
//   4ª+ aplicação → 0% (ignorado)
//   Resetado após CC_DR_RESET_MS sem aquela CC

const SKILLS = require('../config/skills.json');
const {
  CRIT_CHANCE_BASE, CRIT_MULTIPLIER, MAP_W, MAP_H,
  CC_DR_TABLE, CC_DR_RESET_MS,
  DURABILITY_LOSS_PER_HIT,
  MASTERY_XP_PER_USE, MASTERY_XP_PER_HIT,
  MASTERY_WEAPON_DMG_PER_LEVEL,
  YELLOW_FAME_WEAPON_DMG_PER_LEVEL,
  MASTERY_BASE_MOB_XP,
  MASTERY_ZONE_MULT,
} = require('../config/constants');

// CC types que sofrem Diminishing Returns
const CC_TYPES_WITH_DR = new Set(['stun', 'root', 'slow', 'knockback']);

// Tipos de skill que concedem XP de maestria de arma quando resolvidos.
// Inclui tipos ofensivos (dano) e suporte de staff (heal, revive).
// Exclui buff_self e teleport para evitar ganho indevido por skills de armadura.
const MASTERY_SKILL_TYPES = new Set([
  'melee', 'ranged', 'ranged_aoe', 'melee_charge', 'melee_dot', 'trap',
  'heal_target', 'heal_aoe', 'revive',
]);

class CombatEngine {
  constructor(world, playerManager) {
    this.world   = world;
    this.players = playerManager;
    this._monsterMgr = null;
  }

  setMonsterManager(mm) { this._monsterMgr = mm; }

  // ── Maestria — multiplicador de dano/cura ────────────────────────────────────

  /**
   * Calcula o multiplicador de dano/cura baseado na maestria da arma equipada.
   * Level 1 = sem bônus. Level 10 = +18% dano. Yellow Fame 5 = +25% adicional.
   *
   * @param {object} caster
   * @returns {number} multiplicador (ex: 1.0 a ~1.43)
   */
  _getWeaponMasteryMult(caster) {
    const weaponId = caster.equipment?.weapon;
    if (!weaponId) return 1;
    const m = caster.equipmentMastery?.[weaponId];
    if (!m) return 1;
    return 1
      + (m.level - 1) * MASTERY_WEAPON_DMG_PER_LEVEL
      + (m.yellowFame?.level || 0) * YELLOW_FAME_WEAPON_DMG_PER_LEVEL;
  }

  // ── Validação e início de cast ───────────────────────────────────────────────

  startCast(casterId, skillId, tx, ty, now = Date.now()) {
    const caster = this.world.getPlayer(casterId);
    if (!caster || caster.dead) return { rejected: 'dead' };

    const sk = SKILLS[skillId];
    if (!sk) return { rejected: 'unknown_skill' };

    if (!this.players.playerHasSkill(caster, skillId))
      return { rejected: 'skill_not_equipped' };

    if (caster.casting) return { rejected: 'already_casting' };
    if ((caster.cooldowns[skillId] || 0) > now) return { rejected: 'cooldown' };

    const se = caster.statusEffects || {};
    const castBlocked = (se.stun && se.stun.endsAt > now) ||
                        (se.knockback && se.knockback.endsAt > now);
    if (castBlocked) return { rejected: 'cc_blocked' };

    if (sk.mana    && caster.mana    < sk.mana)    return { rejected: 'no_mana' };
    if (sk.stamina && caster.stamina < sk.stamina) return { rejected: 'no_stamina' };

    if (sk.condition === 'target_hp_below_30pct') {
      // Permite o cast; a condição é verificada na resolução
    }

    caster.cooldowns[skillId] = now + sk.cooldown;
    if (sk.mana)    caster.mana    -= sk.mana;
    if (sk.stamina) caster.stamina -= sk.stamina;

    if (sk.castTime === 0) {
      this._resolveAbility(caster, sk, tx, ty);
      return { resolved: true };
    }

    caster.casting = { skillId, endsAt: now + sk.castTime, total: sk.castTime, tx, ty };
    return { casting: true, endsAt: caster.casting.endsAt };
  }

  resolveDueCasts(now) {
    for (const p of this.world.players.values()) {
      if (p.casting && now >= p.casting.endsAt) {
        const { skillId, tx, ty } = p.casting;
        p.casting = null;
        const sk = SKILLS[skillId];
        if (sk) this._resolveAbility(p, sk, tx, ty);
      }
    }
  }

  // ── Dano a Player ────────────────────────────────────────────────────────────

  applyDamage(target, amount, attackerId = null) {
    if (!target || target.dead) return;
    const now = Date.now();

    // Dodge: inclui bônus de maestria de leather
    const dodgeTotal = (target.dodgeChance || 0) + (target.masteryDodgeBonus || 0);
    if (Math.random() < dodgeTotal) {
      this.world.io.to(target.id).emit('combat:dodge', { defenderId: target.id });
      return;
    }

    let finalDmg = amount;
    let isCrit   = false;
    if (Math.random() < CRIT_CHANCE_BASE) {
      finalDmg = Math.round(amount * CRIT_MULTIPLIER);
      isCrit   = true;
    }

    const se = target.statusEffects || {};
    if (se.defenseDown && se.defenseDown.endsAt > now)
      finalDmg = Math.round(finalDmg * (1 + se.defenseDown.factor));

    if (target.damageReduction)
      finalDmg = Math.round(finalDmg * (1 - target.damageReduction));

    if (target.shield > 0) {
      const absorbed = Math.min(target.shield, finalDmg);
      target.shield  = Math.max(0, target.shield - absorbed);
      finalDmg      -= absorbed;
      if (finalDmg <= 0) return;
    }

    target.hp = Math.max(0, target.hp - finalDmg);

    if (target.casting) {
      const castingSk = SKILLS[target.casting.skillId];
      if (castingSk && castingSk.interruptible) {
        target.casting = null;
        this.world.emitInterrupt({ id: target.id, ability: castingSk.name });
      }
    }

    this.world.emitHit({ from: attackerId, to: target.id, damage: finalDmg, crit: isCrit, hp: target.hp });

    // Desgaste de durabilidade: hit causa -1 ponto em uma peça de armadura aleatória.
    // A peça atingida também ganha XP de maestria por absorver o dano.
    if (target.hp > 0 && target.equipment && target.durability) {
      const armorSlots = ['chest', 'head', 'boots'].filter(s => target.equipment[s]);
      if (armorSlots.length > 0) {
        const hitSlot   = armorSlots[Math.floor(Math.random() * armorSlots.length)];
        const hitArmorId = target.equipment[hitSlot];

        target.durability[hitSlot] = Math.max(0,
          (target.durability[hitSlot] ?? 100) - DURABILITY_LOSS_PER_HIT
        );

        // XP de maestria de armadura: apenas hits de monstros concedem XP.
        // PvP mútuo seria trivialmente explorável; monstros têm ID no world.monsters.
        // Escalonado pelo multiplicador de zona: safe=0 elimina farming sem risco.
        const isMonsterAttack = attackerId && this.world.monsters.has(attackerId);
        if (hitArmorId && (target.durability[hitSlot] ?? 0) > 0 && isMonsterAttack) {
          const zoneMult = (MASTERY_ZONE_MULT[this.world.zoneType] ?? 1.0);
          const xp = Math.round(MASTERY_XP_PER_HIT * zoneMult);
          if (xp > 0) this.players.gainMasteryXp(target, hitArmorId, xp);
        }
      }
    }

    if (target.hp <= 0 && !target.dead) {
      target.dead          = true;
      target.casting       = null;
      target.statusEffects = {};

      const deathLoot = this.players.handlePlayerDeath(target, this.world.zoneType);
      this.world.io.to(target.id).emit('player:death_loot', deathLoot);

      this.world.emitDeath({ id: target.id, killerId: attackerId });
      this.players.scheduleRespawn(target);
    }
  }

  // ── Dano a Monstro ───────────────────────────────────────────────────────────

  applyDamageToMonster(monster, amount, attackerId = null) {
    if (!monster || monster.hp <= 0) return;
    const finalDmg = Math.max(1, Math.round(amount));
    monster.hp = Math.max(0, monster.hp - finalDmg);
    if (attackerId) monster.lastHitBy = attackerId;
    this.world.emitHit({ from: attackerId, to: monster.id, damage: finalDmg, crit: false, hp: monster.hp, isMonster: true });

    // Weapon mastery XP: proporcional ao xpReward do mob × multiplicador de zona.
    // Só concede quando o atacante é um player (não monstro vs monstro).
    // Fórmula: MASTERY_XP_PER_USE × (mob.xpReward / BASE_MOB_XP) × zoneMult
    // Exemplo: golem (xpReward=200) em black → 10 × 4 × 1.5 = 60 XP
    //          rato  (xpReward=25)  em safe  → 10 × 0.5 × 0 = 0 XP
    const attacker = attackerId ? this.world.getPlayer(attackerId) : null;
    if (attacker) {
      const zoneMult  = MASTERY_ZONE_MULT[this.world.zoneType] ?? 1.0;
      const mobMult   = (monster.xpReward || MASTERY_BASE_MOB_XP) / MASTERY_BASE_MOB_XP;
      const xp        = Math.round(MASTERY_XP_PER_USE * mobMult * zoneMult);
      const weaponId  = attacker.equipment?.weapon;
      if (xp > 0 && weaponId) this.players.gainMasteryXp(attacker, weaponId, xp);
    }
  }

  // ── Status Effects ───────────────────────────────────────────────────────────

  // Aplica um status effect a um alvo (player).
  // Respeita ccImmune e Diminishing Returns para CC types.
  applyStatusEffect(target, effectType, params = {}, now = Date.now()) {
    if (!target || target.dead) return false;

    const se = target.statusEffects || {};
    target.statusEffects = se;

    if (CC_TYPES_WITH_DR.has(effectType)) {
      if (se.ccImmune && se.ccImmune.endsAt > now) return false;
    }

    let duration = params.duration || 0;
    if (CC_TYPES_WITH_DR.has(effectType) && duration > 0) {
      const drMult = this._getCCDRMultiplier(target, effectType, now);
      if (drMult === 0) return false;
      duration = Math.round(duration * drMult);
      this._recordCCApplication(target, effectType, now);
    }

    switch (effectType) {
      case 'stun':
        se.stun = { endsAt: now + duration };
        if (target.casting) {
          const sk = SKILLS[target.casting.skillId];
          if (sk) {
            target.casting = null;
            this.world.emitInterrupt({ id: target.id, ability: sk.name });
          }
        }
        break;

      case 'root':
        se.root = { endsAt: now + duration };
        break;

      case 'slow':
        se.slow = { endsAt: now + duration, factor: params.factor || 0.5 };
        break;

      case 'knockback':
        if (params.dx !== undefined && params.dy !== undefined && params.distance) {
          const dist = params.distance > 0 ? params.distance : Math.abs(params.distance);
          const dir  = params.distance > 0 ? 1 : -1;
          target.x = Math.max(0, Math.min(MAP_W, target.x + params.dx * dist * dir));
          target.y = Math.max(0, Math.min(MAP_H, target.y + params.dy * dist * dir));
        }
        se.knockback = { endsAt: now + 300 };
        break;

      case 'antiHeal':
        se.antiHeal = { endsAt: now + duration, factor: params.factor || 0.5 };
        break;

      case 'defenseDown':
        se.defenseDown = { endsAt: now + duration, factor: params.factor || 0.2 };
        break;

      case 'bleed':
        if (!se.bleed || se.bleed.endsAt < now) {
          se.bleed = {
            endsAt:    now + duration,
            dotDamage: params.dotDamage || 5,
            dotTicks:  params.dotTicks  || 4,
            dotInterval: params.dotInterval || 1000,
            attackerId: params.attackerId || null,
          };
          this._startBleedTick(target, se.bleed);
        }
        break;

      case 'shield':
        target.shield = (target.shield || 0) + (params.value || 0);
        if (params.duration) {
          setTimeout(() => {
            target.shield = Math.max(0, (target.shield || 0) - (params.value || 0));
          }, params.duration);
        }
        break;

      case 'haste':
        se.haste = { endsAt: now + duration, factor: params.factor || 1.5 };
        break;

      case 'ccImmune':
        se.ccImmune = { endsAt: now + duration };
        for (const cc of CC_TYPES_WITH_DR) { delete se[cc]; }
        break;

      case 'cleanse':
        for (const type of (params.types || ['stun', 'root', 'slow', 'antiHeal', 'defenseDown'])) {
          delete se[type];
        }
        break;

      case 'purge':
        delete se.haste;
        delete se.ccImmune;
        if (target.shield) target.shield = Math.max(0, target.shield - 50);
        break;

      default:
        return false;
    }

    if (target.id) {
      this.world.io.to(target.id).emit('status:applied', {
        type:   effectType,
        endsAt: (se[effectType] || {}).endsAt || now + duration,
      });
    }

    return true;
  }

  _startBleedTick(target, bleedState) {
    let ticks = 0;
    const iv = setInterval(() => {
      if (target.dead || ticks >= bleedState.dotTicks) {
        clearInterval(iv);
        return;
      }
      const now = Date.now();
      if (!target.statusEffects?.bleed || target.statusEffects.bleed.endsAt < now) {
        clearInterval(iv);
        return;
      }
      ticks++;
      if (!target.dead) this.applyDamage(target, bleedState.dotDamage, bleedState.attackerId);
    }, bleedState.dotInterval);
  }

  // ── Diminishing Returns ───────────────────────────────────────────────────────

  _getCCDRMultiplier(target, ccType, now) {
    const hist = (target.ccHistory || {})[ccType];
    if (!hist) return CC_DR_TABLE[0] !== undefined ? CC_DR_TABLE[0] : 1.0;
    if (now - hist.lastAppliedAt > CC_DR_RESET_MS) return 1.0;
    const idx = Math.min(hist.count, CC_DR_TABLE.length - 1);
    return CC_DR_TABLE[idx];
  }

  _recordCCApplication(target, ccType, now) {
    if (!target.ccHistory) target.ccHistory = {};
    const hist = target.ccHistory[ccType];
    if (!hist || now - hist.lastAppliedAt > CC_DR_RESET_MS) {
      target.ccHistory[ccType] = { count: 1, lastAppliedAt: now };
    } else {
      target.ccHistory[ccType].count++;
      target.ccHistory[ccType].lastAppliedAt = now;
    }
  }

  // ── Resolução de Abilities ────────────────────────────────────────────────────

  _resolveAbility(caster, sk, tx, ty) {
    const dist = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);
    const now  = Date.now();

    // Multiplicador de maestria de arma: +2% dano por nível de maestria,
    // +5% adicional por nível de Fama Amarela. Aplicado a dano E curas.
    const masteryMult = this._getWeaponMasteryMult(caster);
    const dmgMult = (caster.damageBonus || 1) * (caster.damageMult || 1) * masteryMult;
    const baseDmg = Math.round((sk.damage || 0) * dmgMult);

    // ── melee: todos os alvos no range ──────────────────────────────────────────
    if (sk.type === 'melee') {
      const aoeRadius = sk.aoeRadius || sk.range;
      for (const t of this.world.players.values()) {
        if (t.id === caster.id || t.dead) continue;
        if (dist(caster, t) <= aoeRadius) {
          this.applyDamage(t, baseDmg, caster.id);
          if (sk.statusEffect) this._applySkillEffect(caster, t, sk, now);
        }
      }
      for (const m of this.world.monsters.values()) {
        if (m.hp <= 0) continue;
        if (dist(caster, m) <= aoeRadius) this.applyDamageToMonster(m, baseDmg, caster.id);
      }

    // ── ranged: alvo único mais próximo no range ─────────────────────────────
    } else if (sk.type === 'ranged') {
      let best = null, bestD = sk.range, bestIsMonster = false;
      for (const t of this.world.players.values()) {
        if (t.id === caster.id || t.dead) continue;
        const d = dist(caster, t);
        if (d <= sk.range && d < bestD) { best = t; bestD = d; bestIsMonster = false; }
      }
      for (const m of this.world.monsters.values()) {
        if (m.hp <= 0) continue;
        const d = dist(caster, m);
        if (d <= sk.range && d < bestD) { best = m; bestD = d; bestIsMonster = true; }
      }
      if (best) {
        if (bestIsMonster) {
          this.applyDamageToMonster(best, baseDmg, caster.id);
        } else {
          if (sk.condition === 'target_hp_below_30pct' && best.hp > best.maxHp * 0.3)
            return;

          this.applyDamage(best, baseDmg, caster.id);
          if (sk.statusEffect) this._applySkillEffect(caster, best, sk, now);
          if (sk.selfHeal) caster.hp = Math.min(caster.maxHp, caster.hp + sk.selfHeal);
          if (sk.manaDrain) best.mana = Math.max(0, (best.mana || 0) - sk.manaDrain);
          if (sk.interruptsCast && best.casting) {
            const castingSk = SKILLS[best.casting.skillId];
            if (castingSk) {
              best.casting = null;
              this.world.emitInterrupt({ id: best.id, ability: castingSk.name });
            }
          }
        }
      }

    // ── ranged_aoe: área em (tx, ty) ────────────────────────────────────────
    } else if (sk.type === 'ranged_aoe') {
      const origin = sk.range === 0 ? { x: caster.x, y: caster.y } : { x: tx, y: ty };
      const radius = sk.aoeRadius || 80;
      for (const t of this.world.players.values()) {
        if (t.id === caster.id || t.dead) continue;
        if (dist(origin, t) <= radius) {
          this.applyDamage(t, baseDmg, caster.id);
          if (sk.statusEffect) this._applySkillEffect(caster, t, sk, now);
        }
      }
      for (const m of this.world.monsters.values()) {
        if (m.hp <= 0) continue;
        if (dist(origin, m) <= radius) this.applyDamageToMonster(m, baseDmg, caster.id);
      }

    // ── heal_target: cura o aliado mais próximo (masteryMult aplica ao heal) ──
    } else if (sk.type === 'heal_target') {
      let healTarget = caster;
      let minD = sk.range || 300;
      for (const p of this.world.players.values()) {
        if (p.id === caster.id || p.dead) continue;
        const d = dist(caster, p);
        if (d < minD) { healTarget = p; minD = d; }
      }
      const se = healTarget.statusEffects || {};
      let heal  = Math.round((sk.healAmount || 0) * masteryMult);
      if (se.antiHeal && se.antiHeal.endsAt > now) heal = Math.round(heal * se.antiHeal.factor);
      healTarget.hp = Math.min(healTarget.maxHp, healTarget.hp + heal);

    // ── heal_aoe: cura todos os aliados próximos ─────────────────────────────
    } else if (sk.type === 'heal_aoe') {
      const radius = sk.aoeRadius || 150;
      for (const p of this.world.players.values()) {
        if (p.dead) continue;
        if (dist(caster, p) <= radius) {
          const se  = p.statusEffects || {};
          let heal  = Math.round((sk.healAmount || 0) * masteryMult);
          if (se.antiHeal && se.antiHeal.endsAt > now) heal = Math.round(heal * se.antiHeal.factor);
          p.hp = Math.min(p.maxHp, p.hp + heal);
        }
      }

    // ── buff_self: buff no próprio caster ────────────────────────────────────
    } else if (sk.type === 'buff_self') {
      if (sk.damageBonus)     caster.damageBonus     = sk.damageBonus;
      if (sk.damageReduction) caster.damageReduction = sk.damageReduction;
      if (sk.dodgeChance)     caster.dodgeChance     = sk.dodgeChance;
      if (sk.shieldAmount)    this.applyStatusEffect(caster, 'shield', { value: sk.shieldAmount, duration: sk.duration }, now);
      if (sk.manaRestore)     caster.mana = Math.min(caster.maxMana, caster.mana + sk.manaRestore);

      if (sk.statusEffect)    this._applySkillEffect(caster, caster, sk, now);

      if (sk.duration) {
        setTimeout(() => {
          if (sk.damageBonus)     delete caster.damageBonus;
          if (sk.damageReduction && caster.damageReduction === sk.damageReduction)
            caster.damageReduction = 0;
          if (sk.dodgeChance)     delete caster.dodgeChance;
        }, sk.duration);
      }

    // ── teleport: blink na direção (tx, ty) ──────────────────────────────────
    } else if (sk.type === 'teleport') {
      const dx = tx - caster.x, dy = ty - caster.y;
      const d  = Math.hypot(dx, dy);
      if (d > 0) {
        const step = Math.min(d, sk.range || 200);
        caster.x = Math.max(0, Math.min(MAP_W, caster.x + (dx / d) * step));
        caster.y = Math.max(0, Math.min(MAP_H, caster.y + (dy / d) * step));
      }
      if (sk.statusEffect) this._applySkillEffect(caster, caster, sk, now);

    // ── melee_charge: dash até alvo + dano + efeitos ─────────────────────────
    } else if (sk.type === 'melee_charge') {
      let best = null, bestD = sk.range, bestIsMonster = false;
      for (const t of this.world.players.values()) {
        if (t.id === caster.id || t.dead) continue;
        const d = dist(caster, t);
        if (d < bestD) { best = t; bestD = d; bestIsMonster = false; }
      }
      for (const m of this.world.monsters.values()) {
        if (m.hp <= 0) continue;
        const d = dist(caster, m);
        if (d < bestD) { best = m; bestD = d; bestIsMonster = true; }
      }
      if (best) {
        const angle  = Math.atan2(best.y - caster.y, best.x - caster.x);
        const chargeD = Math.max(0, bestD - 25);
        caster.x = Math.max(0, Math.min(MAP_W, caster.x + Math.cos(angle) * chargeD));
        caster.y = Math.max(0, Math.min(MAP_H, caster.y + Math.sin(angle) * chargeD));
        if (bestIsMonster) {
          this.applyDamageToMonster(best, baseDmg, caster.id);
        } else {
          this.applyDamage(best, baseDmg, caster.id);
          if (sk.statusEffect) {
            const dx = Math.cos(angle), dy = Math.sin(angle);
            this._applySkillEffect(caster, best, sk, now, { dx, dy });
          }
        }
      }

    // ── melee_dot: dano imediato + ticks de DoT ──────────────────────────────
    } else if (sk.type === 'melee_dot') {
      let best = null, bestD = sk.range, bestIsMonster = false;
      for (const t of this.world.players.values()) {
        if (t.id === caster.id || t.dead) continue;
        const d = dist(caster, t);
        if (d < bestD) { best = t; bestD = d; bestIsMonster = false; }
      }
      for (const m of this.world.monsters.values()) {
        if (m.hp <= 0) continue;
        const d = dist(caster, m);
        if (d < bestD) { best = m; bestD = d; bestIsMonster = true; }
      }
      if (best) {
        if (bestIsMonster) this.applyDamageToMonster(best, baseDmg, caster.id);
        else {
          this.applyDamage(best, baseDmg, caster.id);
          if (sk.statusEffect) this._applySkillEffect(caster, best, sk, now);
        }
        if (sk.dotDamage && sk.dotTicks && sk.dotInterval && sk.statusEffect !== 'bleed') {
          let tick = 0;
          const iv = setInterval(() => {
            tick++;
            if (tick > sk.dotTicks) { clearInterval(iv); return; }
            if (bestIsMonster) {
              if (best.hp > 0) this.applyDamageToMonster(best, sk.dotDamage, caster.id);
              else clearInterval(iv);
            } else {
              if (!best.dead) this.applyDamage(best, sk.dotDamage, caster.id);
              else clearInterval(iv);
            }
          }, sk.dotInterval);
        }
      }

    // ── trap: root na área (tx, ty) ──────────────────────────────────────────
    } else if (sk.type === 'trap') {
      const origin  = { x: tx, y: ty };
      const radius  = sk.aoeRadius || 60;
      for (const m of this.world.monsters.values()) {
        if (m.hp <= 0 || dist(origin, m) > radius) continue;
        this.applyDamageToMonster(m, baseDmg, caster.id);
        m.rooted      = true;
        m.rootedUntil = now + (sk.statusDuration || 3000);
        setTimeout(() => { m.rooted = false; }, sk.statusDuration || 3000);
      }
      for (const t of this.world.players.values()) {
        if (t.id === caster.id || t.dead) continue;
        if (dist(origin, t) > radius) continue;
        this.applyDamage(t, baseDmg, caster.id);
        if (sk.statusEffect) this._applySkillEffect(caster, t, sk, now);
      }

    // ── revive: ressuscita aliado morto próximo ───────────────────────────────
    } else if (sk.type === 'revive') {
      let deadAlly = null, deadAllyD = sk.range || 120;
      for (const p of this.world.players.values()) {
        if (p.id === caster.id || !p.dead) continue;
        const d = dist(caster, p);
        if (d < deadAllyD) { deadAlly = p; deadAllyD = d; }
      }
      if (deadAlly) {
        deadAlly.dead    = false;
        // healAmount também escala com maestria do ressuscitador
        deadAlly.hp      = Math.min(deadAlly.maxHp, Math.round((sk.healAmount || 50) * masteryMult));
        deadAlly.mana    = 0;
        deadAlly.shield  = 0;
        deadAlly.casting = null;
        deadAlly.statusEffects = {};
        this.world.io.to(deadAlly.id).emit('player:revived', { hp: deadAlly.hp });
      }
    }

    // XP de maestria de arma é concedido em applyDamageToMonster (por hit em monstro),
    // não aqui. Isso evita farming spammando skills no vácuo ou em monstros triviais.
  }

  // ── Aplicação de Efeito de Skill ─────────────────────────────────────────────

  _applySkillEffect(caster, target, sk, now, extra = {}) {
    if (!sk.statusEffect || !target || target.dead) return;

    switch (sk.statusEffect) {
      case 'stun':
        this.applyStatusEffect(target, 'stun', { duration: sk.statusDuration || 1500 }, now);
        break;
      case 'root':
        this.applyStatusEffect(target, 'root', { duration: sk.statusDuration || 2000 }, now);
        break;
      case 'slow':
        this.applyStatusEffect(target, 'slow', {
          duration: sk.statusDuration || 2000,
          factor:   sk.statusFactor  || 0.5,
        }, now);
        break;
      case 'knockback': {
        const dx = extra.dx || (target.x - caster.x) / (Math.hypot(target.x - caster.x, target.y - caster.y) || 1);
        const dy = extra.dy || (target.y - caster.y) / (Math.hypot(target.x - caster.x, target.y - caster.y) || 1);
        this.applyStatusEffect(target, 'knockback', {
          dx, dy, distance: sk.knockbackDistance || 80,
        }, now);
        break;
      }
      case 'antiHeal':
        this.applyStatusEffect(target, 'antiHeal', {
          duration: sk.statusDuration || 3000,
          factor:   sk.statusFactor  || 0.5,
        }, now);
        break;
      case 'defenseDown':
        this.applyStatusEffect(target, 'defenseDown', {
          duration: sk.statusDuration || 3000,
          factor:   sk.statusFactor  || 0.2,
        }, now);
        break;
      case 'bleed':
        this.applyStatusEffect(target, 'bleed', {
          duration:    sk.statusDuration || 4000,
          dotDamage:   sk.dotDamage  || 4,
          dotTicks:    sk.dotTicks   || 4,
          dotInterval: sk.dotInterval || 1000,
          attackerId:  caster.id,
        }, now);
        break;

      default:
        break;
    }
  }

  // ── Abilities Disponíveis ────────────────────────────────────────────────────

  /** Retorna o catálogo completo de skills (skills.json). */
  getSkillCatalog() {
    return SKILLS;
  }

  /**
   * Retorna lista de skills ativas do player derivada do gear equipado.
   * Usado pelo servidor ao emitir player:joined / gear:equipped.
   */
  getPlayerAbilities(player) {
    const abilities = [];
    for (const [slot, skillId] of Object.entries(player.selectedSkills || {})) {
      if (!skillId) continue;
      const sk = SKILLS[skillId];
      if (!sk) continue;
      abilities.push({ slot, skillId, ...sk });
    }
    return abilities;
  }
}

module.exports = CombatEngine;
