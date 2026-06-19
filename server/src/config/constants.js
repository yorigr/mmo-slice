// ============================================================
// GAME CONSTANTS — fonte única de verdade para balanceamento
// Edite aqui; não espalhe números mágicos pelo código.
// ============================================================

module.exports = {

  // ----- Servidor -----
  TICK_RATE: 20,          // Hz — atualizações por segundo
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

  // ----- Economia -----
  MARKET_TAX_RATE:         0.05,   // 5% taxa sobre vendas no mercado
  REPAIR_COST_RATE:        0.15,   // custo de reparo = valor × (1 - durability/100) × 0.15
  CRAFT_OVERHEAD_RATE:     0.05,   // fee de crafting = valor_item × 0.05 (silver sink)
  CRAFT_FOCUS_MAX:         20000,  // pool máximo de focus de crafting
  CRAFT_FOCUS_REGEN_HOUR:  2000,   // focus regenerado por hora
  CRAFT_FOCUS_EFFICIENCY:  0.8,    // sem focus: 80% eficiência (20% materiais perdidos)
  DURABILITY_LOSS_PER_HIT: 1,
  MAX_DURABILITY:          100,

  // ----- Status Effects — Diminishing Returns -----
  CC_DR_TABLE:  [1.0, 0.5, 0.25, 0],  // índice = nº de aplicações da mesma CC (1-indexed: índice 0 ignorado)
  CC_DR_RESET_MS: 15000,               // reseta DR após 15s sem a CC
};
