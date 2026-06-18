# 🚀 MMORPG SERVER - PHASE 0 SETUP

## Step 1: Create Project Structure

```bash
# Criar pasta do projeto
mkdir mmorpg-server
cd mmorpg-server

# Inicializar Node.js
npm init -y

# Instalar dependências básicas
npm install express socket.io dotenv sqlite3 cors uuid
npm install --save-dev nodemon jest

# Criar estrutura de pastas
mkdir -p src/{managers,models,utils,config}
mkdir -p tests
mkdir -p logs
```

---

## Step 2: Crie os Arquivos AGORA

### A) package.json
```json
{
  "name": "mmorpg-server",
  "version": "0.1.0",
  "description": "Medieval Skill-Based MMORPG Server",
  "main": "src/server.js",
  "scripts": {
    "start": "node src/server.js",
    "dev": "nodemon src/server.js",
    "test": "jest",
    "test:watch": "jest --watch"
  },
  "keywords": ["mmorpg", "game", "socket.io", "node.js"],
  "author": "Your Name",
  "license": "MIT",
  "dependencies": {
    "express": "^4.18.2",
    "socket.io": "^4.5.4",
    "dotenv": "^16.0.3",
    "sqlite3": "^5.1.6",
    "cors": "^2.8.5",
    "uuid": "^9.0.0"
  },
  "devDependencies": {
    "nodemon": "^2.0.20",
    "jest": "^29.0.0"
  }
}
```

### B) .env.example (copiar de GAME_CONFIG.md)
```bash
# Server
NODE_ENV=development
PORT=3000
HOST=localhost

# Database
DATABASE_URL=sqlite:./game.db

# Security
JWT_SECRET=your_secret_key_change_this_in_production
SESSION_TIMEOUT=3600000

# Game Settings
GAME_DIFFICULTY=1.0
LOOT_DROP_RATE=1.0
GOLD_MULTIPLIER=1.0

# Debug
DEBUG=game:*
LOG_LEVEL=info
ENABLE_PVP=true
```

### C) .env (criar baseado em .env.example)
```bash
cp .env.example .env
# Edite .env com seus valores
```

---

## Step 3: constants.js (Copiar de GAME_CONFIG.md)

**Arquivo:** `src/config/constants.js`

