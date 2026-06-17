# ⚔️ SKILL DEFINITIONS & MECHANICS

**Versão:** 0.1  
**Format:** JSON + Documentation

---

## 📋 SKILL STRUCTURE

Cada skill tem a seguinte estrutura:

```javascript
{
  id: string,              // unique identifier
  name: string,            // display name
  class: string,           // warrior, mage, etc
  type: 'attack' | 'buff' | 'heal' | 'utility',
  
  // Resource Cost
  manaCost: number,        // mana gasto
  cooldown: number,        // ms entre uses
  
  // Casting
  castTime: number,        // ms para completar cast
  canCast: (player, target) => boolean, // validação
  
  // Mechanics
  range: number,           // tiles
  aoe: number | null,      // area of effect radius
  
  // Damage/Healing
  baseDamage: number,      // antes de multiplicadores
  baseHealing: number,     // 0 se não cura
  scaling: {
    weapon: number,        // % do weapon damage
    ap: number,            // % do attack power
  },
  
  // Effects
  effects: [
    {
      type: 'stun' | 'slow' | 'burn' | 'bleed' | 'shield' | 'regen',
      value: number,       // stun duration (ms), slow %,  etc
      duration: number,    // ms
    }
  ],
  
  // Animation
  animationKey: string,
  soundEffect: string,
  
  // Flavor
  description: string,
  flavorText: string
}
```

---

## 🗡️ WARRIOR SKILLS

### 1. Slash (Basic Attack)
```json
{
  "id": "warrior_slash",
  "name": "Slash",
  "class": "warrior",
  "type": "attack",
  
  "manaCost": 0,
  "cooldown": 1000,
  "castTime": 0,
  
  "range": 2,
  "aoe": null,
  
  "baseDamage": 25,
  "scaling": { "weapon": 1.0, "ap": 0.5 },
  
  "effects": [],
  
  "description": "Basic sword attack. Can be spammed to build momentum.",
  "animationKey": "slash_horizontal"
}
```

**Notes:**
- Instant cast (0ms)
- Smallest damage, but no mana cost
- Core rotation building block

---

### 2. Shield Bash
```json
{
  "id": "warrior_shield_bash",
  "name": "Shield Bash",
  "class": "warrior",
  "type": "attack",
  
  "manaCost": 20,
  "cooldown": 3000,
  "castTime": 500,
  
  "range": 2,
  "aoe": null,
  
  "baseDamage": 30,
  "scaling": { "weapon": 0.8, "ap": 0.3 },
  
  "effects": [
    { "type": "stun", "value": 1500, "duration": 1500 }
  ],
  
  "description": "Bash enemy with shield, dealing damage and stunning them. Casting can be interrupted.",
  "animationKey": "shield_bash"
}
```

**Notes:**
- 0.5s casting time (can be cancelled by damage)
- Stuns for 1.5s (time to land follow-up)
- High skill ceiling (timing the stun right)

---

### 3. Defensive Stance
```json
{
  "id": "warrior_defensive_stance",
  "name": "Defensive Stance",
  "class": "warrior",
  "type": "buff",
  
  "manaCost": 0,
  "cooldown": 0,
  "castTime": 0,
  
  "range": 0,
  "aoe": null,
  
  "baseDamage": 0,
  "scaling": {},
  
  "effects": [
    { "type": "armor_buff", "value": 50, "duration": null }
  ],
  
  "description": "Toggle stance. Increases armor by 50% but reduces damage output by 30%.",
  "animationKey": "defensive_stance_toggle"
}
```

**Notes:**
- Toggle skill (no cooldown)
- Passive effect while active
- Trade-off: more defense, less offense

---

### 4. Charge
```json
{
  "id": "warrior_charge",
  "name": "Charge",
  "class": "warrior",
  "type": "utility",
  
  "manaCost": 30,
  "cooldown": 5000,
  "castTime": 1000,
  
  "range": 10,
  "aoe": null,
  
  "baseDamage": 40,
  "scaling": { "weapon": 1.0, "ap": 0.6 },
  
  "effects": [
    { "type": "slow", "value": 50, "duration": 2000 }
  ],
  
  "description": "Charge at enemy, applying slow. Can be used for mobility.",
  "animationKey": "charge"
}
```

