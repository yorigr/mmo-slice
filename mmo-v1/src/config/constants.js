// ============================================================
// GAME CONSTANTS — fonte única de verdade para balanceamento
// Edite aqui; não espalhe números mágicos pelo código.
// ============================================================

module.exports = {

  // ----- Servidor -----
  TIAK_RATE: 20,          // Hz — atualizações por segundo
  PORT: process.env.PORT || 3000,

  // ----- Mundo -----
  MAP_W: 2400,            // largura do mapa em pixels
  MAP_H: 1800,            // altura do mapa em pixels
  ZONE_SAFE_RADIUS: 400,  // raio da zona segura (início)

  // ----- Player -----
  MAX_HP: 100,
  MAX_MANA: 100,
  MAX_STAMINA: 100,
  MANA_REGEN_PER_SEC: 12,
  STAMINA_REGEN_PER_SEC: 15,
  MAX_SPEED: 200,         // px/s
  RESPAWN_MS: 3000,
  BASE_LEVEL: 1,
  MAX_LEVEL: 60,

  // ----- Progressão -----
  XP_PER_KILL_BASE: 50,
  XP_CURVE_EXPONENT: 1.5, // xp_needed = 100 * level ^ 1.5

  // ----- Combate -----
  MELEE_RANGE_BASE: 60,   // px
  RANGED_RANGE_BASE: 320, // px
  CRIT_CHANCE_BASE: 0.05, // 5%
  CRIT_MULTIPLIER: 1.5,
  DODGE_CHANCE_BASE: 0.03,

  // ----- Monstros -----
  MONSTER_SPAWN_INTERVAL_MS: 5000,
  MONSTER_DESPAWN_IDLE_MS: 30000,
  MONSTER_AI_TICK_MS: 500,
  MONSTER_AGGRO_RANGE: 200,
  MONSTER_LEASH_RANGE: 600,

  // ----- Loot -----
  BASE_LOOT_CHANCE: 0.6,  // 60% de chance de dropar algo
  LOOT_DESPAWN_MS: 60000, // 60s para o item desaparecer do chão
  MAX_INVENTORY_SLOTS: 30,
  DURABILITY_LOSS_PER_HIT: 1,
  MAX_DURABILITY: 100,

  // ----- Guilds -----
  MAX_GUILD_MEMBERS: 50,
  GUILD_CREATION_COST_GOLD: 1000,

  // ----- Performance -----
  MAX_PLAYERS_PER_ZONE: 100,
  STATE_BROADCAST_INTERVAL_MS: 50,  // = 20Hz
  DB_SAVE_INTERVAL_MS: 30000,       // salva estado no DB a cada 30s

  // ----- Modificadores de Classe -----
  CLASS_MODIFIERS: {
    warrior: { hp: 1.3,  mana: 0.8,  speed: 0.9,  damage: 1.1 },
    mage:    { hp: 0.8,  mana: 1.5,  speed: 1.0,  damage: 1.3 },
    ranger:  { hp: 0.9,  mana: 1.0,  speed: 1.2,  damage: 1.0 },
    healer:  { hp: 1.0,  mana: 1.4,  speed: 1.0,  damage: 0.7 },
    bruiser: { hp: 1.2,  mana: 0.9,  speed: 0.95, damage: 1.05 },
  },
};
