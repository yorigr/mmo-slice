// CombatEngine — toda a lógica de combate do Phase 1
// Aproveita as lições do mmo-slice: mana no início do cast, interrupção por dano, funções puras.
const SKILLS = require('../config/skills.json');
const { CRIT_CHANCE_BASE, CRIT_MULTIPLIER } = require('../config/constants');

// Flatten: id → skill definition
const ALL_SKILLS = {};
for (const classSkills of Object.values(SKILLS)) {
  for (const sk of classSkills) ALL_SKILLS[sk.id] = sk;
}

class CombatEngine {
  constructor(world, playerManager) {
    this.world   = world;
    this.players = playerManager;
  }

  // ----- Iniciar cast -----
  startCast(casterId, abilityId, tx, ty, now = Date.now()) {
    const caster = this.world.getPlayer(casterId);
    if (!caster || caster.dead) return { rejected: 'dead' };

    const sk = ALL_SKILLS[abilityId];
    if (!sk) return { rejected: 'unknown_ability' };
    if (caster.casting) return { rejected: 'already_casting' };
    if ((caster.cooldowns[abilityId] || 0) > now) return { rejected: 'cooldown' };
    if (sk.mana && caster.mana < sk.mana) return { rejected: 'no_mana' };
    if (sk.stamina && caster.stamina < sk.stamina) return { rejected: 'no_stamina' };

    // Consumir recursos no início
    if (sk.mana) caster.mana -= sk.mana;
    if (sk.stamina) caster.stamina -= sk.stamina;
    caster.cooldowns[abilityId] = now + sk.cooldown;

    if (sk.castTime === 0) {
      this._resolveAbility(caster, sk, tx, ty);
      return { resolved: true };
    }

    caster.casting = { abilityId, endsAt: now + sk.castTime, total: sk.castTime, tx, ty };
    return { casting: true, endsAt: caster.casting.endsAt };
  }

  // ----- Resolver casts que terminaram -----
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

  // ----- Aplicar dano (único ponto de entrada) -----
  applyDamage(target, amount, attackerId = null) {
    if (!target || target.dead) return;

    // Dodge
    if (Math.random() < (target.dodgeChance || 0)) {
      this.world.io.to(target.id).emit('combat:dodge', { defenderId: target.id });
      return;
    }

    // Crit
    let finalDmg = amount;
    let isCrit = false;
    if (Math.random() < CRIT_CHANCE_BASE) { finalDmg = Math.round(amount * CRIT_MULTIPLIER); isCrit = true; }

    // Damage reduction buff
    if (target.damageReduction) finalDmg = Math.round(finalDmg * (1 - target.damageReduction));

    target.hp = Math.max(0, target.hp - finalDmg);

    // Interrupt cast
    if (target.casting) {
      const sk = ALL_SKILLS[target.casting.abilityId];
      if (sk && sk.interruptible) {
        target.casting = null;
        this.world.emitInterrupt({ id: target.id, ability: sk.name });
      }
    }

    this.world.emitHit({ from: attackerId, to: target.id, damage: finalDmg, crit: isCrit, hp: target.hp });

    // Morte
    if (target.hp <= 0 && !target.dead) {
      target.dead = true;
      target.casting = null;
      this.world.emitDeath({ id: target.id });
      this.players.scheduleRespawn(target);
    }
  }

  // ----- Resolver efeito de uma ability -----
  _resolveAbility(caster, sk, tx, ty) {
    const dist = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);

    if (sk.type === 'melee') {
      for (const t of this.world.players.values()) {
        if (t.id === caster.id || t.dead) continue;
        if (dist(caster, t) <= sk.range) this.applyDamage(t, sk.damage, caster.id);
      }
    } else if (sk.type === 'ranged') {
      let best = null, bestD = Infinity;
      for (const t of this.world.players.values()) {
        if (t.id === caster.id || t.dead) continue;
        const d = dist(caster, t);
        if (d <= sk.range && d < bestD) { best = t; bestD = d; }
      }
      if (best) {
        this.applyDamage(best, sk.damage, caster.id);
        if (sk.slowEffect) {
          best.speed = Math.round(best.speed * (1 - sk.slowEffect));
          setTimeout(() => { best.speed = best.maxSpeed || best.speed; }, sk.slowDuration || 2000);
        }
      }
    } else if (sk.type === 'ranged_aoe') {
      const origin = { x: tx, y: ty };
      for (const t of this.world.players.values()) {
        if (t.id === caster.id || t.dead) continue;
        if (dist(origin, t) <= (sk.aoeRadius || 80)) this.applyDamage(t, sk.damage, caster.id);
      }
    } else if (sk.type === 'heal_target') {
      const target = this.world.getPlayer(caster.id); // self for now; extend with target selection
      if (target && !target.dead) target.hp = Math.min(target.maxHp, target.hp + sk.healAmount);
    } else if (sk.type === 'buff_self') {
      Object.assign(caster, {
        damageBonus: sk.damageBonus,
        damageReduction: sk.damageReduction,
        dodgeChance: sk.dodgeChance,
      });
      if (sk.duration) setTimeout(() => {
        delete caster.damageBonus; delete caster.damageReduction; delete caster.dodgeChance;
      }, sk.duration);
    }
    // TODO: teleport, trap, revive, melee_charge, melee_dot
  }

  getSkillCatalog() { return ALL_SKILLS; }
}

module.exports = CombatEngine;