**Notes:**
- 1s casting time
- Moves warrior towards target (gap closer)
- Can be interrupted mid-cast
- Slow = enemy moves 50% slower

---

### 5. Riposte (Passive)
```json
{
  "id": "warrior_riposte",
  "name": "Riposte",
  "class": "warrior",
  "type": "utility",
  
  "manaCost": 0,
  "cooldown": 0,
  "castTime": 0,
  
  "range": 0,
  "aoe": null,
  
  "baseDamage": 20,
  "scaling": { "weapon": 0.5, "ap": 0.25 },
  
  "effects": [],
  
  "description": "Passive: 15% chance to counter-attack after being hit.",
  "animationKey": "riposte_counter"
}
```

**Notes:**
- Passive (always active)
- 15% proc chance per hit taken
- Adds skill ceiling (positioning matters)

---

## 🔥 MAGE SKILLS

### 1. Fireball
```json
{
  "id": "mage_fireball",
  "name": "Fireball",
  "class": "mage",
  "type": "attack",
  
  "manaCost": 50,
  "cooldown": 4000,
  "castTime": 2000,
  
  "range": 15,
  "aoe": 3,
  
  "baseDamage": 60,
  "scaling": { "weapon": 1.2, "ap": 0.8 },
  
  "effects": [
    { "type": "burn", "value": 5, "duration": 6000 }
  ],
  
  "description": "Cast a fireball in 2s. Deals AOE damage in 3x3 radius. Can be interrupted during cast.",
  "animationKey": "fireball_cast"
}
```

**Notes:**
- Longest casting time (2s)
- Largest AOE (3x3 tiles)
- DOT damage (burn ticks 6 times)
- Cancellable = skill-based

---

### 2. Frost Bolt
```json
{
  "id": "mage_frost_bolt",
  "name": "Frost Bolt",
  "class": "mage",
  "type": "attack",
  
  "manaCost": 35,
  "cooldown": 2500,
  "castTime": 1500,
  
  "range": 12,
  "aoe": null,
  
  "baseDamage": 40,
  "scaling": { "weapon": 1.0, "ap": 0.6 },
  
  "effects": [
    { "type": "slow", "value": 60, "duration": 3000 }
  ],
  
  "description": "Single-target frost bolt. Applies 60% slow for 3s. Good for kiting.",
  "animationKey": "frost_bolt"
}
```

**Notes:**
- Shorter cast than Fireball (1.5s)
- Single target (precision required)
- Heavy slow (great for kiting)

---

### 3. Teleport
```json
{
  "id": "mage_teleport",
  "name": "Teleport",
  "class": "mage",
  "type": "utility",
  
  "manaCost": 40,
  "cooldown": 10000,
  "castTime": 500,
  
  "range": 10,
  "aoe": null,
  
  "baseDamage": 0,
  "scaling": {},
  
  "effects": [],
  
  "description": "Teleport up to 10 tiles away. Primary escape tool. Can't teleport outside bounds.",
  "animationKey": "teleport_flash"
}
```

**Notes:**
- 0.5s casting (fast)
- Long cooldown (10s) = limited escape
- Server validates position (anti-exploit)
- Can be used for mobility in fights

---

### 4. Mana Shield
```json
{
  "id": "mage_mana_shield",
  "name": "Mana Shield",
  "class": "mage",
  "type": "buff",
  
  "manaCost": 0,
  "cooldown": 0,
  "castTime": 0,
  
  "range": 0,
  "aoe": null,
  
  "baseDamage": 0,
  "scaling": {},
  
  "effects": [
    { "type": "mana_to_shield", "value": 0.5, "duration": null }
  ],
  
  "description": "Toggle: Convert mana into shield. 1 mana = 0.5 shield HP. Reduces mana regen while active.",
  "animationKey": "mana_shield_toggle"
}
```

**Notes:**
- Toggle skill
- Trade mana for extra HP pool
- Passive while enabled
- Mana regen reduced -50%

---

### 5. Arcane Missiles (Alternative 5th skill)
```json
{
  "id": "mage_arcane_missiles",
  "name": "Arcane Missiles",
  "class": "mage",
  "type": "attack",
  
  "manaCost": 30,
  "cooldown": 3000,
  "castTime": 1000,
  
  "range": 12,
  "aoe": null,
  
  "baseDamage": 25,
  "scaling": { "weapon": 0.8, "ap": 0.5 },
  
  "effects": [],
  
  "description": "Fire 3 missiles rapidly. Each missile can miss independently.",
  "animationKey": "arcane_missiles"
}
```

