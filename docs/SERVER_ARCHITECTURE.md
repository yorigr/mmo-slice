# 🏗️ MMORPG Server Architecture

**Versão:** 0.1  
**Status:** Design Phase  
**Stack:** Node.js + Express + Socket.io + MongoDB/SQLite

---

## 📐 SYSTEM ARCHITECTURE

```
┌─────────────────────────────────────────────────────────────┐
│                        CLIENT (React)                       │
│  - WebGL Renderer (Phaser 3 / Babylon.js)                   │
│  - Input Handler (WASD, Skills, UI)                         │
│  - Client-side prediction (visual only)                     │
└────────────────────────┬────────────────────────────────────┘
                         │
                    WebSocket (Socket.io)
                         │
┌────────────────────────▼────────────────────────────────────┐
│                   SERVER (Node.js)                          │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │            GAME WORLD STATE                           │   │
│  │  - Player Manager                                    │   │
│  │  - Monster Manager                                   │   │
│  │  - Zone Manager                                      │   │
│  │  - Item Manager                                      │   │
│  └──────────────────────────────────────────────────────┘   │
│                          │                                   │
│  ┌──────────────────────────────────────────────────────┐   │
│  │         GAME LOGIC & VALIDATION                       │   │
│  │  - Combat Engine (damage calc, validation)           │   │
│  │  - Skill System (cooldown, mana, casting)            │   │
│  │  - Loot System (drop, pickup, durability)            │   │
│  │  - Guild System (members, chat, war)                 │   │
│  │  - Chat System (global, whisper, guild)              │   │
│  └──────────────────────────────────────────────────────┘   │
│                          │                                   │
│  ┌──────────────────────────────────────────────────────┐   │
│  │         PERSISTENCE LAYER                             │   │
│  │  - Database (MongoDB / SQLite)                        │   │
│  │  - Player profiles, inventory, stats                 │   │
│  │  - Guild data, logs, economy data                    │   │
│  └──────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────┘
```

---

## 📁 PROJECT STRUCTURE

```
mmorpg-server/
├── src/
│   ├── server.js                 # Entry point, Express setup
│   ├── socket-handler.js         # Socket.io events
│   │
│   ├── managers/
│   │   ├── WorldManager.js       # Coordinates all game state
│   │   ├── PlayerManager.js      # Player spawn, movement, sync
│   │   ├── CombatEngine.js       # Damage calc, skill validation
│   │   ├── SkillSystem.js        # Cooldowns, mana, casting
│   │   ├── LootSystem.js         # Item drops, pickup
│   │   ├── GuildSystem.js        # Guild creation, members
│   │   ├── ChatSystem.js         # Global, whisper, guild chat
│   │   ├── MonsterManager.js     # Spawning, AI, drops
│   │   └── ZoneManager.js        # Red/Yellow/White zones
│   │
│   ├── models/
│   │   ├── Player.js             # Player schema
│   │   ├── Monster.js            # Monster schema
│   │   ├── Item.js               # Item schema
│   │   ├── Guild.js              # Guild schema
│   │   └── World.js              # World state
│   │
│   ├── utils/
│   │   ├── constants.js          # Game constants (damage, cooldowns)
│   │   ├── validators.js         # Input validation
│   │   ├── helpers.js            # Utility functions
│   │   └── logger.js             # Logging
│   │
│   └── config/
│       ├── database.js           # DB connection
│       ├── skills.json           # Skill definitions
│       └── monsters.json         # Monster definitions
│
├── tests/
│   ├── combat.test.js            # Combat validation tests
│   ├── skills.test.js            # Skill system tests
│   └── loot.test.js              # Loot drop tests
│
├── package.json
├── .env.example
└── README.md
```

---

## 🔌 SOCKET.IO EVENTS

### **CLIENT → SERVER**

#### Movement
```javascript
socket.emit('player:move', {
  x: number,
  y: number,
  direction: 'up' | 'down' | 'left' | 'right'
})
```

