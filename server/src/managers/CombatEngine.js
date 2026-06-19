// CombatEngine -- toda a logica de combate (PvP e PvM)
// v2: bug fix critico: _resolveAbility agora acerta MONSTROS tambem.
// v3: implementa todos os tipos de skill; shield; damageMult por level.
const SKILLS = require('../config/skills.json');
const { CRIT_CHANCE_BASE, CRIT_MULTIPLIER, MAP_W, MAP_H } = require('../config/constants');

const ALL_SKILLS = {};
for (const classSkills of Object.values(SKILLS)) {
  for (const sk of classSkills) ALL_SKILLS[sk.id] = sk;
}

class CombatEngine {
  constructor(world, playerManager) {
    this.world   = world;
    this.players = playerManager;
    this._monsterMgr = null;
  }

  setMonsterManager(mm) { this._monsterMgr = mm; }

  startCast(casterId, abilityId, tx, ty, now = Date.now()) {
    const caster = this.world.getPlayer(casterId);
    if (!caster || caster.dead) return { rejected: 'dead' };

    const sk = ALL_SKILLS[abilityId];
    if (!sk) return { rejected: 'unknown_ability' };
    if (caster.casting) return { rejected: 'already_casting' };
    if ((caster.cooldowns[abilityId] || 0) > now) return { rejected: 'cooldown' };
    if (sk.mana    && caster.mana    < sk.mana)    return { rejected: 'no_mana' };
    if (sk.stamina && caster.stamina < sk.stamina) return { rejected: 'no_stamina' };

    if (sk.mana)    caster.mana    -= sk.mana;
    if (sk.stamina) caster.stamina -= sk.stamina;
    caster.cooldowns[abilityId] = now + sk.cooldown;

    if (sk.castTime === 0) {
      this._resolveAbility(caster, sk, tx, ty);
      return { resolved: true };
    }

    caster.casting = { abilityId, endsAt: now + sk.castTime, total: sk.castTime, tx, ty };
    return { casting: true, endsAt: caster.casting.endsAt };
  }

  resolveDueCasts(now) {
    for (const p of this.world.players.values()) {
      if (p.casting && now >= p.casting.endsAt) {
        const { abilityId, tx, ty } = p.casting;
        p.casting = null;
        const sk = ALL_SKILLS[abilityId];
        if (sk) this._resolveAbility(p, sk, tx, ty);
      }
    }
  }

  // Aplica dano a um PLAYER (considera shield)
  applyDamage(target, amount, attackerId = null) {
    if (!target || target.dead) return;

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

    if (target.damageReduction)
      finalDmg = Math.round(finalDmg * (1 - target.damageReduction));

    // Shield absorve dano antes do HP
    if (target.shield > 0) {
      const absorbed = Math.min(target.shield, finalDmg);
      target.shield  = Math.max(0, target.shield - absorbed);
      finalDmg      -= absorbed;
      if (finalDmg <= 0) return;
    }

    target.hp = Math.max(0, target.hp - finalDmg);

    if (target.casting) {
      const sk = ALL_SKILLS[target.casting.abilityId];
      if (sk && sk.interruptible) {
        target.casting = null;
        this.world.emitInterrupt({ id: target.id, ability: sk.name });
      }
    }

    this.world.emitHit({ from: attackerId, to: target.id, damage: finalDmg, crit: isCrit, hp: target.hp });

    if (target.hp <= 0 && !target.dead) {
      target.dead    = true;
      target.casting = null;
      this.world.emitDeath({ id: target.id });
      this.players.scheduleRespawn(target);
    }
  }

  // Aplica dano a um MONSTRO
  applyDamageToMonster(monster, amount, attackerId = null) {
    if (!monster || monster.hp <= 0) return;

    const finalDmg = Math.max(1, Math.round(amount));
    monster.hp = Math.max(0, monster.hp - finalDmg);

    if (attackerId) monster.lastHitBy = attackerId;

    this.world.emitHit({
      from: attackerId,
      to: monster.id,
      damage: finalDmg,
      crit: false,
      hp: monster.hp,
      isMonster: true,
    });
  }

  _resolveAbility(caster, sk, tx, ty) {
    const dist = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);

    // Dano: buff temporario (War Cry) x escalonamento por level
    const dmgMult = (caster.damageBonus || 1) * (caster.damageMult || 1);
    const baseDmg = Math.round((sk.damage || 0) * dmgMult);

