# ⚙️ GAME CONFIGURATION & CONSTANTS

**File Type:** JSON + JavaScript Constants  
**Purpose:** Centralized configuration for all game systems

---

## 📋 CONSTANTS.JS

```javascript
// GAME CONSTANTS & CONFIGURATION
// Update here to balance the entire game

// ============================================================================
// WORLD CONFIGURATION
// ============================================================================

const WORLD_CONFIG = {
  // Map dimensions
  MAP_WIDTH: 100,
  MAP_HEIGHT: 100,
  TILE_SIZE: 32, // pixels
  
  // Camera & Rendering
  RENDER_DISTANCE: 20, // tiles (players beyond this aren't synced)
  TICK_RATE: 20, // Server ticks per second (50ms)
  
  // Zones
  ZONES: {
    TOWN: {
      name: 'Town',
      color: '#FFFFFF', // White (safe)
      pvpEnabled: false,
      bounds: { x: 0, y: 0, w: 30, h: 30 },
      respawnPoint: { x: 15, y: 15 },
    },
    COUNTRYSIDE: {
      name: 'Countryside',
      color: '#FFFF00', // Yellow (moderate risk)
      pvpEnabled: true,
      bounds: { x: 30, y: 0, w: 35, h: 50 },
      respawnPoint: { x: 15, y: 15 }, // Revive in Town anyway
    },
    DARK_FOREST: {
      name: 'Dark Forest',
      color: '#FF0000', // Red (hardcore)
      pvpEnabled: true,
      bounds: { x: 65, y: 0, w: 35, h: 50 },
      respawnPoint: { x: 15, y: 15 },
    },
  },

  // Player defaults
  DEFAULT_MOVEMENT_SPEED: 4, // tiles per second
  MAX_MOVEMENT_SPEED: 6, // tiles per second (sprint)
};

// ============================================================================
// COMBAT CONFIGURATION
// ============================================================================

const COMBAT_CONFIG = {
  // Synchronization & Validation
  COMBAT_TICK_MS: 50, // 50ms per combat tick (20 Hz)
  SKILL_VALIDATION_LATENCY: 100, // 100ms server latency compensation
  
  // Damage & Defense
  BASE_ARMOR_DAMAGE_REDUCTION: 0.01, // 1% per armor point
  CRITICAL_STRIKE_MULTIPLIER: 1.5, // 50% bonus on crit
  
  // Status Effects
  STATUS_EFFECTS: {
    STUN: { canMove: false, canCast: false, duration: 1500 },
    SLOW: { speedMultiplier: 0.5, duration: 3000 },
    BURN: { tickDamage: 5, tickInterval: 1000, duration: 6000 },
    BLEED: { tickDamage: 3, tickInterval: 1000, duration: 5000 },
    SHIELD: { absorbDamage: true, duration: 3000 },
    REGEN: { healPerTick: 10, tickInterval: 1000, duration: 5000 },
    IMMUNITY: { invulnerable: true, duration: 2000 },
    SLOW_CAST: { castTimeMultiplier: 1.5, duration: 3000 },
  },
  
  // Casting
  GLOBAL_CAST_COOLDOWN: 500, // minimum 500ms between any action
  
  // Death & Respawn
  DEATH_ANIMATION_DURATION: 3000, // 3s ragdoll
  RESPAWN_DELAY: 5000, // 5s to revive
  RESPAWN_LOCATION: 'TOWN_CENTER',
  
  // Loot on Death
  DEATH_LOOT_TIMEOUT: 300000, // 5 minutes before loot despawns
  KILLER_LOOT_EXCLUSIVITY: 10000, // 10s only killer can loot
};

// ============================================================================
// SKILL CONFIGURATION
// ============================================================================

const SKILL_CONFIG = {
  // Skill system
  HOTBAR_SIZE: 5, // 5 skills per hotbar
  MAX_HOTBARS: 2, // Can swap hotbars (Q/E)
  
  // Mana
  MANA_REGEN_PER_SECOND: {
    warrior: 10,
    mage: 20,
    ranger: 5,
    healer: 15,
    bruiser: 12,
  },
  
  // Stamina (Ranger)
  STAMINA_REGEN_PER_SECOND: {
    ranger: 30, // Regenerates fast
  },
  SPRINT_DRAIN_PER_SECOND: {
    ranger: 20, // Drains stamina while sprinting
  },
  
  // Class-specific modifiers
  CLASS_MODIFIERS: {
    warrior: { damage: 1.1, armor: 1.15, health: 1.2 },
    mage: { damage: 0.95, armor: 0.6, health: 0.8, mana: 1.3 },
    ranger: { damage: 1.0, armor: 0.7, health: 0.9, stamina: 1.2 },
    healer: { damage: 0.7, armor: 0.8, health: 1.0, mana: 1.2 },
    bruiser: { damage: 1.05, armor: 1.0, health: 1.1 },
  },
};

// ============================================================================
// ECONOMY & LOOT CONFIGURATION
// ============================================================================

const ECONOMY_CONFIG = {
  // Item durability
  DURABILITY_LOSS_PER_HIT: 0.5, // 0.5% durability per hit taken
  DURABILITY_LOSS_ON_DEATH: 30, // 30% durability loss when dying
  REPAIR_COST_MULTIPLIER: 0.3, // Costs 30% of item value to repair
  
  // Item rarity values (base gold)
  ITEM_VALUE: {
    common: 10,
    uncommon: 50,
    rare: 200,
    epic: 800,
    legendary: 3000,
  },
  
  // Loot drops
  MONSTER_LOOT_CHANCE: 0.7, // 70% chance to drop loot
  BOSS_LOOT_CHANCE: 1.0, // Always drops
  GOLD_DROP_RANGE: {
    goblin: { min: 10, max: 50 },
    wolf: { min: 30, max: 100 },
    troll: { min: 100, max: 300 },
    boss: { min: 500, max: 2000 },
  },
  
  // Energy system (anti-RMT)
  ENERGY_MAX: 100,
  ENERGY_PER_KILL: 10, // 10 energy per kill
  ENERGY_REGEN_PER_MINUTE: 1, // 1 energy per minute (60 energy per hour)
  FARM_LIMIT_WITH_ZERO_ENERGY: 10, // Can still get 10 kills at 0 energy
  
  // Inflation control
  GOLD_SINK_MECHANIC: 'REPAIR_COST', // Gold leaves economy via repairs
  ECONOMY_RESET_INTERVAL: null, // null = no reset
};

// ============================================================================
// PLAYER PROGRESSION
// ============================================================================

const PROGRESSION_CONFIG = {
  // Health & Mana by class
  BASE_HEALTH: {
    warrior: 150,
    mage: 80,
    ranger: 100,
    healer: 120,
    bruiser: 130,
  },
  
  BASE_MANA: {
    warrior: 100,
    mage: 150,
    ranger: 50, // stamina instead
    healer: 120,
    bruiser: 100,
  },
  
  // Leveling (future)
  LEVEL_UP_THRESHOLD: 1000, // experience points to level up
  
  // Skills (equipment-based progression)
  SKILL_PROGRESSION: {
    swordSkill: { weaponType: 'sword', maxLevel: 100 },
    magicSkill: { weaponType: 'staff', maxLevel: 100 },
    arrowSkill: { weaponType: 'bow', maxLevel: 100 },
    healingSkill: { weaponType: 'healing', maxLevel: 100 },
    defenseSkill: { armorType: 'plate', maxLevel: 100 },
  },
};

// ============================================================================
// GUILD CONFIGURATION
// ============================================================================

const GUILD_CONFIG = {
  // Guild creation
  MIN_GUILD_NAME_LENGTH: 3,
  MAX_GUILD_NAME_LENGTH: 20,
  MIN_GUILD_TAG_LENGTH: 2,
  MAX_GUILD_TAG_LENGTH: 4,
  
  // Members
  MAX_GUILD_SIZE: 100,
  MAX_OFFICERS: 3,
  
  // Guild bank
  GUILD_BANK_SLOTS: 100,
  GUILD_BANK_TAX: 0.05, // 5% tax on stored items (value)
  
  // Permissions
  GUILD_RANKS: ['member', 'officer', 'master'],
  
  // Guild war (future)
  GUILD_WAR_DURATION: 86400000, // 24 hours
  GUILD_WAR_COOLDOWN: 604800000, // 1 week between wars
};

// ============================================================================
// DUNGEON CONFIGURATION
// ============================================================================

const DUNGEON_CONFIG = {
  // Goblin Caves (MVP)
  GOBLIN_CAVES: {
    name: 'Goblin Caves',
    minLevel: 1,
    maxPlayers: 5,
    duration: 1800000, // 30 min timeout
    resetCooldown: 3600000, // 1 hour cooldown
    
    // Spawns
    trash: [
      { type: 'goblin', count: 5, level: 1 },
      { type: 'goblin_scout', count: 2, level: 1 },
    ],
    
    boss: {
      name: 'Goblin King',
      health: 200,
      damage: 20,
      armor: 10,
      loot: [
        { itemId: 'green_dagger', rarity: 'uncommon', chance: 0.8 },
        { itemId: 'goblin_ring', rarity: 'rare', chance: 0.3 },
      ],
      goldReward: 200,
    },
    
    // Loot distribution
    LOOT_DISTRIBUTION: 'ROLL', // 'ROLL' | 'ROUND_ROBIN' | 'FREE_FOR_ALL'
  },
};

// ============================================================================
// MONSTER CONFIGURATION
// ============================================================================

const MONSTER_CONFIG = {
  // Spawn settings
  DEFAULT_SPAWN_DELAY: 30000, // 30 seconds to respawn
  SPAWN_ANIMATION_DURATION: 1000, // 1 second appear animation
  
  // Monster types
  MONSTERS: {
    goblin: {
      name: 'Goblin',
      health: 50,
      damage: 10,
      armor: 5,
      speed: 3,
      range: 1,
      aggroRange: 8,
      attackCooldown: 1500,
      drops: { goldMin: 10, goldMax: 30 },
      loot: [
        { itemId: 'cloth_scrap', chance: 0.4 },
      ],
    },
    wolf: {
      name: 'Wolf',
      health: 80,
      damage: 15,
      armor: 8,
      speed: 4,
      range: 1,
      aggroRange: 10,
      attackCooldown: 1200,
      drops: { goldMin: 30, goldMax: 80 },
      loot: [
        { itemId: 'wolf_fang', chance: 0.6 },
      ],
    },
    troll: {
      name: 'Troll',
      health: 150,
      damage: 25,
      armor: 15,
      speed: 2.5,
      range: 2,
      aggroRange: 12,
      attackCooldown: 2000,
      drops: { goldMin: 100, goldMax: 250 },
      loot: [
        { itemId: 'troll_bone', chance: 0.5 },
      ],
    },
  },
};

// ============================================================================
// CHAT CONFIGURATION
// ============================================================================

const CHAT_CONFIG = {
  // Rate limiting
  MAX_MESSAGES_PER_10_SECONDS: 5,
  MUTE_DURATION_MS: 600000, // 10 minutes auto-mute
  
  // Chat channels
  GLOBAL_CHAT_RADIUS: 20, // tiles (players beyond this don't see chat)
  
  // Moderation
  BANNED_WORDS: [], // Empty for MVP (add later)
  SPAM_FILTER: true,
};

// ============================================================================
// PERFORMANCE TARGETS
// ============================================================================

const PERFORMANCE_CONFIG = {
  // Network
  MAX_PLAYERS_PER_SERVER: 100,
  BANDWIDTH_PER_PLAYER_KB_S: 0.1, // 100 bytes/sec per player
  
  // Memory
  MEMORY_PER_PLAYER_MB: 2, // Rough estimate
  MAX_MEMORY_GB: 4, // Max memory before issues
  
  // CPU
  MAX_CPU_USAGE_PERCENT: 70, // Alert if > 70%
  
  // Database
  SAVE_INTERVAL: 30000, // Save player data every 30 seconds
  BACKUP_INTERVAL: 3600000, // Backup every 1 hour
};

// ============================================================================
// SECURITY CONFIGURATION
// ============================================================================

const SECURITY_CONFIG = {
  // Anti-exploit
  MAX_SPEED_THRESHOLD: 8, // tiles per second (kick if faster)
  MAX_POSITION_DELTA: 10, // max tile distance per tick (kick if larger)
  
  // Cooldown validation
  COOLDOWN_GRACE_PERIOD: 50, // 50ms grace for network latency
  
  // Ban system
  BAN_DURATION: {
    first_exploit: 0, // Warn only
    second_exploit: 86400000, // 24 hours
    third_exploit: null, // Permanent
  },
  
  // Rate limiting
  RATE_LIMIT: {
    login_attempts: 5, // max per minute
    login_lockout_duration: 300000, // 5 minutes
  },
};

// ============================================================================
// DEBUGGING & TESTING
// ============================================================================

const DEBUG_CONFIG = {
  // Enable/disable features
  ENABLE_GODMODE: false, // Invulnerable players
  ENABLE_INSTANT_KILLS: false, // Instant kill mode
  ENABLE_INFINITE_MANA: false, // Unlimited mana
  
  // Logging
  LOG_LEVEL: 'INFO', // 'DEBUG' | 'INFO' | 'WARN' | 'ERROR'
  LOG_COMBAT: true, // Log all combat events
  LOG_LOOT: true, // Log all loot drops
  LOG_PVP: true, // Log PvP kills
  
  // Auto-testing
  BOT_PLAYERS_COUNT: 0, // Number of AI bots to spawn (testing)
  BOT_BEHAVIOR: 'RANDOM', // 'RANDOM' | 'AGGRESSIVE' | 'PASSIVE'
};

// ============================================================================
// EXPORT
// ============================================================================

module.exports = {
  WORLD_CONFIG,
  COMBAT_CONFIG,
  SKILL_CONFIG,
  ECONOMY_CONFIG,
  PROGRESSION_CONFIG,
  GUILD_CONFIG,
  DUNGEON_CONFIG,
  MONSTER_CONFIG,
  CHAT_CONFIG,
  PERFORMANCE_CONFIG,
  SECURITY_CONFIG,
  DEBUG_CONFIG,
};
```

