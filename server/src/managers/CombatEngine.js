// CombatEngine -- lógica de combate (PvP e PvM)
// v4: gear-based skills + Status Effects completo + Diminishing Returns em CC.
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
const { CRIT_CHANCE_BASE, CRIT_MULTIPLIER, MAP_W, MAP_H, CC_DR_TABLE, CC_DR_RESET_MS } = require('../config/constants');

// CC types que sofrem Diminishing Returns
const CC_TYPES_WITH_DR = new Set(['stun', 'root', 'slow', 'knockback']);

class CombatEngine {
  constructor(world, playerManager) {
    this.world   = world;
    this.players = playerManager;
    this._monsterMgr = null;
  }

  setMonsterManager(mm) { this._monsterMgr = mm; }

  // ── Validação e início de cast ───────────────────────────────────────────────

  startCast(casterId, skillId, tx, ty, now = Date.now()) {
    const caster = this.world.getPlayer(casterId);
    if (!caster || caster.dead) return { rejected: 'dead' };

    const sk = SKILLS[skillId];
    if (!sk) return { rejected: 'unknown_skill' };

    // Verifica que o jogador tem esta skill equipada
    if (!this.players.playerHasSkill(caster, skillId))
      return { rejected: 'skill_not_equipped' };

    if (caster.casting) return { rejected: 'already_casting' };
    if ((caster.cooldowns[skillId] || 0) > now) return { rejected: 'cooldown' };

    // Verifica status effects que bloqueiam cast
    const se = caster.statusEffects || {};
    const castBlocked = (se.stun && se.stun.endsAt > now) ||
                        (se.knockback && se.knockback.endsAt > now);
    if (castBlocked) return { rejected: 'cc_blocked' };

    if (sk.mana    && caster.mana    < sk.mana)    return { rejected: 'no_mana' };
    if (sk.stamina && caster.stamina < sk.stamina) return { rejected: 'no_stamina' };

    // Condições especiais
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

    // Dodge
    if (Math.random() < (target.dodgeChance || 0)) {
      this.world.io.to(target.id).emit('combat:dodge', { defenderId: target.id });
      return;
    }

    let finalDmg = amount;
    let isCrit   = false;
    if (Math.random() < CRIT_CHANCE_BASE) {
      finalDmg = Math.round(amount * CRIT_MULTIPLIER);
      isCrit   = true;
    }

    // DefenseDown amplia dano recebido
    const se = target.statusEffects || {};
    if (se.defenseDown && se.defenseDown.endsAt > now)
      finalDmg = Math.round(finalDmg * (1 + se.defenseDown.factor));

    // Damage reduction (armadura e buffs)
    if (target.damageReduction)
      finalDmg = Math.round(finalDmg * (1 - target.damageReduction));

    // Shield absorve antes do HP
    if (target.shield > 0) {
      const absorbed = Math.min(target.shield, finalDmg);
      target.shield  = Math.max(0, target.shield - absorbed);
      finalDmg      -= absorbed;
      if (finalDmg <= 0) return;
    }

    target.hp = Math.max(0, target.hp - finalDmg);

    // Interrompe cast se skill for interruptível
    if (target.casting) {
      const castingSk = SKILLS[target.casting.skillId];
      if (castingSk && castingSk.interruptible) {
        target.casting = null;
        this.world.emitInterrupt({ id: target.id, ability: castingSk.name });
      }
    }

    this.world.emitHit({ from: attackerId, to: target.id, damage: finalDmg, crit: isCrit, hp: target.hp });

    if (target.hp <= 0 && !target.dead) {
      target.dead    = true;
      target.casting = null;
      target.statusEffects = {};
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
  }

  // ── Status Effects ───────────────────────────────────────────────────────────

  // Aplica um status effect a um alvo (player).
  // Respeita ccImmune e Diminishing Returns para CC types.
  applyStatusEffect(target, effectType, params = {}, now = Date.now()) {
    if (!target || target.dead) return false;

    const se = target.statusEffects || {};
    target.statusEffects = se;

    // CC Immune bloqueia todos os CC com DR
    if (CC_TYPES_WITH_DR.has(effectType)) {
      if (se.ccImmune && se.ccImmune.endsAt > now) return false;
    }

    // Calcular duração com Diminishing Returns
    let duration = params.duration || 0;
    if (CC_TYPES_WITH_DR.has(effectType) && duration > 0) {
      const drMult = this._getCCDRMultiplier(target, effectType, now);
      if (drMult === 0) return false;  // completamente imune por DR
      duration = Math.round(duration * drMult);
      this._recordCCApplication(target, effectType, now);
    }

    // Aplicar o efeito
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
          const dir  = params.distance > 0 ? 1 : -1;  // positivo = empurrar, negativo = puxar
          target.x = Math.max(0, Math.min(MAP_W, target.x + params.dx * dist * dir));
          target.y = Math.max(0, Math.min(MAP_H, target.y + params.dy * dist * dir));
        }
        se.knockback = { endsAt: now + 300 }; // breve janela de stun após knockback
        break;

      case 'antiHeal':
        se.antiHeal = { endsAt: now + duration, factor: params.factor || 0.5 };
        break;

      case 'defenseDown':
        se.defenseDown = { endsAt: now + duration, factor: params.factor || 0.2 };
        break;

      case 'bleed':
        // Bleed: DoT incurável. Aplica independente mesmo se já existir.
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
        // Remove CCs ativos
        for (const cc of CC_TYPES_WITH_DR) { delete se[cc]; }
        break;

      case 'cleanse':
        // Remove debuffs especificados (não remove bleed — é incurável)
        for (const type of (params.types || ['stun', 'root', 'slow', 'antiHeal', 'defenseDown'])) {
          delete se[type];
        }
        break;

      case 'purge':
        // Remove buffs do alvo (shield, haste, ccImmune, damageBonus)
        delete se.haste;
        delete se.ccImmune;
        if (target.shield) target.shield = Math.max(0, target.shield - 50); // remove parcialmente
        break;

      default:
        return false;
    }

