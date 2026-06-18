# 📚 MMORPG PROJECT DOCUMENTATION INDEX

**Project:** MMORPG Medieval Skill-Based (Shangri-La Frontier inspired)  
**Status:** Design Phase - Ready for Development  
**Last Updated:** June 2026

---

## 🎯 QUICK START

**New to the project?** Read in this order:

1. **README_FIRST.md** (This document) - Start here
2. **MMORPG_GDD.md** - Understand the game vision
3. **SERVER_ARCHITECTURE.md** - How the backend works
4. **DEVELOPMENT_ROADMAP.md** - What to build first
5. **SKILL_DEFINITIONS.md** - All game mechanics
6. **GAME_CONFIG.md** - Balance constants

---

## 📖 DOCUMENT DESCRIPTIONS

### 1. **MMORPG_GDD.md** - Game Design Document
**What:** Complete game design specification  
**Who:** Designers, developers, anyone learning the game  
**Size:** ~20 pages  
**Read if:** You want to understand the full game concept

**Contains:**
- Vision & Pillars
- 5 Classes with all 25 skills
- Combat system mechanics
- World zones & PvP rules
- Death & loot system
- Equipment & progression
- Guild system
- Dungeons & content
- Chat & communication
- Security & anti-cheat
- Build philosophy (Shangri-La vibes)

**Key Decisions Made:**
- ✅ Skill-based progression (equipment = progression)
- ✅ Hardcore loot (ALL items dropable on death)
- ✅ Cel-shading visuals (anime style)
- ✅ Fixed skills per class (but highly variable playstyle)
- ✅ Hybrid server validation (responsive + secure)

---

### 2. **SERVER_ARCHITECTURE.md** - Technical Blueprint
**What:** How the server works, socket events, data flow  
**Who:** Backend developers, DevOps  
**Size:** ~15 pages  
**Read if:** You're building the Node.js backend

**Contains:**
- System architecture diagram
- Project structure (files & folders)
- Socket.io events (client ↔ server)
- Main game loop (20 Hz)
- Database schema (collections)
- Data flow examples (combat)
- Security validation checklist
- Performance targets
- Deployment phases

**Key Technical Decisions:**
- ✅ Node.js + Express.js
- ✅ Socket.io for real-time sync
- ✅ 20 Hz server tick rate (50ms)
- ✅ Hybrid validation (client predict, server authority)
- ✅ SQLite (dev) → MongoDB (prod)

---

### 3. **SKILL_DEFINITIONS.md** - Game Mechanics Bible
**What:** Every skill with exact stats, cooldowns, effects  
**Who:** Game designers, balance testers, developers  
**Size:** ~25 pages  
**Read if:** You need to implement skills or understand balance

**Contains:**
- Skill structure template
- Warrior skills (5 total): Slash, Shield Bash, Defensive Stance, Charge, Riposte
- Mage skills (5 total): Fireball, Frost Bolt, Teleport, Mana Shield, Arcane Missiles
- Ranger skills (5 total): Shot, Multi Shot, Evasion, Sprint, Power Shot
- Healer skills (5 total): Heal, Holy Nova, Divine Shield, Resurrect, Cleanse
- Bruiser skills (5 total): Smash, Riposte, Fortitude, Whirlwind, Last Stand
- Combo examples
- Balance framework (DPS, efficiency calculations)
- Skill interactions

**Key Balance Info:**
- All 5 skills are class-fixed (no customization MVP)
- But playstyle is 100% customizable (hotbar order, timing, combos)
- DPS balanced across classes (~40-50 DPS baseline)
- Counter-play exists (Rock-Paper-Scissors style)

---

### 4. **DEVELOPMENT_ROADMAP.md** - Build Order & Timeline
**What:** Phase-by-phase breakdown of what to build first  
**Who:** Project managers, developers  
**Size:** ~20 pages  
**Read if:** You're planning the development timeline