---

## 📄 .ENV.EXAMPLE

```bash
# Server Configuration
NODE_ENV=development
PORT=3000
HOST=localhost

# Database
DATABASE_URL=sqlite:./game.db
# Or for MongoDB:
# DATABASE_URL=mongodb://localhost:27017/mmorpg

# Security
JWT_SECRET=your_secret_key_change_this
SESSION_TIMEOUT=3600000

# Game Balance (can override constants)
GAME_DIFFICULTY=1.0
LOOT_DROP_RATE=1.0
GOLD_MULTIPLIER=1.0

# Debugging
DEBUG=game:*
LOG_LEVEL=info

# Features (enable/disable)
ENABLE_PVP=true
ENABLE_GUILDS=true
ENABLE_DUNGEONS=true

# Deployment
REDIS_URL=redis://localhost:6379
SENTRY_DSN=your_error_tracking_url
```

---

## 📊 BALANCE SPREADSHEET TEMPLATE

```
Use this to track balance changes:

| Class | Skill | Damage | Cooldown | Mana | Scaling | Notes |
|-------|-------|--------|----------|------|---------|-------|
| Warrior | Slash | 25 | 1000 | 0 | 1.0 | Baseline |
| Warrior | Shield Bash | 30 | 3000 | 20 | 0.8 | Stun 1.5s |
| Warrior | Charge | 40 | 5000 | 30 | 1.0 | Gap closer |
| Mage | Fireball | 60 | 4000 | 50 | 1.2 | AOE 3x3 |
| Mage | Frost Bolt | 40 | 2500 | 35 | 1.0 | Slow 60% |
| ... | ... | ... | ... | ... | ... | ... |
```

