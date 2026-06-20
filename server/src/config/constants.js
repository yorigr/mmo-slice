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

  // ----- Guilds -----
  MAX_GUILD_MEMBERS: 50,
  GUILD_CREATION_COST_GOLD: 1000,

  // ----- Performance -----
  MAX_PLAYERS_PER_ZONE: 100,
  STATE_BROADCAST_INTERVAL_MS: 50,  // = 20Hz
  DB_SAVE_INTERVAL_MS: 30000,       // salva estado no DB a cada 30s

  // ----- Economia -----
  MARKET_TAX_RATE:         0.05,   // 5% taxa sobre vendas no mercado
  REPAIR_COST_RATE:        0.15,   // custo de reparo = baseValue x (1 - dur/100) x 0.15
  CRAFT_OVERHEAD_RATE:     0.05,   // fee de crafting = valor_item x 0.05 (silver sink)
  CRAFT_FOCUS_MAX:         20000,  // pool maximo de focus de crafting
  CRAFT_FOCUS_REGEN_HOUR:  2000,   // focus regenerado por hora
  CRAFT_FOCUS_EFFICIENCY:  0.8,    // sem focus: 80% eficiencia (20% materiais perdidos)

  // ----- Durabilidade -----
  MAX_DURABILITY:           100,
  DURABILITY_LOSS_PER_HIT:  1,    // pontos perdidos por hit recebido (em peca aleatoria)
  DURABILITY_DEATH_PENALTY: 30,   // pontos perdidos em CADA peca sobrevivente ao morrer

  // Valor base (gold) de cada peca para calcular custo de reparo.
  // custo = REPAIR_BASE_VALUES[gearId] * (1 - durability/100) * REPAIR_COST_RATE
  REPAIR_BASE_VALUES: {
    // Armas
    sword: 100, greataxe: 150, daggers: 80, mace: 120, hammer: 150,
    bow: 100, fire_staff: 130, frost_staff: 130, arcane_staff: 130, holy_staff: 130,
    // Armaduras (cloth < leather < plate) - IDs conforme gear.json
    cloth_chest: 70,  cloth_hood: 50,   cloth_boots: 50,
    leather_chest: 110, leather_cap: 80, leather_boots: 80,
    plate_chest: 180, plate_helm: 130,  plate_boots: 130,
  },

  // ----- NPC: Ferreiro (posicao fixa no overworld) -----
  BLACKSMITH_X:     1200,   // proximo ao centro do mapa (MAP_W/2)
  BLACKSMITH_Y:      900,   // proximo ao centro do mapa (MAP_H/2)
  BLACKSMITH_RANGE:  120,   // distancia maxima (px) para interagir

  // ----- NPC: Instrutor (converte Fama Amarela pendente em bonus permanentes) -----
  TRAINER_X:    1000,        // noroeste do centro
  TRAINER_Y:     600,
  TRAINER_RANGE: 120,

  // ----- Maestria de Equipamento (Equipment Mastery) -----
  // Inspirado no sistema do Albion Online:
  //   Armas    -> XP por usar skill ofensiva/suporte
  //   Armaduras -> XP por absorver dano com a peca equipada
  // Ao atingir maestria maxima (nivel 10), XP excedente vira "Fama Amarela pendente".
  // Fama Amarela e convertida em bonus permanentes pagando ouro ao Instrutor NPC
  // (sink de ouro progressivamente punitivo, similar ao silver no Albion).
  MASTERY_MAX_LEVEL: 10,
  // XP para avançar do nivel N para N+1. Indice 0 = nivel 1->2 (9 entradas).
  MASTERY_XP_TABLE: [500, 1500, 4000, 10000, 25000, 60000, 140000, 320000, 750000],

  // Bonus por tipo de equipamento por nivel de maestria
  MASTERY_WEAPON_DMG_PER_LEVEL:    0.02,   // armas: +2% dano por nivel
  MASTERY_CLOTH_MANA_PER_LEVEL:    2,      // cloth: +2 maxMana por nivel
  MASTERY_LEATHER_DODGE_PER_LEVEL: 0.003,  // leather: +0.3% dodgeChance por nivel
  MASTERY_PLATE_DR_PER_LEVEL:      0.002,  // plate: +0.2% damageReduction por nivel

  // XP base por evento (escalonado pelos multiplicadores abaixo antes de ser concedido)
  MASTERY_XP_PER_USE: 10,   // base por skill de arma que conecta em monstro
  MASTERY_XP_PER_HIT: 3,    // base por hit de monstro absorvido pela armadura

  // XP de referencia de monstro (= XP_PER_KILL_BASE).
  // weapon mastery XP = MASTERY_XP_PER_USE * (mob.xpReward / MASTERY_BASE_MOB_XP) * zoneMult
  // Exemplo: golem (xpReward=200) em black  → 10 * (200/50) * 1.5 = 60 XP/hit
  //          rato  (xpReward=25)  em safe   → 10 * (25/50)  * 0.0 =  0 XP
  MASTERY_BASE_MOB_XP: 50,

  // Multiplicador de XP de maestria por tipo de zona.
  // safe=0 elimina farming em area segura; black=1.5 recompensa risco maximo.
  // Espelha o bonus de fama do Albion (blue=0%, yellow=+50%, red/black=+100%).
  MASTERY_ZONE_MULT: { safe: 0.0, yellow: 0.5, red: 1.0, black: 1.5 },

  // ----- Fama Amarela (Yellow Fame - pos-maestria maxima) -----
  YELLOW_FAME_MAX_LEVEL: 5,
  // XP pendente necessario para converter cada nivel de Fama Amarela (0-indexed)
  YELLOW_FAME_XP_TABLE:  [200000, 600000, 1500000, 4000000, 10000000],
  // Custo em ouro ao Instrutor para cristalizar cada nivel
  // Total por peca: 500+1500+4000+10000+25000 = 41.000 gold (sink intencional)
  YELLOW_FAME_GOLD_TABLE: [500, 1500, 4000, 10000, 25000],

  // Bonus adicionais por nivel de Fama Amarela (por tipo)
  YELLOW_FAME_WEAPON_DMG_PER_LEVEL:    0.05,   // armas: +5% dano
  YELLOW_FAME_CLOTH_MANA_PER_LEVEL:    3,      // cloth: +3 maxMana
  YELLOW_FAME_LEATHER_DODGE_PER_LEVEL: 0.005,  // leather: +0.5% dodge
  YELLOW_FAME_PLATE_DR_PER_LEVEL:      0.003,  // plate: +0.3% DR

  // ----- Status Effects - Diminishing Returns -----
  CC_DR_TABLE:  [1.0, 0.5, 0.25, 0],  // indice = no de aplicacoes da mesma CC
  CC_DR_RESET_MS: 15000,               // reseta DR apos 15s sem a CC

  // ----- Morte - Destruicao de Itens -----
  // Taxa de destruicao por peca equipada, rolada individualmente.
  // safe: area inicial (sem perda). yellow: overworld PvM. red/black: zonas PvP.
  DEATH_DESTROY_RATES: { safe: 0.00, yellow: 0.25, red: 0.33, black: 0.50 },
  DEATH_RESOURCE_LOSS_RATE: 0.10,   // 10% de cada stack de material destruido na morte
};