**Notes:**
- Medium cast time
- Multi-hit = high skill ceiling
- Can be partially dodged

---

## 🏹 RANGER SKILLS

### 1. Shot
```json
{
  "id": "ranger_shot",
  "name": "Shot",
  "class": "ranger",
  "type": "attack",
  
  "manaCost": 0,
  "cooldown": 1200,
  "castTime": 1000,
  
  "range": 14,
  "aoe": null,
  
  "baseDamage": 30,
  "scaling": { "weapon": 1.1, "ap": 0.4 },
  
  "effects": [],
  
  "description": "Quick shot. Balanced damage and speed. Main rotation.",
  "animationKey": "arrow_shot"
}
```

**Notes:**
- 1s casting (medium)
- Longer range than melee (14 tiles)
- Main ranged DPS tool

---

### 2. Multi Shot
```json
{
  "id": "ranger_multi_shot",
  "name": "Multi Shot",
  "class": "ranger",
  "type": "attack",
  
  "manaCost": 25,
  "cooldown": 4000,
  "castTime": 1500,
  
  "range": 14,
  "aoe": 4,
  
  "baseDamage": 20,
  "scaling": { "weapon": 0.9, "ap": 0.3 },
  
  "effects": [],
  
  "description": "Fire 3 arrows in cone. Each hit applies full damage. AOE 4x4.",
  "animationKey": "multi_shot"
}
```

**Notes:**
- 1.5s casting
- Cone AOE (360° cone)
- Good for grouped enemies

---

### 3. Evasion (Passive)
```json
{
  "id": "ranger_evasion",
  "name": "Evasion",
  "class": "ranger",
  "type": "utility",
  
  "manaCost": 0,
  "cooldown": 0,
  "castTime": 0,
  
  "range": 0,
  "aoe": null,
  
  "baseDamage": 0,
  "scaling": {},
  
  "effects": [
    { "type": "dodge_chance", "value": 20, "duration": null }
  ],
  
  "description": "Passive: 20% chance to dodge incoming damage.",
  "animationKey": null
}
```

**Notes:**
- Always active
- 20% dodge chance = ~1 in 5 hits avoided
- Stacks with dodge skills

---

### 4. Sprint
```json
{
  "id": "ranger_sprint",
  "name": "Sprint",
  "class": "ranger",
  "type": "buff",
  
  "manaCost": 0,
  "cooldown": 0,
  "castTime": 0,
  
  "range": 0,
  "aoe": null,
  
  "baseDamage": 0,
  "scaling": {},
  
  "effects": [
    { "type": "speed_buff", "value": 50, "duration": null }
  ],
  
  "description": "Toggle sprint. +50% movement speed, drains stamina rapidly. Can't use skills while sprinting.",
  "animationKey": "sprint_animation"
}
```

**Notes:**
- Toggle (toggle to start/stop)
- Stamina drain = limited duration
- Can't cast while sprinting
- Perfect for kiting

---

### 5. Power Shot
```json
{
  "id": "ranger_power_shot",
  "name": "Power Shot",
  "class": "ranger",
  "type": "attack",
  
  "manaCost": 20,
  "cooldown": 5000,
  "castTime": 2000,
  
  "range": 16,
  "aoe": null,
  
  "baseDamage": 60,
  "scaling": { "weapon": 1.5, "ap": 0.7 },
  
  "effects": [
    { "type": "knockback", "value": 3, "duration": 500 }
  ],
  
  "description": "Powerful shot. Knockback enemy 3 tiles. High damage, long cast.",
  "animationKey": "power_shot"
}
```

**Notes:**
- 2s casting (long)
- Knockback = gap creator
- High damage payoff

---

## 💚 HEALER SKILLS

### 1. Heal
```json
{
  "id": "healer_heal",
  "name": "Heal",
  "class": "healer",
  "type": "heal",
  
  "manaCost": 40,
  "cooldown": 2000,
  "castTime": 1500,
  
  "range": 12,
  "aoe": null,
  
  "baseDamage": 0,
  "baseHealing": 60,
  "scaling": { "ap": 1.0 },
  
  "effects": [],
  
  "description": "Heal single target. Can be interrupted. Main healing tool.",
  "animationKey": "heal_cast"
}
```