    if (sk.type === 'melee') {
      for (const t of this.world.players.values()) {
        if (t.id === caster.id || t.dead) continue;
        if (dist(caster, t) <= sk.range) this.applyDamage(t, baseDmg, caster.id);
      }
      for (const m of this.world.monsters.values()) {
        if (m.hp <= 0) continue;
        if (dist(caster, m) <= sk.range) this.applyDamageToMonster(m, baseDmg, caster.id);
      }

    } else if (sk.type === 'ranged') {
      let best = null, bestD = Infinity, bestIsMonster = false;
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
          this.applyDamage(best, baseDmg, caster.id);
          if (sk.slowEffect) {
            best.speed = Math.round(best.speed * (1 - sk.slowEffect));
            setTimeout(() => { best.speed = best.maxSpeed || best.speed; }, sk.slowDuration || 2000);
          }
        }
      }

    } else if (sk.type === 'ranged_aoe') {
      const origin = { x: tx, y: ty };
      const radius = sk.aoeRadius || 80;
      for (const t of this.world.players.values()) {
        if (t.id === caster.id || t.dead) continue;
        if (dist(origin, t) <= radius) this.applyDamage(t, baseDmg, caster.id);
      }
      for (const m of this.world.monsters.values()) {
        if (m.hp <= 0) continue;
        if (dist(origin, m) <= radius) this.applyDamageToMonster(m, baseDmg, caster.id);
      }

    } else if (sk.type === 'heal_target') {
      const target = this.world.getPlayer(caster.id);
      if (target && !target.dead)
        target.hp = Math.min(target.maxHp, target.hp + (sk.healAmount || 0));

    } else if (sk.type === 'heal_aoe') {
      const radius = sk.aoeRadius || 150;
      for (const p of this.world.players.values()) {
        if (p.dead) continue;
        if (dist(caster, p) <= radius)
          p.hp = Math.min(p.maxHp, p.hp + (sk.healAmount || 0));
      }

    } else if (sk.type === 'buff_self') {
      if (sk.damageBonus)     caster.damageBonus     = sk.damageBonus;
      if (sk.damageReduction) caster.damageReduction = sk.damageReduction;
      if (sk.dodgeChance)     caster.dodgeChance     = sk.dodgeChance;
      if (sk.shieldAmount)    caster.shield = (caster.shield || 0) + sk.shieldAmount;

      if (sk.duration) setTimeout(() => {
        delete caster.damageBonus;
        delete caster.damageReduction;
        delete caster.dodgeChance;
        if (sk.shieldAmount)
          caster.shield = Math.max(0, (caster.shield || 0) - sk.shieldAmount);
      }, sk.duration);

    } else if (sk.type === 'buff_target') {
      // Shield/buff no aliado mais proximo; fallback: self
      let bestAlly = null, bestAllyD = sk.range || 200;
      for (const p of this.world.players.values()) {
        if (p.id === caster.id || p.dead) continue;
        const d = dist(caster, p);
        if (d < bestAllyD) { bestAlly = p; bestAllyD = d; }
      }
      const buffTarget = bestAlly || caster;
      if (sk.shieldAmount) {
        buffTarget.shield = (buffTarget.shield || 0) + sk.shieldAmount;
        if (sk.duration) setTimeout(() => {
          buffTarget.shield = Math.max(0, (buffTarget.shield || 0) - sk.shieldAmount);
        }, sk.duration);
      }
      if (sk.damageReduction) {
        buffTarget.damageReduction = sk.damageReduction;
        if (sk.duration) setTimeout(() => { delete buffTarget.damageReduction; }, sk.duration);
      }

    } else if (sk.type === 'teleport') {
      // Blink na direcao (tx, ty)
      const dx = tx - caster.x, dy = ty - caster.y;
      const d  = Math.hypot(dx, dy);
      if (d > 0) {
        const step = Math.min(d, sk.range || 200);
        caster.x = Math.max(0, Math.min(MAP_W, caster.x + (dx / d) * step));
        caster.y = Math.max(0, Math.min(MAP_H, caster.y + (dy / d) * step));
      }

    } else if (sk.type === 'melee_charge') {
      // Charge ao alvo mais proximo, dano + knockback
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
          if (sk.knockbackDistance) {
            best.x = Math.max(0, Math.min(MAP_W, best.x + Math.cos(angle) * sk.knockbackDistance));
            best.y = Math.max(0, Math.min(MAP_H, best.y + Math.sin(angle) * sk.knockbackDistance));
          }
        }
      }

    } else if (sk.type === 'melee_dot') {
      // Dano imediato + ticks de DoT
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
        else               this.applyDamage(best, baseDmg, caster.id);

        if (sk.dotDamage && sk.dotTicks && sk.dotInterval) {
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

    } else if (sk.type === 'trap') {
      // Root imediato no inimigo mais proximo da area (tx, ty)
      const origin  = { x: tx, y: ty };
      const radius  = sk.aoeRadius || 60;
      const rootDur = sk.rootDuration || 3000;
      for (const m of this.world.monsters.values()) {
        if (m.hp <= 0 || dist(origin, m) > radius) continue;
        this.applyDamageToMonster(m, baseDmg, caster.id);
        m.rooted      = true;
        m.rootedUntil = Date.now() + rootDur;
        setTimeout(() => { m.rooted = false; }, rootDur);
      }

    } else if (sk.type === 'revive') {
      // Ressuscita aliado morto mais proximo no range
      let deadAlly = null, deadAllyD = sk.range || 100;
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
        this.world.io.to(deadAlly.id).emit('player:revived', { hp: deadAlly.hp });
      }
    }
  }

  getSkillCatalog()              { return ALL_SKILLS; }
  getClassSkills(playerClass)    { return SKILLS[playerClass] || []; }
}

module.exports = CombatEngine;