```javascript
// GAME CONSTANTS & CONFIGURATION

const WORLD_CONFIG = {
  MAP_WIDTH: 100,
  MAP_HEIGHT: 100,
  TILE_SIZE: 32,
  RENDER_DISTANCE: 20,
  TICK_RATE: 20, // 50ms ticks
  
  ZONES: {
    TOWN: {
      name: 'Town',
      color: '#FFFFFF',
      pvpEnabled: false,
      bounds: { x: 0, y: 0, w: 30, h: 30 },
      respawnPoint: { x: 15, y: 15 },
    },
    COUNTRYSIDE: {
      name: 'Countryside',
      color: '#FFFF00',
      pvpEnabled: true,
      bounds: { x: 30, y: 0, w: 35, h: 50 },
      respawnPoint: { x: 15, y: 15 },
    },
    DARK_FOREST: {
      name: 'Dark Forest',
      color: '#FF0000',
      pvpEnabled: true,
      bounds: { x: 65, y: 0, w: 35, h: 50 },
      respawnPoint: { x: 15, y: 15 },
    },
  },
  
  DEFAULT_MOVEMENT_SPEED: 4,
  MAX_MOVEMENT_SPEED: 6,
};

const COMBAT_CONFIG = {
  COMBAT_TICK_MS: 50,
  SKILL_VALIDATION_LATENCY: 100,
  BASE_ARMOR_DAMAGE_REDUCTION: 0.01,
  CRITICAL_STRIKE_MULTIPLIER: 1.5,
  
  DEATH_ANIMATION_DURATION: 3000,
  RESPAWN_DELAY: 5000,
  DEATH_LOOT_TIMEOUT: 300000,
  KILLER_LOOT_EXCLUSIVITY: 10000,
};

const SKILL_CONFIG = {
  HOTBAR_SIZE: 5,
  MAX_HOTBARS: 2,
  
  MANA_REGEN_PER_SECOND: {
    warrior: 10,
    mage: 20,
    ranger: 5,
    healer: 15,
    bruiser: 12,
  },
  
  CLASS_MODIFIERS: {
    warrior: { damage: 1.1, armor: 1.15, health: 1.2 },
    mage: { damage: 0.95, armor: 0.6, health: 0.8, mana: 1.3 },
    ranger: { damage: 1.0, armor: 0.7, health: 0.9, stamina: 1.2 },
    healer: { damage: 0.7, armor: 0.8, health: 1.0, mana: 1.2 },
    bruiser: { damage: 1.05, armor: 1.0, health: 1.1 },
  },
};

const ECONOMY_CONFIG = {
  DURABILITY_LOSS_PER_HIT: 0.5,
  DURABILITY_LOSS_ON_DEATH: 30,
  REPAIR_COST_MULTIPLIER: 0.3,
  
  ITEM_VALUE: {
    common: 10,
    uncommon: 50,
    rare: 200,
    epic: 800,
    legendary: 3000,
  },
  
  MONSTER_LOOT_CHANCE: 0.7,
  BOSS_LOOT_CHANCE: 1.0,
  
  ENERGY_MAX: 100,
  ENERGY_PER_KILL: 10,
  ENERGY_REGEN_PER_MINUTE: 1,
  FARM_LIMIT_WITH_ZERO_ENERGY: 10,
};

const PROGRESSION_CONFIG = {
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
    ranger: 50,
    healer: 120,
    bruiser: 100,
  },
};

const SECURITY_CONFIG = {
  MAX_SPEED_THRESHOLD: 8,
  MAX_POSITION_DELTA: 10,
  COOLDOWN_GRACE_PERIOD: 50,
};

const DEBUG_CONFIG = {
  ENABLE_GODMODE: false,
  ENABLE_INSTANT_KILLS: false,
  ENABLE_INFINITE_MANA: false,
  LOG_LEVEL: 'INFO',
  LOG_COMBAT: true,
  LOG_LOOT: true,
  LOG_PVP: true,
  BOT_PLAYERS_COUNT: 0,
};

module.exports = {
  WORLD_CONFIG,
  COMBAT_CONFIG,
  SKILL_CONFIG,
  ECONOMY_CONFIG,
  PROGRESSION_CONFIG,
  SECURITY_CONFIG,
  DEBUG_CONFIG,
};
```

---

## Step 4: server.js (Entry Point)

**Arquivo:** `src/server.js`