#### Skills
```javascript
socket.emit('skill:use', {
  skillId: string,        // 'slash', 'fireball', etc
  targetId: string | null // player/monster ID or null (self-cast)
})
```

#### Interaction
```javascript
socket.emit('interact:npc', {
  npcId: string,
  action: 'talk' | 'trade' | 'bank'
})

socket.emit('item:pickup', {
  itemId: string,
  x: number,
  y: number
})
```

#### Chat
```javascript
socket.emit('chat:send', {
  channel: 'global' | 'whisper' | 'guild',
  message: string,
  targetPlayer?: string // só pra whisper
})
```

#### Guild
```javascript
socket.emit('guild:create', {
  name: string,
  tag: string
})

socket.emit('guild:invite', {
  playerId: string
})
```

---

### **SERVER → CLIENT**

#### World State (broadcast)
```javascript
socket.emit('world:update', {
  players: [
    {
      id: string,
      name: string,
      x: number,
      y: number,
      class: string,
      health: number,
      maxHealth: number,
      equipment: object,
      inCombat: boolean
    }
  ],
  monsters: [
    {
      id: string,
      type: string,
      x: number,
      y: number,
      health: number,
      maxHealth: number
    }
  ],
  items: [
    {
      id: string,
      type: string,
      x: number,
      y: number,
      durability: number
    }
  ]
})
```

#### Skill Hit Confirmation
```javascript
socket.emit('skill:hit', {
  skillId: string,
  targetId: string,
  damage: number,
  hitType: 'hit' | 'miss' | 'crit',
  effects: [
    {
      type: 'stun' | 'slow' | 'burn',
      duration: number
    }
  ]
})
```

#### Death
```javascript
socket.emit('player:death', {
  playerId: string,
  killerId: string | null,
  lootDropped: [
    {
      itemId: string,
      type: string,
      x: number,
      y: number
    }
  ]
})
```

---

## 🎮 GAME LOOP

```javascript
// Main game loop (60 FPS ideal, 20 FPS min)
const TICK_RATE = 20; // 50ms ticks

setInterval(() => {
  // 1. Process input (from socket events queue)
  processPlayerInputs();
  
  // 2. Update game state
  updateMonsterAI();
  updateProjectiles();
  updateCooldowns();
  updateManaRegen();
  
  // 3. Detect collisions
  detectCombat();
  detectLootPickup();
  
  // 4. Sync to all clients
  broadcastWorldState();
  
  // 5. Persist periodically (every 30s)
  if (tick % 600 === 0) {
    savePlayerData();
  }
}, 1000 / TICK_RATE);
```

---

## 🔄 DATA FLOW (COMBAT EXAMPLE)

```
T0: Player clicks "Fireball" (CLIENT)
    └─> socket.emit('skill:use', { skillId: 'fireball', targetId: 'monster_1' })

T50ms: Server receives event
       └─> CombatEngine.validateSkill()
           - Check: mana >= 50? ✓
           - Check: cooldown expired? ✓
           - Check: distance <= 15 tiles? ✓
           - Check: target exists? ✓

T100ms: Server calculates hit
        └─> damage = baseDamage * (1 + critChance) * (1 - targetArmor%)
            damage = 80 * 1.0 * (1 - 0.2) = 64
            Apply slow effect (slow: 30%, duration: 3s)

T150ms: Server broadcasts to all clients in zone
        └─> socket.emit('skill:hit', {
              skillId: 'fireball',
              targetId: 'monster_1',
              damage: 64,
              effects: [{ type: 'slow', duration: 3000 }]
            })

T200ms: Clients update their state
        └─> Monster animation plays
            Health bar updates
            Floating damage text appears
            Sound effect plays (client-side only)
```

---

## 💾 DATABASE SCHEMA