    // Notifica cliente do novo status effect
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

    // Resetar se passou o tempo de reset
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

    const dmgMult = (caster.damageBonus || 1) * (caster.damageMult || 1);
    const baseDmg = Math.round((sk.damage || 0) * dmgMult);

    // ── melee: todos os alvos no range ──────────────────────────────────────────
    if (sk.type === 'melee') {
      const aoeRadius = sk.aoeRadius || sk.range;  // alguns melee têm cleave (aoeRadius = range)
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
          // Verifica condição especial (ex: execute)
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
      // Se range = 0, aplica ao redor do caster
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

    // ── heal_target: cura o aliado mais próximo (ou self) ───────────────────
    } else if (sk.type === 'heal_target') {
      let healTarget = caster;
      let minD = sk.range || 300;
      for (const p of this.world.players.values()) {
        if (p.id === caster.id || p.dead) continue;
        const d = dist(caster, p);
        if (d < minD) { healTarget = p; minD = d; }
      }
      const se = healTarget.statusEffects || {};
      let heal  = sk.healAmount || 0;
      if (se.antiHeal && se.antiHeal.endsAt > now) heal = Math.round(heal * se.antiHeal.factor);
      healTarget.hp = Math.min(healTarget.maxHp, healTarget.hp + heal);

    // ── heal_aoe: cura todos os aliados próximos ─────────────────────────────
    } else if (sk.type === 'heal_aoe') {
      const radius = sk.aoeRadius || 150;
      for (const p of this.world.players.values()) {
        if (p.dead) continue;
        if (dist(caster, p) <= radius) {
          const se  = p.statusEffects || {};
          let heal  = sk.healAmount || 0;
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
        // DoT extra além do status effect
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
        deadAlly.hp      = Math.min(deadAlly.maxHp, sk.healAmount || 50);
        deadAlly.mana    = 0;
        deadAlly.shield  = 0;
        deadAlly.casting = null;
        deadAlly.statusEffects = {};
        this.world.io.to(deadAlly.id).emit('player:revived', { hp: deadAlly.hp });
      }
    }
  }

  // ── Aplicação de Efeito de Skill ─────────────────────────────────────────────

  // Centraliza a aplicação de status effects vindos de skills.
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
      case 'haste':
        this.applyStatusEffect(target, 'haste', {
          duration: sk.statusDuration || 3000,
          factor:   sk.statusFactor  || 1.5,
        }, now);
        break;
      case 'ccImmune':
        this.applyStatusEffect(target, 'ccImmune', { duration: sk.statusDuration || 2000 }, now);
        break;
      case 'cleanse':
        this.applyStatusEffect(target, 'cleanse', { types: sk.cleanseTypes }, now);
        break;
      case 'purge':
        this.applyStatusEffect(target, 'purge', {}, now);
        break;
    }
  }

  // ── API pública (usada em server.js) ─────────────────────────────────────────

  // Retorna o array de skill objects ativos do player (baseado em selectedSkills).
  getPlayerAbilities(player) {
    const slots = ['weapon_Q', 'weapon_W', 'weapon_E', 'chest_R', 'head_D'];
    return slots.map(slot => {
      const skillId = (player.selectedSkills || {})[slot];
      return skillId ? (SKILLS[skillId] || null) : null;
    });
  }

  getSkillCatalog() { return SKILLS; }
}

module.exports = CombatEngine;