```javascript
require('dotenv').config();
const express = require('express');
const { createServer } = require('http');
const { Server } = require('socket.io');
const cors = require('cors');
const path = require('path');

// Import managers
const WorldManager = require('./managers/WorldManager');
const PlayerManager = require('./managers/PlayerManager');
const SocketHandler = require('./socket-handler');

// Initialize express & HTTP server
const app = express();
const httpServer = createServer(app);
const io = new Server(httpServer, {
  cors: {
    origin: '*',
    methods: ['GET', 'POST'],
  },
});

// Middleware
app.use(cors());
app.use(express.json());
app.use(express.static(path.join(__dirname, '../public')));

// Initialize game managers
const worldManager = new WorldManager();
const playerManager = new PlayerManager();

// Initialize socket handler
const socketHandler = new SocketHandler(io, worldManager, playerManager);

// Routes
app.get('/api/health', (req, res) => {
  res.json({ status: 'Server is running', timestamp: new Date() });
});

app.get('/api/world', (req, res) => {
  res.json({
    zones: Object.keys(worldManager.zones),
    playerCount: playerManager.getPlayerCount(),
    timestamp: new Date(),
  });
});

// Socket.io connection
io.on('connection', (socket) => {
  console.log(`[SOCKET] Player connected: ${socket.id}`);
  
  socket.on('player:create', (data) => socketHandler.handlePlayerCreate(socket, data));
  socket.on('player:move', (data) => socketHandler.handlePlayerMove(socket, data));
  socket.on('player:disconnect', () => socketHandler.handlePlayerDisconnect(socket));
  
  socket.on('disconnect', () => {
    console.log(`[SOCKET] Player disconnected: ${socket.id}`);
    socketHandler.handlePlayerDisconnect(socket);
  });
});

// Start game loop
const TICK_RATE = 20; // 50ms
setInterval(() => {
  worldManager.tick();
  
  // Broadcast world state to all connected players
  io.emit('world:update', {
    players: worldManager.getPlayersData(),
    monsters: worldManager.getMonstersData(),
    items: worldManager.getItemsData(),
    timestamp: Date.now(),
  });
}, 1000 / TICK_RATE);

// Start server
const PORT = process.env.PORT || 3000;
const HOST = process.env.HOST || 'localhost';

httpServer.listen(PORT, HOST, () => {
  console.log(`
╔══════════════════════════════════════════╗
║   🎮 MMORPG SERVER STARTED               ║
╠══════════════════════════════════════════╣
║   Host: ${HOST.padEnd(32)}║
║   Port: ${PORT.toString().padEnd(32)}║
║   Environment: ${(process.env.NODE_ENV || 'development').padEnd(20)}║
╚══════════════════════════════════════════╝
  `);
});

// Graceful shutdown
process.on('SIGINT', () => {
  console.log('\n[SERVER] Shutting down gracefully...');
  httpServer.close(() => {
    console.log('[SERVER] Server closed');
    process.exit(0);
  });
});

module.exports = { app, io, worldManager, playerManager };
```

---

## Step 5: WorldManager.js (Game State)

**Arquivo:** `src/managers/WorldManager.js`

```javascript
const { WORLD_CONFIG } = require('../config/constants');
const { v4: uuidv4 } = require('uuid');

class WorldManager {
  constructor() {
    this.zones = WORLD_CONFIG.ZONES;
    this.players = new Map(); // playerId → Player object
    this.monsters = new Map(); // monsterId → Monster object
    this.items = new Map(); // itemId → Item object
    this.tick = 0;
    
    console.log('[WorldManager] Initialized');
  }

  /**
   * Add player to world
   */
  addPlayer(playerId, playerData) {
    if (this.players.has(playerId)) {
      console.warn(`[WorldManager] Player ${playerId} already exists`);
      return null;
    }

    const player = {
      id: playerId,
      name: playerData.name,
      class: playerData.class,
      x: 15,
      y: 15,
      zone: 'TOWN',
      health: 100,
      maxHealth: 100,
      mana: 100,
      maxMana: 100,
      armor: 10,
      equipment: {},
      inventory: [],
      hotbar: [null, null, null, null, null],
      cooldowns: {},
      inCombat: false,
      createdAt: new Date(),
    };

    this.players.set(playerId, player);
    console.log(`[WorldManager] Player added: ${playerData.name} (${playerId})`);
    
    return player;
  }

  /**
   * Remove player from world
   */
  removePlayer(playerId) {
    this.players.delete(playerId);
    console.log(`[WorldManager] Player removed: ${playerId}`);
  }

  /**
   * Get player by ID
   */
  getPlayer(playerId) {
    return this.players.get(playerId);
  }

  /**
   * Update player position
   */
  updatePlayerPosition(playerId, x, y) {
    const player = this.getPlayer(playerId);
    if (!player) return null;

    // Validate position
    if (x < 0 || x > WORLD_CONFIG.MAP_WIDTH || y < 0 || y > WORLD_CONFIG.MAP_HEIGHT) {
      console.warn(`[WorldManager] Invalid position for ${playerId}: ${x}, ${y}`);
      return null;
    }

    player.x = x;
    player.y = y;
    return player;
  }

  /**
   * Get all players data for broadcast
   */
  getPlayersData() {
    return Array.from(this.players.values()).map(p => ({
      id: p.id,
      name: p.name,
      class: p.class,
      x: p.x,
      y: p.y,
      zone: p.zone,
      health: p.health,
      maxHealth: p.maxHealth,
      mana: p.mana,
      maxMana: p.maxMana,
      armor: p.armor,
      inCombat: p.inCombat,
    }));
  }

  /**
   * Get all monsters data
   */
  getMonstersData() {
    return Array.from(this.monsters.values());
  }

  /**
   * Get all items data
   */
  getItemsData() {
    return Array.from(this.items.values());
  }

  /**
   * Get player count
   */
  getPlayerCount() {
    return this.players.size;
  }

  /**
   * Main game loop tick
   */
  tick() {
    this.tick++;

    // Update player mana regen
    for (const player of this.players.values()) {
      if (player.mana < player.maxMana) {
        player.mana = Math.min(player.maxMana, player.mana + 5);
      }
    }

    // Log every 100 ticks (5 seconds at 20 Hz)
    if (this.tick % 100 === 0) {
      console.log(
        `[Tick ${this.tick}] Players: ${this.players.size}, ` +
        `Monsters: ${this.monsters.size}, Items: ${this.items.size}`
      );
    }
  }
}

module.exports = WorldManager;
```

