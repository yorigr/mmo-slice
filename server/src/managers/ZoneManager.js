// ZoneManager — gerencia instâncias de zona isoladas (equivalente a "rooms" do Colyseus).
// Cada zona tem seu próprio loop de jogo e estado independente.
// Players entram/saem de zonas; broadcasts são escopados via Socket.IO rooms.
//
// Fluxo:
//   ZoneManager.getOrCreate('overworld') → Zone.start() → WorldManager.start() + spawn monstros
//   ZoneManager.joinZone(socket, zoneId, playerData) → socket.join(zoneId) → estado player criado
//   ZoneManager.leaveZone(socket) → player removido → zona vazia não-padrão é destruída
//
// Por que não Colyseus? O cliente Unity usa WebSocket + Socket.IO v4 em C# puro (sem SDK externo).
// Migrar exigiria o Colyseus Unity package ou reimplementar o protocolo binário do Colyseus
// manualmente. Custo > benefício para o estágio atual do projeto. Os recursos relevantes do
// Colyseus (rooms, state sync, reconnect) são implementados aqui sobre Socket.IO.

const WorldManager  = require('./WorldManager');
const PlayerManager = require('./PlayerManager');
const CombatEngine  = require('./CombatEngine');
const MonsterManager = require('./MonsterManager');
const { MONSTER_SPAWN_INTERVAL_MS } = require('../config/constants');

const INITIAL_MONSTERS    = 15;
const MAX_MONSTERS_PER_ZONE = 30;

// ─── Zone ─────────────────────────────────────────────────────────────────────
// Uma zona é um mundo isolado: managers próprios, game loop próprio, Socket.IO room própria.

class Zone {
  /**
   * @param {object} io      - Socket.IO server instance
   * @param {string} zoneId  - ID único da zona
   * @param {object} options
   * @param {string} options.zoneType - 'safe' | 'yellow' | 'red' | 'black' (padrão: 'yellow')
   *   Define regras de morte: taxa de destruição de gear e se full loot está ativado.
   */
  constructor(io, zoneId, options = {}) {
    this.id      = zoneId;
    this.type    = options.zoneType || 'yellow';
    this.world   = new WorldManager(io, zoneId, this.type);
    this.players = new PlayerManager(this.world);
    this.combat  = new CombatEngine(this.world, this.players);
    this.monsters = new MonsterManager(this.world, this.combat);

    // Liga combat ↔ monsters (para callbacks de morte / drop de loot)
    this.combat.setMonsterManager(this.monsters);

    // Substitui o _tick do WorldManager para injetar a lógica de jogo antes do broadcast.
    // Padrão decorator: origTick() cuida de broadcast + reset de eventos;
    // o novo _tick cuida de regen, casts e IA primeiro.
    const origTick = this.world._tick.bind(this.world);
    const self = this;
    this.world._tick = function () {
      const now   = Date.now();
      const dtSec = Math.min((now - this._lastTick) / 1000, 0.1); // cap 100ms contra lag spike
      self.players.regenTick(dtSec);
      self.combat.resolveDueCasts(now);
      self.monsters.aiTick(now);
      origTick();
    };

    this._spawnTimer = null;
  }

  start() {
    this.world.start();

    // Popula com monstros iniciais
    for (let i = 0; i < INITIAL_MONSTERS; i++) this.monsters.spawnRandom();

    // Auto-respawn periódico (usa MONSTER_SPAWN_INTERVAL_MS de constants.js)
    this._spawnTimer = setInterval(() => {
      if (this.world.monsters.size < MAX_MONSTERS_PER_ZONE) {
        this.monsters.spawnRandom();
      }
    }, MONSTER_SPAWN_INTERVAL_MS);

    console.log(`[Zone:${this.id}] iniciada (${INITIAL_MONSTERS} monstros, max ${MAX_MONSTERS_PER_ZONE})`);
  }

  stop() {
    this.world.stop();
    if (this._spawnTimer) { clearInterval(this._spawnTimer); this._spawnTimer = null; }
    console.log(`[Zone:${this.id}] parada`);
  }

  get playerCount() { return this.world.players.size; }
  get isEmpty()     { return this.playerCount === 0; }
}

// ─── ZoneManager ──────────────────────────────────────────────────────────────

class ZoneManager {
  constructor(io) {
    this.io          = io;
    this.zones       = new Map(); // zoneId → Zone
    this._socketZone = new Map(); // socketId → zoneId
  }

  /**
   * Retorna a zona (criando e iniciando se necessário).
   * @param {string} zoneId
   * @param {object} options - Passado ao construtor de Zone (ex: { zoneType: 'red' })
   */
  getOrCreate(zoneId, options = {}) {
    if (!this.zones.has(zoneId)) {
      const zone = new Zone(this.io, zoneId, options);
      this.zones.set(zoneId, zone);
      zone.start();
    }
    return this.zones.get(zoneId);
  }

  /**
   * Faz um socket entrar em uma zona e cria o player.
   * Retorna { zone, state }.
   */
  joinZone(socket, zoneId, playerData) {
    this.leaveZone(socket); // remove da zona anterior, se houver

    const zone = this.getOrCreate(zoneId);
    socket.join(zoneId);
    this._socketZone.set(socket.id, zoneId);

    const state = zone.players.createPlayer(socket.id, playerData);
    return { zone, state };
  }

  /** Remove o socket da zona atual. Destrói zonas não-padrão que ficam vazias. */
  leaveZone(socket) {
    const zoneId = this._socketZone.get(socket.id);
    if (!zoneId) return;

    const zone = this.zones.get(zoneId);
    if (zone) {
      zone.players.removePlayer(socket.id);

      if (zone.isEmpty && zoneId !== 'overworld') {
        zone.stop();
        this.zones.delete(zoneId);
        console.log(`[ZoneManager] Zona "${zoneId}" destruída (vazia)`);
      }
    }

    socket.leave(zoneId);
    this._socketZone.delete(socket.id);
  }

  /** Retorna a zona onde o socket está, ou null. */
  getZone(socketId) {
    const zoneId = this._socketZone.get(socketId);
    return zoneId ? (this.zones.get(zoneId) || null) : null;
  }

  /** Retorna o ID da zona onde o socket está, ou null. */
  getZoneId(socketId) {
    return this._socketZone.get(socketId) || null;
  }

  /** Diagnóstico: lista zonas ativas e quantidade de players. */
  getStats() {
    const result = {};
    for (const [id, zone] of this.zones) {
      result[id] = { players: zone.playerCount, monsters: zone.world.monsters.size };
    }
    return result;
  }
}

module.exports = { ZoneManager, Zone };
