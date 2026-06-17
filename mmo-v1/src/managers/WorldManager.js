// WorldManager — coordena todo o estado do jogo
// É o único "dono" do estado; os outros managers interagem via ele.
const { TICK_RATE, MAP_W, MAP_H, STATE_BROADCAST_INTERVAL_MS, DB_SAVE_INTERVAL_MS } = require('../config/constants');

class WorldManager {
  constructor(io) {
    this.io = io;
    this.players  = new Map(); // socketId → PlayerState
    this.monsters = new Map(); // monsterId → MonsterState
    this.items    = new Map(); // itemId → ItemState (loot no chão)
    this.events   = this._newEvents();
    this._lastTick = Date.now();
    this._tickMs   = 1000 / TICK_RATE;
  }

  start() {
    this._interval = setInterval(() => this._tick(), this._tickMs);
    console.log(`[World] Loop iniciado a ${TICK_RATE}Hz`);
  }

  stop() {
    clearInterval(this._interval);
  }

  // ----- Tick principal -----
  _tick() {
    const now = Date.now();
    const dtSec = (now - this._lastTick) / 1000;
    this._lastTick = now;

    // TODO: resolver casts prontos
    // TODO: regen de mana/stamina
    // TODO: IA de monstros (a cada MONSTER_AI_TICK_MS)
    // TODO: despawn de itens vencidos

    this._broadcast(now);
    this.events = this._newEvents();
  }

  // ----- Estado snapshot para broadcast -----
  _broadcast(now) {
    const playerSnap = [];
    for (const p of this.players.values()) {
      playerSnap.push({
        id: p.id, name: p.name, class: p.class,
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
      monsterSnap.push({ id: m.id, type: m.type, x: Math.round(m.x), y: Math.round(m.y), hp: m.hp, maxHp: m.maxHp });
    }

    const itemSnap = [];
    for (const i of this.items.values()) {
      itemSnap.push({ id: i.id, type: i.type, x: i.x, y: i.y });
    }

    this.io.emit('world:update', { t: now, players: playerSnap, monsters: monsterSnap, items: itemSnap });

    // Emite eventos acumulados no tick
    if (this.events.hits.length)       this.io.emit('combat:hits',       this.events.hits);
    if (this.events.interrupts.length) this.io.emit('combat:interrupts',  this.events.interrupts);
    if (this.events.deaths.length)     this.io.emit('combat:deaths',      this.events.deaths);
  }

  _newEvents() { return { hits: [], interrupts: [], deaths: [] }; }

  // ----- Helpers públicos usados pelos managers -----
  addPlayer(state)  { this.players.set(state.id, state); }
  removePlayer(id)  { this.players.delete(id); }
  getPlayer(id)     { return this.players.get(id); }

  addMonster(state) { this.monsters.set(state.id, state); }
  removeMonster(id) { this.monsters.delete(id); }

  addItem(state)    { this.items.set(state.id, state); }
  removeItem(id)    { this.items.delete(id); }

  emitHit(hit)          { this.events.hits.push(hit); }
  emitInterrupt(data)   { this.events.interrupts.push(data); }
  emitDeath(data)       { this.events.deaths.push(data); }
}

module.exports = WorldManager;