---

## Step 6: PlayerManager.js (Player Data)

**Arquivo:** `src/managers/PlayerManager.js`

```javascript
const { v4: uuidv4 } = require('uuid');
const { PROGRESSION_CONFIG, SKILL_CONFIG } = require('../config/constants');

class PlayerManager {
  constructor() {
    this.players = new Map(); // playerId → Player profile
    console.log('[PlayerManager] Initialized');
  }

  /**
   * Create new player character
   */
  createPlayer(playerId, name, class_) {
    if (this.players.has(playerId)) {
      console.warn(`[PlayerManager] Player ${playerId} already created`);
      return null;
    }

    const player = {
      id: playerId,
      name: name,
      class: class_,
      createdAt: new Date(),
      
      // Base stats
      baseHealth: PROGRESSION_CONFIG.BASE_HEALTH[class_] || 100,
      baseMana: PROGRESSION_CONFIG.BASE_MANA[class_] || 100,
      armor: 10,
      
      // Equipment slots
      equipment: {
        helmet: null,
        chest: null,
        legs: null,
        weapon: null,
        offhand: null,
        ring: null,
      },
      
      // Inventory
      inventory: [],
      maxInventorySlots: 20,
      
      // Guild
      guildId: null,
      guildRank: null,
      
      // Progression
      experience: 0,
      level: 1,
      
      // Skills
      skills: {
        swordSkill: 0,
        magicSkill: 0,
        arrowSkill: 0,
        healingSkill: 0,
        defenseSkill: 0,
      },
      
      // Chat
      lastChatMessage: null,
      isMuted: false,
    };

    this.players.set(playerId, player);
    console.log(`[PlayerManager] Player created: ${name} (${class_})`);
    
    return player;
  }

  /**
   * Get player profile
   */
  getPlayer(playerId) {
    return this.players.get(playerId);
  }

  /**
   * Get player count
   */
  getPlayerCount() {
    return this.players.size;
  }

  /**
   * Add experience to player
   */
  addExperience(playerId, amount) {
    const player = this.getPlayer(playerId);
    if (!player) return null;

    player.experience += amount;
    console.log(`[PlayerManager] ${player.name} gained ${amount} XP`);
    
    return player;
  }

  /**
   * Add item to inventory
   */
  addItemToInventory(playerId, item) {
    const player = this.getPlayer(playerId);
    if (!player) return null;

    if (player.inventory.length >= player.maxInventorySlots) {
      console.warn(`[PlayerManager] Inventory full for ${playerId}`);
      return null;
    }

    player.inventory.push(item);
    console.log(`[PlayerManager] ${player.name} received ${item.name}`);
    
    return player;
  }

  /**
   * Save player data (persistence)
   */
  savePlayer(playerId) {
    const player = this.getPlayer(playerId);
    if (!player) return null;

    // TODO: Save to database
    console.log(`[PlayerManager] Player saved: ${playerId}`);
    
    return player;
  }
}

module.exports = PlayerManager;
```

