// WorldManager — coordena todo o estado de UMA zona de jogo.
// É o único "dono" do estado; os outros managers interagem via ele.
//
// Mudança v2: aceita zoneId no construtor e escopa broadcasts para
// io.to(zoneId) em vez de io.emit (global). Isso isola o estado por zona.
const { TICK_RATE, MAP_W, MAP_H } = require('../config/constants');

class WorldManager {
  /**
   * @param {import('socket.io').Server} io
   * @param {string} zoneId - ID da zona (Socket.IO room). Padrão: 'overworld'.
   */
  constructor(io, zoneId = 'overworld') {
    this.io      = io;
    this.zoneId  = zoneId;
    this.players  = new Map(); // socketId → PlayerState
    this.monsters = new Map(); // monsterId → MonsterState
    this.items    = new Map(); // itemId → ItemState (loot no chão)
    this.events   = this._newEvents();
    this._lastTick = Date.now();
    this._tickMs   = 1000 / TICK_RATE;
  }

  start() {
    this._interval = setInterval(() => this._tick(), this._tickMs);
    console.log(`[WorldManager:${this.zoneId}] Loop iniciado a ${TICK_RATE}Hz`);
  }

  stop() {
    clearInterval(this._interval);
  }

  // ----- Tick principal -----
  _tick() {
    const now = Date.now();
    this._lastTick = now;
    this._broadcast(now);
    this.events = this._newEvents();
  }

  // ----- Estado snapshot para broadcast -----
  // Emite para io.to(zoneId) — apenas os players desta zona recebem.
  _broadcast(now) {
    const zoneEmitter = this.io.to(this.zoneId);

    const playerSnap = [];
    for (const p of this.players.values()) {
      playerSnap.push({
        id: p.id, name: p.name,
        class: p.class,          // mantido para o cliente browser HTML
        playerClass: p.class,    // alias — C# não aceita campo 'class'
        x: Math.round(p.x), y: Math.round(p.y),
        hp: Math.round(p.hp), maxHp: p.maxHp,
        mana: Math.round(p.mana), maxMana: p.maxMana,
        dead: p.dead,
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

    // Emite eventos de combate acumulados no tick
    if (this.events.hits.length)       zoneEmitter.emit('combat:hits',      this.events.hits);
    if (this.events.interrupts.length) zoneEmitter.emit('combat:interrupts', this.events.interrupts);
    if (this.events.deaths.length)     zoneEmitter.emit('combat:deaths',     this.events.deaths);
  }

  _newEvents() { return { hits: [], interrupts: [], deaths: [] }; }

  // ----- Helpers públicos usados pelos managers -----
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
