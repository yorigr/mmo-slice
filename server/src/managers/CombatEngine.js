// CombatEngine — toda a lógica de combate (PvP e PvM)
//
// v2 — Bug fix crítico: _resolveAbility agora acerta MONSTROS também.
// Anteriormente só iterava world.players — jogadores não podiam matar monstros.
//
// Mudanças:
//   - applyDamageToMonster() — aplica dano a monstros, rastreia lastHitBy para XP
//   - _resolveAbility() — melee / ranged / ranged_aoe acertam players E monstros
//   - setMonsterManager() — injeta referência para callbacks de morte/loot
//
const SKILLS = require('../config/skills.json');
const { CRIT_CHANCE_BASE, CRIT_MULTIPLIER } = require('../config/constants');

// Flatten: id → skill definition
const ALL_SKILLS = {};
for (const classSkills of Object.values(SKILLS)) {
  for (const sk of classSkills) ALL_SKILLS[sk.id] = sk;
}

class CombatEngine {
  constructor(world, playerManager) {
    this.world       = world;
    this.players     = playerManager;
    this._monsterMgr = null; // injetado via setMonsterManager()
  }

  /** Injeta referência ao MonsterManager (para disparar morte/loot). */
  setMonsterManager(mm) { this._monsterMgr = mm; }

  // ----- Iniciar cast -----
  startCast(casterId, abilityId, tx, ty, now = Date.now()) {
    const caster = this.world.getPlayer(casterId);
    if (!caster || caster.dead) return { rejected: 'dead' };

    const sk = ALL_SKILLS[abilityId];
    if (!sk) return { rejected: 'unknown_ability' };
    if (caster.casting) return { rejected: 'already_casting' };
    if ((caster.cooldowns[abilityId] || 0) > now) return { rejected: 'cooldown' };
    if (sk.mana    && caster.mana    < sk.mana)    return { rejected: 'no_mana' };
    if (sk.stamina && caster.stamina < sk.stamina) return { rejected: 'no_stamina' };

    // Consumir recursos no início do cast
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

  // ----- Aplicar dano a um PLAYER -----
  applyDamage(target, amount, attackerId = null) {
    if (!target || target.dead) return;

    // Dodge
    if (Math.random() < (target.dodgeChance || 0)) {
      this.world.io.to(target.id).emit('combat:dodge', { defenderId: target.id });
      return;
    }

    // Crit
    let finalDmg = amount;
    let isCrit   = false;
    if (Math.random() < CRIT_CHANCE_BASE) {
      finalDmg = Math.round(amount * CRIT_MULTIPLIER);
      isCrit   = true;
    }

    // Damage reduction buff
    if (target.damageReduction)
      finalDmg = Math.round(finalDmg * (1 - target.damageReduction));

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

    // Morte do player
    if (target.hp <= 0 && !target.dead) {
      target.dead    = true;
      target.casting = null;
      this.world.emitDeath({ id: target.id });
      this.players.scheduleRespawn(target);
    }
  }

  // ----- Aplicar dano a um MONSTRO -----
  // v2: novo método. Rastreia lastHitBy para atribuir XP na morte.
  applyDamageToMonster(monster, amount, attackerId = null) {
    if (!monster || monster.hp <= 0) return;

    // Dano bruto (sem dodge/crit para monstros por ora — extensível depois)
    const finalDmg = Math.max(1, Math.round(amount));
    monster.hp = Math.max(0, monster.hp - finalDmg);

    // Rastreia quem acertou por último (para XP na morte)
    if (attackerId) monster.lastHitBy = attackerId;

    this.world.emitHit({
      from: attackerId,
      to: monster.id,
      damage: finalDmg,
      crit: false,
      hp: monster.hp,
      isMonster: true,
    });

    // Morte é detectada em MonsterManager.aiTick() para evitar remoção duplicada
    // (o monstro pode levar vários hits no mesmo tick; removemos na próxima iteração de IA)
  }

  // ----- Resolver efeito de uma ability -----
  _resolveAbility(caster, sk, tx, ty) {
    const dist = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);

    // Bônus de dano por buff (ex: War Cry)
    const dmgMult = caster.damageBonus || 1;
    const baseDmg = Math.round((sk.damage || 0) * dmgMult);

    if (sk.type === 'melee') {
      // Acerta players
      for (const t of this.world.players.values()) {
        if (t.id === caster.id || t.dead) continue;
        if (dist(caster, t) <= sk.range) this.applyDamage(t, baseDmg, caster.id);
      }
      // Acerta monstros — BUG FIX v2
      for (const m of this.world.monsters.values()) {
        if (m.hp <= 0) continue;
        if (dist(caster, m) <= sk.range) this.applyDamageToMonster(m, baseDmg, caster.id);
      }

    } else if (sk.type === 'ranged') {
      // Alvo mais próximo — players OU monstros
      let best = null, bestD = Infinity, bestIsMonster = false;

      for (const t of this.world.players.values()) {
        if (t.id === caster.id || t.dead) continue;
        const d = dist(caster, t);
        if (d <= sk.range && d < bestD) { best = t; bestD = d; bestIsMonster = false; }
      }
      // BUG FIX v2: inclui monstros no targeting
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

      // Players em AoE
      for (const t of this.world.players.values()) {
        if (t.id === caster.id || t.dead) continue;
        if (dist(origin, t) <= radius) this.applyDamage(t, baseDmg, caster.id);
      }
      // Monstros em AoE — BUG FIX v2
      for (const m of this.world.monsters.values()) {
        if (m.hp <= 0) continue;
        if (dist(origin, m) <= radius) this.applyDamageToMonster(m, baseDmg, caster.id);
      }

    } else if (sk.type === 'heal_target') {
      // Self-heal por ora; estendível com seleção de alvo
      const target = this.world.getPlayer(caster.id);
      if (target && !target.dead)
        target.hp = Math.min(target.maxHp, target.hp + (sk.healAmount || 0));

    } else if (sk.type === 'heal_aoe') {
      // Cura todos os aliados próximos (healer group heal)
      const radius = sk.aoeRadius || 150;
      for (const p of this.world.players.values()) {
        if (p.dead) continue;
        if (dist(caster, p) <= radius)
          p.hp = Math.min(p.maxHp, p.hp + (sk.healAmount || 0));
      }

    } else if (sk.type === 'buff_self') {
      Object.assign(caster, {
        damageBonus:     sk.damageBonus,
        damageReduction: sk.damageReduction,
        dodgeChance:     sk.dodgeChance,
      });
      if (sk.duration) setTimeout(() => {
        delete caster.damageBonus;
        delete caster.damageReduction;
        delete caster.dodgeChance;
      }, sk.duration);
    }
    // TODO: teleport (blink), trap, revive, melee_charge, melee_dot, buff_target
  }

  getSkillCatalog() { return ALL_SKILLS; }
}

module.exports = CombatEngine;