**Notes:**
- Single target heal
- 1.5s casting (interruptible)
- Medium mana cost

---

### 2. Holy Nova
```json
{
  "id": "healer_holy_nova",
  "name": "Holy Nova",
  "class": "healer",
  "type": "heal",
  
  "manaCost": 60,
  "cooldown": 5000,
  "castTime": 2000,
  
  "range": 0,
  "aoe": 4,
  
  "baseDamage": 0,
  "baseHealing": 40,
  "scaling": { "ap": 0.8 },
  
  "effects": [],
  
  "description": "AOE heal in 4x4 radius. Heals all allies including self. Expensive.",
  "animationKey": "holy_nova"
}
```

**Notes:**
- AOE (self-centered)
- Expensive mana (60)
- Long cooldown (5s)
- Multi-target = group support

---

### 3. Divine Shield
```json
{
  "id": "healer_divine_shield",
  "name": "Divine Shield",
  "class": "healer",
  "type": "buff",
  
  "manaCost": 50,
  "cooldown": 8000,
  "castTime": 300,
  
  "range": 12,
  "aoe": null,
  
  "baseDamage": 0,
  "scaling": {},
  
  "effects": [
    { "type": "invulnerability", "value": 2000, "duration": 2000 }
  ],
  
  "description": "Make target invulnerable for 2s. Long cooldown. Game-changer in group fights.",
  "animationKey": "divine_shield"
}
```

**Notes:**
- Fast cast (0.3s)
- True invulnerability (no damage taken)
- Game-changing in PvP
- Long cooldown = limited uses

---

### 4. Resurrect
```json
{
  "id": "healer_resurrect",
  "name": "Resurrect",
  "class": "healer",
  "type": "heal",
  
  "manaCost": 80,
  "cooldown": 30000,
  "castTime": 3000,
  
  "range": 8,
  "aoe": null,
  
  "baseDamage": 0,
  "baseHealing": 100,
  "scaling": {},
  
  "effects": [],
  
  "description": "Revive fallen ally with 50% health. Can only be used out of combat. 30s cooldown.",
  "animationKey": "resurrect_spell"
}
```

**Notes:**
- 3s casting (channeled)
- Only works out of combat
- Huge cooldown (30s)
- 50% health revive

---

### 5. Cleanse
```json
{
  "id": "healer_cleanse",
  "name": "Cleanse",
  "class": "healer",
  "type": "utility",
  
  "manaCost": 30,
  "cooldown": 4000,
  "castTime": 500,
  
  "range": 12,
  "aoe": null,
  
  "baseDamage": 0,
  "scaling": {},
  
  "effects": [
    { "type": "remove_effects", "value": "all", "duration": 0 }
  ],
  
  "description": "Remove all negative effects (stun, slow, burn) from target.",
  "animationKey": "cleanse_aura"
}
```

**Notes:**
- 0.5s casting
- Removes ALL bad effects
- Support utility
- Anti-CC

---

## 💪 BRUISER SKILLS

### 1. Smash
```json
{
  "id": "bruiser_smash",
  "name": "Smash",
  "class": "bruiser",
  "type": "attack",
  
  "manaCost": 25,
  "cooldown": 3000,
  "castTime": 800,
  
  "range": 3,
  "aoe": null,
  
  "baseDamage": 50,
  "scaling": { "weapon": 1.2, "ap": 0.6 },
  
  "effects": [
    { "type": "knockback", "value": 2, "duration": 500 }
  ],
  
  "description": "Heavy smash. Knockback enemy 2 tiles. High damage output.",
  "animationKey": "smash_attack"
}
```

**Notes:**
- Medium cast time (0.8s)
- High damage
- Knockback = positioning tool
- AOE damage potential (with wall stun)

---

### 2. Riposte (Passive)
```json
{
  "id": "bruiser_riposte",
  "name": "Riposte",
  "class": "bruiser",
  "type": "utility",
  
  "manaCost": 0,
  "cooldown": 0,
  "castTime": 0,
  
  "range": 0,
  "aoe": null,
  
  "baseDamage": 15,
  "scaling": { "weapon": 0.4, "ap": 0.2 },
  
  "effects": [],
  
  "description": "Passive: 15% chance to counter-attack after being hit.",
  "animationKey": "riposte_counter"
}
```