**Contains:**
- 8 development phases (Week 1-14 to MVP)
- Phase 0: Design & Setup
- Phase 1: Core Server
- Phase 2: Combat System
- Phase 3: Monsters & Loot
- Phase 4: PvP & Death System
- Phase 5: Client Graphics
- Phase 6: Dungeons & Group Play
- Phase 7: Guild System
- Phase 8: Polish & Testing
- MVP feature checklist
- Risk mitigation
- Post-MVP roadmap (v0.2 → v1.0)

**Development Estimate:**
- **MVP:** 14 weeks (1 developer)
- **Phase 0:** 1 week (setup)
- **Phase 1-2:** 3 weeks (core gameplay)
- **Phase 3-4:** 2 weeks (progression)
- **Phase 5:** 2 weeks (graphics)
- **Phase 6-7:** 2 weeks (content)
- **Phase 8:** 2 weeks (polish)

**Success Criteria:**
- 10 concurrent players
- No crashes in 2+ hour sessions
- Combat responsive (<200ms lag)
- Emergent gameplay from loot economy

---

### 5. **GAME_CONFIG.md** - Balance Constants & Configuration
**What:** All configurable numbers in one place  
**Who:** Balance designers, ops  
**Size:** ~15 pages  
**Read if:** You need to tweak game balance or understand all numbers

**Contains:**
- World configuration (map size, zones, movement)
- Combat configuration (damage, effects, death)
- Skill configuration (mana regen, class modifiers)
- Economy configuration (loot, durability, farming limits)
- Player progression (base stats)
- Guild configuration (size limits, bank)
- Dungeon configuration (Goblin Caves)
- Monster configuration (stats per type)
- Chat configuration (rate limits)
- Performance targets
- Security thresholds
- Debug settings

**How to Use:**
All these numbers are in `constants.js`:
- Easy to change without rewriting code
- Supports live-update after restart
- Includes `.env.example` for environment variables

**Example Balance Change:**
```javascript
// To buff Mage by 10%:
// In CLASS_MODIFIERS.mage
// Change: damage: 0.95 → damage: 1.05
// Restart server = 10% mage buff applied globally
```

---

## 🗂️ FILE ORGANIZATION

```
Project Root/
├── docs/                          (These documentation files)
│   ├── README_FIRST.md            (This file)
│   ├── MMORPG_GDD.md              (Full game design)
│   ├── SERVER_ARCHITECTURE.md     (Technical blueprint)
│   ├── SKILL_DEFINITIONS.md       (All mechanics)
│   ├── DEVELOPMENT_ROADMAP.md     (Build timeline)
│   └── GAME_CONFIG.md             (Balance constants)
│
├── backend/
│   ├── src/
│   │   ├── server.js              (Entry point)
│   │   ├── managers/              (Game logic)
│   │   │   ├── WorldManager.js
│   │   │   ├── PlayerManager.js
│   │   │   ├── CombatEngine.js
│   │   │   ├── SkillSystem.js
│   │   │   ├── LootSystem.js
│   │   │   ├── GuildSystem.js
│   │   │   ├── MonsterManager.js
│   │   │   └── ZoneManager.js
│   │   ├── models/
│   │   │   ├── Player.js
│   │   │   ├── Monster.js
│   │   │   ├── Item.js
│   │   │   ├── Guild.js
│   │   │   └── World.js
│   │   ├── utils/
│   │   │   ├── constants.js       (← Use GAME_CONFIG.md to update)
│   │   │   ├── validators.js
│   │   │   ├── helpers.js
│   │   │   └── logger.js
│   │   └── config/
│   │       ├── skills.json
│   │       ├── monsters.json
│   │       └── database.js
│   ├── tests/
│   │   ├── combat.test.js
│   │   ├── skills.test.js
│   │   └── loot.test.js
│   ├── package.json
│   ├── .env.example               (← Template from GAME_CONFIG.md)
│   └── README.md
│
├── frontend/
│   ├── src/
│   │   ├── App.jsx
│   │   ├── pages/
│   │   │   ├── Login.jsx
│   │   │   ├── CharacterCreation.jsx
│   │   │   ├── Game.jsx
│   │   │   └── UI/
│   │   ├── components/
│   │   │   ├── GameWorld.jsx      (Phaser/Babylon.js)
│   │   │   ├── Hotbar.jsx
│   │   │   ├── ChatPanel.jsx
│   │   │   ├── InventoryPanel.jsx
│   │   │   └── Stats.jsx
│   │   └── socket/
│   │       └── client.js
│   ├── package.json
│   └── README.md
│
└── README.md                      (Top-level project overview)
```

