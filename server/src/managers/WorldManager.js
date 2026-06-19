// WorldManager -- coordena todo o estado de UMA zona de jogo.
// Mudanca v2: escopa broadcasts para io.to(zoneId) em vez de io.emit (global).
const { TICK_RATE } = require('../config/constants');

// XP necessario para o proximo level
function xpMax(level) { return Math.floor(100 * Math.pow(level || 1, 1.5)); }

class WorldManager {
  /**
   * @param {object} io       - Socket.IO server instance
   * @param {string} zoneId   - ID da zona (ex: 'overworld', 'dungeon_1')
   * @param {string} zoneType - Tipo de zona: 'safe' | 'yellow' | 'red' | 'black'
   *   Controla regras de morte: taxa de destruição e se loot por outros é permitido.
   */
  constructor(io, zoneId = 'overworld', zoneType = 'yellow') {
    this.io       = io;
    this.zoneId   = zoneId;
    this.zoneType = zoneType;   // usado por PlayerManager.handlePlayerDeath
    this.players  = new Map();
    this.monsters = new Map();
    this.items    = new Map();
    this.events   = this._newEvents();
    this._lastTick = Date.now();
    this._tickMs   = 1000 / TICK_RATE;
  }

  start() {
    this._interval = setInterval(() => this._tick(), this._tickMs);
    console.log('[WorldManager:' + this.zoneId + '] Loop iniciado a ' + TICK_RATE + 'Hz');
  }

  stop() {
    clearInterval(this._interval);
  }

  _tick() {
    const now = Date.now();
    this._lastTick = now;
    this._broadcast(now);
    this.events = this._newEvents();
  }

  _broadcast(now) {
    const zoneEmitter = this.io.to(this.zoneId);

    const playerSnap = [];
    for (const p of this.players.values()) {
      playerSnap.push({
        id: p.id, name: p.name,
        class: p.class,
        playerClass: p.class,
        x: Math.round(p.x), y: Math.round(p.y),
        hp: Math.round(p.hp), maxHp: p.maxHp,
        mana: Math.round(p.mana), maxMana: p.maxMana,
        stamina: Math.round(p.stamina), maxStamina: p.maxStamina,
        dead: p.dead,
        level:  p.level,
        xp:     Math.round(p.xp),
        xpMax:  xpMax(p.level),
        gold:   p.gold,
        casting: p.casting ? {
          abilityId: p.casting.abilityId,
          remaining: Math.max(0, p.casting.endsAt - now),
          total: p.casting.total,
        } : null,
      });
    }

    const monsterSnap = [];
    for (const m of this.monsters.values()) {
      monsterSnap.push({
        id: m.id, type: m.type,
        x: Math.round(m.x), y: Math.round(m.y),
        hp: m.hp, maxHp: m.maxHp,
      });
    }

    const itemSnap = [];
    for (const i of this.items.values()) {
      itemSnap.push({ id: i.id, type: i.type, x: i.x, y: i.y });
    }

    zoneEmitter.emit('world:update', {
      t: now,
      zoneId: this.zoneId,
      players: playerSnap,
      monsters: monsterSnap,
      items: itemSnap,
    });

    if (this.events.hits.length)       zoneEmitter.emit('combat:hits',      this.events.hits);
    if (this.events.interrupts.length) zoneEmitter.emit('combat:interrupts', this.events.interrupts);
    if (this.events.deaths.length)     zoneEmitter.emit('combat:deaths',     this.events.deaths);
  }

  _newEvents() { return { hits: [], interrupts: [], deaths: [] }; }

  addPlayer(state)  { this.players.set(state.id, state); }
  removePlayer(id)  { this.players.delete(id); }
  getPlayer(id)     { return this.players.get(id); }

  addMonster(state) { this.monsters.set(state.id, state); }
  removeMonster(id) { this.monsters.delete(id); }
  getMonster(id)    { return this.monsters.get(id); }

  addItem(state)    { this.items.set(state.id, state); }
  removeItem(id)    { this.items.delete(id); }

  emitHit(hit)        { this.events.hits.push(hit); }
  emitInterrupt(data) { this.events.interrupts.push(data); }
  emitDeath(data)     { this.events.deaths.push(data); }
}

module.exports = WorldManager;