**Notes:**
- Same as Warrior riposte
- Adds RNG element

---

### 3. Fortitude (Toggle)
```json
{
  "id": "bruiser_fortitude",
  "name": "Fortitude",
  "class": "bruiser",
  "type": "buff",
  
  "manaCost": 0,
  "cooldown": 0,
  "castTime": 0,
  
  "range": 0,
  "aoe": null,
  
  "baseDamage": 0,
  "scaling": {},
  
  "effects": [
    { "type": "armor_buff", "value": 30, "duration": null },
    { "type": "damage_penalty", "value": -20, "duration": null }
  ],
  
  "description": "Toggle stance. +30% armor, -20% damage output.",
  "animationKey": "fortitude_stance"
}
```

**Notes:**
- Toggle
- Less defensive than Warrior (30% vs 50%)
- But less damage penalty (-20% vs -30%)
- Hybrid balance

---

### 4. Whirlwind
```json
{
  "id": "bruiser_whirlwind",
  "name": "Whirlwind",
  "class": "bruiser",
  "type": "attack",
  
  "manaCost": 35,
  "cooldown": 4500,
  "castTime": 1500,
  
  "range": 0,
  "aoe": 3,
  
  "baseDamage": 35,
  "scaling": { "weapon": 1.0, "ap": 0.5 },
  
  "effects": [],
  
  "description": "Spin in place, hitting all enemies in 3x3 radius. Multi-hit.",
  "animationKey": "whirlwind_spin"
}
```

**Notes:**
- AOE (centered on self)
- 1.5s casting
- Medium mana cost
- Group damage tool

---

### 5. Last Stand
```json
{
  "id": "bruiser_last_stand",
  "name": "Last Stand",
  "class": "bruiser",
  "type": "buff",
  
  "manaCost": 0,
  "cooldown": 10000,
  "castTime": 500,
  
  "range": 0,
  "aoe": null,
  
  "baseDamage": 0,
  "scaling": {},
  
  "effects": [
    { "type": "damage_reduction", "value": 75, "duration": 3000 },
    { "type": "immobilize", "value": true, "duration": 3000 }
  ],
  
  "description": "Brace for impact. Reduce all damage by 75% for 3s but can't move.",
  "animationKey": "last_stand_brace"
}
```

**Notes:**
- 0.5s casting (fast)
- Powerful defensive tool
- Trade-off: immobile
- 10s cooldown = limited uses

---

## 🎮 COMBO EXAMPLES

| Combo | Effect | Timing |
|-------|--------|--------|
| Charge + Bash | Knockback to position + stun | Charge to target, immediately bash |
| Teleport + Fireball | Safe ranged burst | Teleport away, cast Fireball |
| Sprint + Multi Shot | Mobile burst | Sprint towards enemy, Multi Shot |
| Divine Shield + Holy Nova | Group immune + heal | Cast shield on tank, heal everyone |
| Smash + Whirlwind | AOE cleanup | Smash knockback into Whirlwind |

---

## ⚖️ BALANCE FRAMEWORK

### DPS Calculation
```
DPS = (baseDamage + weaponDamage * scaling) / cooldown_seconds

Example:
Slash: (25 + 20*1.0) / 1 = 45 DPS (baseline)
Fireball: (60 + 20*1.2) / 4 = 24 DPS (burst potential)
```

### Mana Efficiency
```
Efficiency = healingOrDamage / manaCost

Example:
Heal: 60 healing / 40 mana = 1.5 eff
Holy Nova: 40 healing / 60 mana = 0.67 eff (but AOE tax)
```

### Cooldown Density
```
Total skill cooldowns should average ~6-8 seconds
Ensures downtime between big plays
```

---

## 🔄 SKILL INTERACTIONS

### Interruption Mechanics
- **Casting interrupted by:** Knockback, Stun, Death
- **Can be interrupted:** Any skill with castTime > 0
- **Cannot interrupt:** Passive skills, toggle skills

### Damage Type System (Future)
```
Physical > Magic > None
Warrior → Mage (physical weak to magic)
Mage → Ranger (magic weak to physical)
Ranger → Warrior (physical > ranged)
```

---

**This document will be updated as balancing data comes in from playtesting.**