---

## 🔄 HOW TO USE THESE DOCUMENTS

### For Game Designers
1. Read **MMORPG_GDD.md** - Understand the vision
2. Read **SKILL_DEFINITIONS.md** - Learn all mechanics
3. Use **GAME_CONFIG.md** to balance
4. Update **SKILL_DEFINITIONS.md** when balance changes

### For Backend Developers
1. Read **SERVER_ARCHITECTURE.md** - Understand the system
2. Read **GAME_CONFIG.md** - Learn the numbers
3. Implement following **DEVELOPMENT_ROADMAP.md** phases
4. Reference **SKILL_DEFINITIONS.md** when coding skills

### For Frontend Developers
1. Skim **MMORPG_GDD.md** - Know what you're building
2. Read **SERVER_ARCHITECTURE.md** - Understand Socket.io events
3. Read **SKILL_DEFINITIONS.md** - Learn animations & effects
4. Reference **DEVELOPMENT_ROADMAP.md** Phase 5

### For Project Managers
1. Read **DEVELOPMENT_ROADMAP.md** - Know the timeline
2. Reference phases weekly
3. Track completion using the checklist
4. Adjust scope if needed (defer to post-MVP)

### For Balance Testers
1. Read **SKILL_DEFINITIONS.md** - Know the expected stats
2. Read **GAME_CONFIG.md** - Learn balance math
3. Playtest following **DEVELOPMENT_ROADMAP.md** Phase 8
4. Document findings & update docs

---

## 🎮 KEY GAME MECHANICS AT A GLANCE

### Combat System
- **Type:** Hotbar-based with cooldowns & mana
- **Classes:** 5 (Warrior, Mage, Ranger, Healer, Bruiser)
- **Skills:** 5 per class (25 total)
- **Casting:** Variable 0-2.5s (can be interrupted)
- **Resources:** Mana (regen) + Cooldowns (per skill)
- **Validation:** Server-side authoritative with client prediction

### Progression
- **Type:** Skill-based (equipment = progression)
- **Leveling:** None (MVP) - equipment-based only
- **Skills:** Equip weapon → use skill → skill progresses
- **Scaling:** Weapon damage + ability power modifiers

### PvP & Zones
- **Town (White):** Safe, no PvP
- **Countryside (Yellow):** PvP enabled, moderate risk
- **Dark Forest (Red):** Hardcore PvP, all-or-nothing
- **Death Penalty:** Lose ALL items (durability -30%), revive in Town
- **Loot Mechanics:** Killer gets 10s exclusivity, then free-for-all

### Economy
- **Income:** Monster loot, PvP kills, dungeons
- **Sinks:** Repair (30% of item value), guild taxes
- **Farming Limit:** Energy system (10 energy/kill, regen 1/min)
- **Anti-RMT:** Durability degradation, respawn timers

### Content (MVP)
- **Monsters:** 3-5 types (goblin, wolf, troll, etc)
- **Dungeons:** 1 (Goblin Caves, 5 players)
- **NPCs:** Guard, Merchant, Banker, Guild Master
- **World:** 3 zones (Town, Countryside, Dark Forest)

### Groups
- **Parties:** Max 5 players
- **Guilds:** Max 100 members, 3 officers, guild bank, chat
- **PvP Wars:** Future (v0.2+)

---

## 🚀 NEXT STEPS (START HERE!)