---

## Step 7: socket-handler.js (Socket.io Events)

**Arquivo:** `src/socket-handler.js`

```javascript
const { v4: uuidv4 } = require('uuid');

class SocketHandler {
  constructor(io, worldManager, playerManager) {
    this.io = io;
    this.worldManager = worldManager;
    this.playerManager = playerManager;
    this.socketToPlayerId = new Map(); // socket.id → playerId
  }

  /**
   * Handle player creation
   */
  handlePlayerCreate(socket, data) {
    const { name, class: class_ } = data;

    if (!name || !class_) {
      socket.emit('error', { message: 'Name and class required' });
      return;
    }

    // Generate player ID
    const playerId = uuidv4();

    // Create player in managers
    this.playerManager.createPlayer(playerId, name, class_);
    this.worldManager.addPlayer(playerId, { name, class: class_ });

    // Map socket to player
    this.socketToPlayerId.set(socket.id, playerId);

    // Respond with player data
    socket.emit('player:created', {
      playerId: playerId,
      name: name,
      class: class_,
    });

    console.log(`[SocketHandler] Player created: ${name} (${playerId})`);
  }

  /**
   * Handle player movement
   */
  handlePlayerMove(socket, data) {
    const { x, y } = data;
    const playerId = this.socketToPlayerId.get(socket.id);

    if (!playerId) {
      socket.emit('error', { message: 'Player not found' });
      return;
    }

    // Update position in world
    this.worldManager.updatePlayerPosition(playerId, x, y);

    // Broadcast movement to all clients (will be part of world:update)
    console.log(`[SocketHandler] Player moved: ${playerId} → (${x}, ${y})`);
  }

  /**
   * Handle player disconnect
   */
  handlePlayerDisconnect(socket) {
    const playerId = this.socketToPlayerId.get(socket.id);

    if (playerId) {
      this.worldManager.removePlayer(playerId);
      this.socketToPlayerId.delete(socket.id);
      console.log(`[SocketHandler] Player disconnected: ${playerId}`);
    }
  }
}

module.exports = SocketHandler;
```

---

## Step 8: Run the Server

```bash
# Desenvolvimento (com auto-reload)
npm run dev

# Produção
npm start
```

**Expected Output:**
```
╔══════════════════════════════════════════╗
║   🎮 MMORPG SERVER STARTED               ║
╠══════════════════════════════════════════╣
║   Host: localhost                        ║
║   Port: 3000                             ║
║   Environment: development               ║
╚══════════════════════════════════════════╝

[WorldManager] Initialized
[PlayerManager] Initialized
```

---

## ✅ PHASE 0 COMPLETE!

Você tem:
- ✅ Projeto Node.js estruturado
- ✅ Express.js + Socket.io setup
- ✅ WorldManager (gerencia estado)
- ✅ PlayerManager (gerencia jogadores)
- ✅ Constants.js (balanceamento)
- ✅ Socket events básico
- ✅ Game loop (20 Hz)

---

## 🎯 PRÓXIMO: Phase 1

Quando estiver pronto, vamos adicionar:
1. Database persistence (SQLite)
2. Player authentication
3. More socket events
4. Combat system basics

**Continue?** Diga "Phase 1" e começamos! 🚀
