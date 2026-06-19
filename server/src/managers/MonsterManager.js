// MonsterManager -- spawn, IA, morte e loot de monstros
const { v4: uuidv4 } = require('uuid');
const {
  MONSTER_AI_TICK_MS, MONSTER_AGGRO_RANGE, MONSTER_LEASH_RANGE,
  MAP_W, MAP_H, LOOT_DESPAWN_MS,
} = require('../config/constants');

const MONSTER_TYPES = {
  goblin: {
    name: 'Goblin', hp: 40, maxHp: 40, speed: 120,
    damage: 8, range: 55, attackCooldown: 1200,
    xpReward: 30, goldReward: [5, 15],
    lootTable: [
      { itemId: 'sword_rusty',  chance: 0.15 },
      { itemId: 'potion_small', chance: 0.30 },
    ],
  },
  orc: {
    name: 'Orc', hp: 80, maxHp: 80, speed: 90,
    damage: 18, range: 65, attackCooldown: 2000,
    xpReward: 70, goldReward: [15, 40],
    lootTable: [
      { itemId: 'axe_iron',      chance: 0.20 },
      { itemId: 'armor_leather', chance: 0.10 },
      { itemId: 'potion_small',  chance: 0.40 },
    ],
  },
  skeleton: {
    name: 'Skeleton', hp: 55, maxHp: 55, speed: 110,
    damage: 12, range: 60, attackCooldown: 1500,
    xpReward: 45, goldReward: [8, 20],
    lootTable: [
      { itemId: 'bow_bone',     chance: 0.12 },
      { itemId: 'potion_small', chance: 0.25 },
    ],
  },
  wolf: {
    name: 'Wolf', hp: 35, maxHp: 35, speed: 160,
    damage: 10, range: 50, attackCooldown: 1000,
    xpReward: 25, goldReward: [3, 10],
    lootTable: [
      { itemId: 'wolf_pelt', chance: 0.50 },
    ],
  },
  troll: {
    name: 'Troll', hp: 200, maxHp: 200, speed: 70,
    damage: 35, range: 80, attackCooldown: 3000,
    xpReward: 200, goldReward: [50, 120],
    lootTable: [
      { itemId: 'club_heavy',   chance: 0.25 },
      { itemId: 'potion_large', chance: 0.30 },
      { itemId: 'troll_hide',   chance: 0.40 },
    ],
  },
};

class MonsterManager {
  constructor(world, combat) {
    this.world  = world;
    this.combat = combat;
    this._aiTimer = 0;
  }

  spawnRandom() {
    const types = Object.keys(MONSTER_TYPES);
    const type  = types[Math.floor(Math.random() * types.length)];
    return this.spawn(type, {
      x: Math.random() * (MAP_W - 200) + 100,
      y: Math.random() * (MAP_H - 200) + 100,
    });
  }

  spawn(type, { x, y }) {
    const def = MONSTER_TYPES[type];
    if (!def) return null;

    const state = {
      id: uuidv4(), type,
      name: def.name,
      x, y, spawnX: x, spawnY: y,
      hp: def.hp, maxHp: def.maxHp,
      speed: def.speed,
      damage: def.damage, range: def.range,
      attackCooldown: def.attackCooldown, lastAttackAt: 0,
      xpReward: def.xpReward, goldReward: def.goldReward, lootTable: def.lootTable,
      target: null,
      state:  'idle',
      lastHitBy: null,
      rooted: false,
      rootedUntil: 0,
    };

    this.world.addMonster(state);
    return state;
  }

  aiTick(now) {
    this._aiTimer += this.world._tickMs;
    if (this._aiTimer < MONSTER_AI_TICK_MS) return;
    this._aiTimer = 0;

    const dist = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);

    for (const m of this.world.monsters.values()) {
      if (m.hp <= 0) {
        this.onMonsterDeath(m, m.lastHitBy || null);
        continue;
      }

      // Limpa root expirado
      if (m.rooted && now >= m.rootedUntil) m.rooted = false;

      // Encontrar alvo
      if (!m.target || m.state === 'idle') {
        let closest = null, closestD = MONSTER_AGGRO_RANGE;
        for (const p of this.world.players.values()) {
          if (p.dead) continue;
          const d = dist(m, p);
          if (d < closestD) { closest = p; closestD = d; }
        }
        if (closest) { m.target = closest.id; m.state = 'aggro'; }
      }

      if (m.state === 'aggro' && m.target) {
        const target = this.world.getPlayer(m.target);

        if (!target || target.dead) {
          m.target = null; m.state = 'returning';
        } else {
          const d = dist(m, target);

          if (dist(m, { x: m.spawnX, y: m.spawnY }) > MONSTER_LEASH_RANGE) {
            m.target = null; m.state = 'returning';
          } else if (d <= m.range) {
            if (!m.rooted && now - m.lastAttackAt >= m.attackCooldown) {
              m.lastAttackAt = now;
              this.combat.applyDamage(target, m.damage, null);
            }
          } else if (!m.rooted) {
            const angle = Math.atan2(target.y - m.y, target.x - m.x);
            const step  = m.speed * (MONSTER_AI_TICK_MS / 1000);
            m.x += Math.cos(angle) * step;
            m.y += Math.sin(angle) * step;
          }
        }
      }

      if (m.state === 'returning') {
        const dx = m.spawnX - m.x, dy = m.spawnY - m.y;
        const d  = Math.hypot(dx, dy);
        if (d < 5) {
          m.x = m.spawnX; m.y = m.spawnY; m.state = 'idle';
          m.hp = m.maxHp;
        } else {
          const step = m.speed * (MONSTER_AI_TICK_MS / 1000) * 1.5;
          m.x += (dx / d) * step;
          m.y += (dy / d) * step;
        }
      }
    }
  }

  onMonsterDeath(monster, killerId) {
    this.world.removeMonster(monster.id);

    if (killerId) {
      const killer = this.world.getPlayer(killerId);
      if (killer) {
        killer.xp += monster.xpReward;
        const goldMin = monster.goldReward[0];
        const goldMax = monster.goldReward[1];
        const gold    = goldMin + Math.floor(Math.random() * (goldMax - goldMin + 1));
        killer.gold  += gold;

        this.world.io.to(killerId).emit('player:xp', {
          xp:        monster.xpReward,
          gold,
          totalXp:   killer.xp,
          totalGold: killer.gold,
          xpMax:     Math.floor(100 * Math.pow(killer.level, 1.5)),
        });

        // Verifica level up
        this.combat.players.checkLevelUp(killer);
      }
    }

    // Loot drop no chao
    for (const entry of monster.lootTable) {
      if (Math.random() < entry.chance) {
        const item = {
          id:   uuidv4(),
          type: entry.itemId,
          x:    monster.x + (Math.random() - 0.5) * 30,
          y:    monster.y + (Math.random() - 0.5) * 30,
        };
        this.world.addItem(item);
        setTimeout(() => this.world.removeItem(item.id), LOOT_DESPAWN_MS);
      }
    }
  }

  getMonsterTypes() { return MONSTER_TYPES; }
}

module.exports = MonsterManager;