### Step 1: Setup (Day 1)
- [ ] Clone GitHub repository (or create new)
- [ ] Read MMORPG_GDD.md completely
- [ ] Review DEVELOPMENT_ROADMAP.md
- [ ] Setup Node.js environment locally

### Step 2: Phase 0 (Days 2-4)
- [ ] Create project structure (see FILE ORGANIZATION above)
- [ ] Setup Express.js + Socket.io
- [ ] Setup SQLite database locally
- [ ] Create constants.js from GAME_CONFIG.md
- [ ] Create basic player model

### Step 3: Phase 1 (Days 5-12)
- [ ] Implement WorldManager
- [ ] Implement PlayerManager
- [ ] Setup Socket.io events
- [ ] Create player movement validation
- [ ] Implement world state broadcasting
- [ ] Test with 5 players locally

### Step 4: Phase 2 (Days 13-21)
- [ ] Implement CombatEngine
- [ ] Implement SkillSystem
- [ ] Create all 25 skills
- [ ] Test damage calculation
- [ ] Test cooldowns & mana

**Continue following DEVELOPMENT_ROADMAP.md for remaining phases...**

---

## 📊 REFERENCE TABLE

| Document | Purpose | Size | Priority |
|----------|---------|------|----------|
| MMORPG_GDD.md | Full game design | 20pg | ⭐⭐⭐⭐⭐ |
| SERVER_ARCHITECTURE.md | Technical blueprint | 15pg | ⭐⭐⭐⭐⭐ |
| SKILL_DEFINITIONS.md | Game mechanics | 25pg | ⭐⭐⭐⭐ |
| DEVELOPMENT_ROADMAP.md | Build timeline | 20pg | ⭐⭐⭐⭐ |
| GAME_CONFIG.md | Balance constants | 15pg | ⭐⭐⭐ |

---

## ❓ COMMON QUESTIONS

### Q: Can I change the design?
**A:** YES! The documents are guidelines. If you have a better idea, document it and move forward. The most important thing is shipped > perfect.

### Q: What if my team disagrees on balance?
**A:** Use SKILL_DEFINITIONS.md as the baseline. Test it. Data beats opinions. Update docs when you change something.

### Q: How often should I update these docs?
**A:** 
- After major design decisions
- After playtesting reveals balance issues
- Before each phase starts
- Never let docs get too stale

### Q: Can I defer features to post-MVP?
**A:** **YES!** MVP scope is STRICT. Crafting, gathering, quests, trading → all v0.2+. Focus on core gameplay first.

### Q: How do I know if something is "in scope"?
**A:** Check DEVELOPMENT_ROADMAP.md Phase 8. If it's not there, it's post-MVP. Only exception: critical bugs.

---

## 🎓 RECOMMENDED LEARNING PATH

**Week 1:**
- Day 1-2: Read MMORPG_GDD.md
- Day 3: Read SERVER_ARCHITECTURE.md
- Day 4-5: Read SKILL_DEFINITIONS.md
- Day 6-7: Read DEVELOPMENT_ROADMAP.md

**Week 2:**
- Start Phase 0 (setup)
- Reference GAME_CONFIG.md as you code

**Ongoing:**
- Reference SKILL_DEFINITIONS.md when implementing skills
- Use GAME_CONFIG.md for all balance numbers
- Check DEVELOPMENT_ROADMAP.md weekly for progress

---

## 📞 DOCUMENT MAINTENANCE

**Created:** June 2026  
**Last Updated:** June 2026  
**Version:** 1.0 (Design Phase)

**Updates Needed When:**
- [ ] Game mechanics change
- [ ] Skill values shift
- [ ] New zones/dungeons added
- [ ] Balance changes applied
- [ ] Architecture decisions change

---

## 🎉 YOU'RE READY!

These 5 documents contain **everything** you need to build this MMORPG.

**Next step:** Open **DEVELOPMENT_ROADMAP.md** and start Phase 0! 🚀

---

**Questions? Ambiguities?** These docs are living. Update them as you learn more.

**Good luck, developer!** Let's build something legendary 🎮⚔️