### Players Collection
```javascript
{
  _id: ObjectId,
  playerId: string,
  name: string,
  class: 'warrior' | 'mage' | 'ranger' | 'healer' | 'bruiser',
  level: number,
  experience: number,
  
  // Stats
  health: number,
  maxHealth: number,
  mana: number,
  maxMana: number,
  stamina: number,
  maxStamina: number,
  armor: number,
  
  // Position
  x: number,
  y: number,
  zone: 'town' | 'countryside' | 'darkforest',
  
  // Equipment
  equipment: {
    helmet: { id: string, durability: number },
    chest: { id: string, durability: number },
    legs: { id: string, durability: number },
    weapon: { id: string, durability: number },
    offhand: { id: string, durability: number },
    ring: { id: string, durability: number }
  },
  
  // Inventory
  inventory: [
    { itemId: string, quantity: number, durability: number }
  ],
  
  // Guild
  guildId: string | null,
  guildRank: 'member' | 'officer' | 'master',
  
  // Progression
  skills: {
    swordSkill: number,     // 0-100
    magicSkill: number,
    arrowSkill: number,
    healingSkill: number,
    defenseSkill: number
  },
  
  // Meta
  createdAt: Date,
  lastLogin: Date,
  banned: boolean
}
```

### Monsters Collection
```javascript
{
  _id: ObjectId,
  monsterId: string,
  type: 'goblin' | 'wolf' | 'troll' | 'boss',
  x: number,
  y: number,
  zone: string,
  
  health: number,
  maxHealth: number,
  damage: number,
  armor: number,
  
  // Drops
  lootTable: [
    { itemId: string, chance: 0.5, quantity: 1 },
    { itemId: string, chance: 0.3, quantity: 2 }
  ],
  
  // Respawn
  respawnTime: number, // ms
  lastDied: Date | null
}
```

### Items Collection
```javascript
{
  _id: ObjectId,
  itemId: string,
  type: 'weapon' | 'armor' | 'consumable' | 'misc',
  name: string,
  rarity: 'common' | 'uncommon' | 'rare' | 'epic' | 'legendary',
  
  // Stats
  armor: number,
  damage: number,
  manaBonus: number,
  
  // Durability
  maxDurability: number,
  degradationRate: 0.005, // per hit
  
  // Owner
  ownerId: string | null, // null if on ground
  x: number | null,
  y: number | null
}
```

---

## 🔐 SECURITY & VALIDATION

### Server-Side Validation Checklist

Para **CADA skill usage:**
- ✅ Player exists in world
- ✅ Mana >= skill.manaCost
- ✅ Cooldown expired
- ✅ Target exists
- ✅ Distance within range
- ✅ Player not in "loading" state
- ✅ Player not dead

Para **CADA movement:**
- ✅ Position within map bounds
- ✅ Movement speed realistic (< maxSpeed)
- ✅ No teleporting (distance check)
- ✅ Zone access validated

Para **CADA loot pickup:**
- ✅ Item exists at position
- ✅ Player in range (< 2 tiles)
- ✅ Inventory space available
- ✅ Item durability > 0

---

## 📊 PERFORMANCE TARGETS

| Metric | Target | Notes |
|--------|--------|-------|
| **Tick Rate** | 20 Hz (50ms) | Balanced vs accuracy |
| **Player Sync** | Every tick | Full state broadcast |
| **Cooldown Resolution** | 50ms | Smooth feel |
| **Concurrent Players** | 100+ per server | Shardable |
| **Network Bandwidth** | <100KB/s per player | Optimized updates |
| **Server CPU** | <70% on 100 players | Headroom for spikes |

---

## 🚀 DEPLOYMENT PHASES

### Phase 1: Local Testing (Week 1-2)
- Single Node.js server
- SQLite database
- 10 players testing

### Phase 2: VPS Deploy (Week 3-4)
- AWS EC2 (t3.small)
- MongoDB Atlas
- 50+ players

### Phase 3: Production (Week 5+)
- Load balancer (horizontal scaling)
- Multiple servers with shared DB
- 500+ concurrent players
- CDN for assets

---

## 📚 NEXT STEPS

1. Setup Node.js project with Express
2. Implement Socket.io connection
3. Create WorldManager
4. Create basic PlayerManager
5. Implement CombatEngine
6. Create React + Phaser client
7. Test with 10 players locally
8. Iterate on balance