---

## 🔄 CONFIGURATION OVERRIDE HIERARCHY

```
Precedence (highest to lowest):
1. Runtime arguments (CLI flags)
2. Environment variables (.env)
3. Constants.js
4. Defaults in code
```

---

## 📝 HOW TO USE

### In Node.js Code:
```javascript
const {
  WORLD_CONFIG,
  COMBAT_CONFIG,
  SKILL_CONFIG,
} = require('./constants');

// Use in code
const MAX_PLAYERS = WORLD_CONFIG.MAP_WIDTH * WORLD_CONFIG.MAP_HEIGHT;
const TICK_RATE = WORLD_CONFIG.TICK_RATE;

// Easy to balance
const manaRegen = SKILL_CONFIG.MANA_REGEN_PER_SECOND[playerClass];
```

### Easy Balancing:
```javascript
// To buff Mage damage:
// In SKILL_CONFIG.CLASS_MODIFIERS.mage
// Change: damage: 0.95 → damage: 1.05
// Restart server - all Mages get 5% damage boost

// To nerf Warrior armor:
// In SKILL_CONFIG.CLASS_MODIFIERS.warrior
// Change: armor: 1.15 → armor: 1.05
// Restart server - nerf applied
```

---

**This configuration system allows you to balance the game without code changes!** 🎮
