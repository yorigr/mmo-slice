# Combat & Progression Reference
## Albion Online + RuneScape → Nosso MMORPG

> Este documento é o resultado de um estudo das referências do projeto.
> Cada seção termina com "→ Nossa Decisão" — como aquela mecânica se traduz
> respeitando os pilares do PROJECT_MANIFESTO.md.

---

## PARTE 1 — DINÂMICA DE COMBATE: ALBION ONLINE

### 1.1 Como o Albion estrutura habilidades

Em Albion, **o equipamento é a classe**. Não existe classe permanente:

```
SLOT Q (Weapon) — habilidade básica / poke
SLOT W (Weapon) — habilidade secundária / trade
SLOT E (Weapon) — ultimate / ultimate do tipo de arma

SLOT R (Chest armor)  — habilidade defensiva / ofensiva de suporte
SLOT D (Head armor)   — habilidade de sobrevivência
SLOT F (Boots)        — habilidade de mobilidade / posicionamento

+ 1 passiva por peça de armadura (exceto peito, que dá 2)
```

O resultado: **6 habilidades ativas** com possibilidades combinatórias enormes.
Um jogador com Frost Staff (stun em cone) + Plate Armor (absorb) + Soldier Boots (dash)
joga completamente diferente de outro com Frost Staff + Leather Armor (sprint) + Mage Boots (blink).

**→ Nossa Decisão:**
Mantemos as 5 classes como identidade e ponto de entrada (manifesto pede curva suave).
Mas ampliamos o sistema de habilidades adicionando **armas como item equipável** que substituem
a skill de slot E (habilidade especial de arma). Assim:
- Warrior sempre tem Slash/ShieldBash/Charge como base
- Mas a ARMA equipada define a habilidade E: Longsword (dash+stun), Battleaxe (cleave AoE), Halberd (knockback de área)
- Isso cria builds dentro da classe, não entre classes — alinhado ao "Akatsuki Principle"

---

### 1.2 Tipos de Efeito de Controle (CC) no Albion

Albion tem o CC system mais rico de MMORPG sandbox. Estudo completo:

| Tipo CC | O que faz | Interrompe cast? | Impede movimento? | Impede habilidades? |
|---------|-----------|-----------------|-------------------|---------------------|
| **Stun** | Para tudo | ✅ Sim | ✅ Sim | ✅ Sim |
| **Root** | Enraíza no lugar | ❌ Não | ✅ Sim | ❌ Não (pode usar skills) |
| **Slow** | Reduz velocidade | ❌ Não | ❌ Parcial | ❌ Não |
| **Silence** | Bloqueia habilidades | ✅ Sim | ❌ Não | ✅ Sim |
| **Knockback** | Empurra em direção oposta | ✅ Interrompe | ✅ Move forçado | ✅ Sim |
| **Knockup** | Joga para cima (breve) | ✅ Sim | ✅ Sim | ✅ Sim |
| **Displacement** | Puxa/empurra para posição específica | ✅ Sim | ✅ Sim | ✅ Sim |
| **Fear** | Move em direção aleatória | ✅ Sim | ✅ Move forçado | ✅ Sim |
| **Chill** | Versão suave de slow (Frost) | ❌ Não | ❌ Parcial | ❌ Não |

**CC Chains (encadeamento):** O que torna Crystal GvG tão rico é que grupos treinam para encadear CC:
```
Hammer (Stun AoE) → Frost (Root no stun) → Mage burst DPS → Healer cleanse do próprio time
```
Sem o cleanse do próprio time, a chain seria quebrada pelo healer inimigo.

**Diminishing Returns (DR):** Albion aplica redução de duração em CC repetido no mesmo alvo.
Mesmo CC aplicado 2x → 50% da duração. 3x → 25%. Impede que uma pessoa seja stunada para sempre.

**→ Nossa Decisão:**
Implementar 4 tipos de CC na próxima iteração de combat:
- **Stun** — interrompe tudo por duração curta (0.8-1.5s). Poucas skills têm isso, alto cooldown.
- **Root** — prende no lugar, pode usar skills. Ranger e Mage têm access.
- **Slow** — já existente no servidor, expandir para todas as classes relevantes.
- **Knockback** — Bruiser/Warrior como ferramenta de posicionamento.

Silence fica para versão futura (requer mais UI para feedback).
DR será implementado no servidor: mesmo CC no mesmo alvo → duração × 0.5.

---

### 1.3 Tipos de Debuff (não-CC) no Albion

| Debuff | Efeito | Quem usa |
|--------|--------|----------|
| **Defense Reduction** | Alvo toma mais dano físico % | Spears, Axes, Bows |
| **Healing Reduction (Anti-heal)** | Reduz curas recebidas % | Fire, Crossbow, Axes, Gloves |
| **Max HP Reduction** | Reduz HP máximo (mata players com menos HP total) | Maces, Realmbreaker Axe |
| **Damage Amplification** | Alvo toma mais dano de qualquer fonte | Cursed Staffs |
| **Energy Drain** | Drena mana/energy do alvo | Alguns Arcane Staffs |
| **Bleed** | Dano over time, não interrompido por cura | Axes, alguns Daggers |
| **Poison** | Dano over time, curable | Nature Staffs (ofensivo) |
| **Burn** | Dano over time + debuff de mobilidade | Fire Staffs |

**→ Nossa Decisão:**
Para próxima iteração, adicionar ao servidor:
- **Healing Reduction** (debuff "Grievous Wounds") — aplicado por Bruiser e Ranger. Crítico para contra-play de Healer dominante.
- **Defense Reduction** — aplicado por alguns ataques de Warrior. Torna o Warrior "tank buster" além de tanker.
- **Bleed (DoT)** — já temos DoT no servidor? Verificar CombatEngine.js. Adicionar tipo específico que não é removido por heal.

---

### 1.4 Buffs e Cleanses

| Buff | Efeito |
|------|--------|
| **Shield** | Absorbe X dano antes de tirar HP |
| **Immunity** | Invulnerável por duração curta (Great Arcane) |
| **Speed Boost** | +% velocidade de movimento |
| **Heal Over Time (HoT)** | Cura progressiva ao longo do tempo |
| **Damage Boost** | +% dano em ataques |
| **CC Immunity** | Não pode receber CC por duração |

| Cleanse Type | O que remove |
|-------------|-------------|
| **Cleanse** | Remove todos os CC e debuffs do alvo |
| **Purge** | Remove todos os buffs de um INIMIGO |
| **Dispel** | Remove efeitos mágicos específicos |

**→ Nossa Decisão:**
- **Cleanse** fica com Healer (Holy Nova → adicionar cleanse no hit em aliados próximos)
- **Purge** fica com Mage (novo uso para Mana Shield → quando ativo e atingido, purga 1 buff do atacante)
- Warrior e Bruiser ganham **CC Immunity** de curta duração como parte de habilidades defensivas (Defensive Stance / Fortitude = imune a root/slow enquanto ativo)

---

### 1.5 Crystal GvG — O que faz esse modo ser o mais competitivo

**Formato:**
- **5v5** (batalha de guilda por território) ou **20v20** (conquista de cidades)
- Cada time começa com **150 pontos**
- Times só PERDEM pontos, nunca ganham
- Primeiro a chegar em 0 perde
- **Full loot** — você perde o que vestia se morrer

**Estrutura do mapa:**
- 3 pontos de captura (pedras/altares)
- Capturar um ponto = drena pontos do inimigo mais rápido
- Kills também drenam pontos
- Times que perdem um membro ficam 4x1 ou 3v2 — momentum muda radicalmente

**Por que Crystal GvG cria jogabilidade única:**
```
1. Item Power Cap suave (1000 IP, acima disso conta 50%)
   → skill > gear mesmo em full BiS
   
2. Composições de grupo com papéis distintos:
   - Initiator (Tank com stun AoE para iniciar)
   - DPS burst (DPS que mata em 2s quando o stun conecta)
   - Healer (mantém o time vivo, cleanse de CC)
   - Peeler (protege o healer de flanks)
   - Support (buffs, anti-heal, displacement)
   
3. Timing de cooldowns:
   - O stun AoE tem 30s de cooldown
   - O burst DPS tem 15s de cooldown
   - Uma janela de 5s no inicio do fight = decide tudo
   - Equipes que wastam o stun em 1 target e não matam = perdem o fight
   
4. Posicionamento:
   - Pontos de captura = pressão constante de território
   - Stuns em cone devem ser direcionados corretamente
   - Healing Staffs em AoE só alcançam o time se estiver agrupado
   - Split-push vs. teamfight é uma decisão constante
```

**→ Nossa Decisão (não implementar Crystal GvG agora, mas projetar para ele):**

O sistema de zonas que já temos (ZoneManager com rooms Socket.IO) é a fundação correta.
Cada zona pode se tornar um "campo de batalha" com:
- Pontos de captura no mapa (3 posições pré-definidas por zona)
- Timer de duração de batalha
- Sistema de pontos por captura + kill
- Full loot enforcement nessas zonas

Isso vira o **modo Guild War** quando tivermos guilds implementadas.
Por enquanto: as zonas vermelhas do GDD já têm PvP pleno e full loot como filosofia.

---

## PARTE 2 — GATHERING E CRAFTING: RUNESCAPE

### 2.1 Como RuneScape estrutura Gathering Skills

RuneScape tem **23 skills de non-combat**, cada uma com XP própria de 1 a 99 (120 no RS3).
As mais relevantes para nosso escopo:

| Skill | O que coleta | Materiais chave |
|-------|-------------|-----------------|
| **Mining** | Minérios de pedra e metal | Bronze Ore, Iron, Coal, Mithril, Adamantite |
| **Woodcutting** | Madeira de árvores | Normal, Oak, Willow, Maple, Yew, Magic |
| **Fishing** | Peixes para culinária | Shrimp, Trout, Salmon, Lobster, Shark |
| **Farming** | Plantas, ervas mágicas | Herbs (base de potions), Grain, Vegetables |
| **Hunting** | Animais e criaturas | Pelts, Feathers, Bones (crafting e prayer) |
| **Runecrafting** | Runas para magias | Air, Water, Earth, Fire, Chaos, Nature runes |
| **Thieving** | Roubo de NPCs/baús | Gold, Gems, Lockpicks |

**O loop econômico de RuneScape:**
```
Mining lvl 55 → Mithril Ore
+ Smithing lvl 50 → Mithril Bar
+ Smithing lvl 55 → Mithril Sword
→ Vende na Grand Exchange por 2x o custo de materials
```

Cada link da cadeia tem um skill com XP independente.
Isso cria **especializações**: um player pode ser Miner/Smithing expert e vender para
Warriors que preferem jogar o jogo sem craftar.

### 2.2 Como RuneScape estrutura Crafting Skills

| Skill | O que produz | Inputs |
|-------|-------------|--------|
| **Smithing** | Armas e armaduras de metal | Metal bars (do Mining) |
| **Crafting** | Couro e jóias | Leather (do Hunter/loots), Gems |
| **Herblore** | Potions de combate e suporte | Herbs (do Farming) + secondary |
| **Fletching** | Arcos e flechas | Logs (do Woodcutting) + Feathers |
| **Cooking** | Comida que cura HP | Peixes (do Fishing), Ingredients |
| **Construction** | Casas e objetos | Planks (do Woodcutting processado) |
| **Runecrafting** | Runas para spells | Pure Essence + altares |

**O que torna isso rico:**
- Cada skill tem **milestone gates**: você não pode craftar Mithril Sword sem Smithing 55
- Items craftados por players são geralmente melhores que drops (incentiva economia)
- Potions de Herblore criam dependência: Warriors querem Strength Potions, precisam de players com Herblore alto
- Economia player-driven: ninguém é auto-suficiente em tudo

### 2.3 Eventos de Mundo do RuneScape (World Events)

RuneScape tem eventos que transformam o mundo temporariamente:

| Evento | Mecânica | O que cria |
|--------|----------|-----------|
| **God Wars Bosses** | Boss poderoso em ponto fixo do mapa | Congregação de players, PvP natural nos arredores (safe/unsafe border) |
| **Shooting Stars (OSRS)** | Meteoro cai em posição aleatória do mapa | Corrida de players para minerar o estrela antes de outros |
| **Evil Tree (OSRS)** | Árvore maligna cresce em posição aleatória | Players cooperam para destruí-la, recebem Woodcutting XP |
| **Wyrm Attacks** | Criaturas invadem zonas urbanas periodicamente | Defesa cooperativa da cidade, loot especial |
| **Wilderness Meteor** | Meteoro cai na Wilderness (zona PvP) | Risco extremo de ir coletar, reward de raro material |

**O que esses eventos fazem de importante:**
- Criam **concentration points** no mapa — players se encontram naturalmente
- Geram **conflito orgânico** — quem vai ser o primeiro a pegar o drop do boss?
- Adicionam **urgência** — o evento some em X minutos
- São **previsíveis mas não determinísticos** — Shooting Star cai em uma das 20 possíveis locais

---

## PARTE 3 — NOSSA SÍNTESE: DECISÕES DE DESIGN

### 3.1 Sistema de Skills de Vida (Gathering + Crafting)

Cada atividade tem sua própria XP e nível (1-100, como RuneScape):

**Gathering Skills:**
| Skill | Fonte | Material coletado |
|-------|-------|------------------|
| `mining` | Veios de minério no mundo | Iron Ore, Coal, Mithril Ore, Adamantite Ore |
| `woodcutting` | Árvores no mapa | Oak Log, Birch Log, Darkwood Log |
| `herbalism` | Plantas em campos/pântanos | Healing Herb, Mana Root, Poison Ivy |
| `hunting` | Criaturas específicas com armadilha | Animal Pelt, Feathers, Bones |
| `fishing` | Pontos de pesca em lagos/rios | Raw Fish (heal), Deep Fish (mana), Ancient Fish (DoT resist) |

**Crafting Skills:**
| Skill | Input | Output |
|-------|-------|--------|
| `smithing` | Metal Ores (mining) | Weapons, Armor (metal) |
| `leatherwork` | Animal Pelts (hunting) | Leather Armor, Quivers, Pouches |
| `alchemy` | Herbs + Fish (herbalism + fishing) | HP Potions, Mana Potions, Buff Potions |
| `fletching` | Wood Logs + Feathers | Arrows, Bows (Ranger weapons) |
| `runecrafting` | Crystal Shards (drop raro) | Rune Scrolls (consumíveis de skill extra) |

**Implementação no servidor:**
- Cada player tem `skills: { mining: { level: 1, xp: 0 }, smithing: {...}, ... }`
- Persistido no PlayerManager (antes do banco de dados, em memória por sessão)
- Nível determina o que pode coletar/craftar (gate como RuneScape)
- XP por ação vai para a skill correta, não para o combat XP

### 3.2 Sistema de Combate Expandido (Albion-inspired)

**Tipos de efeito a implementar no CombatEngine:**

```javascript
// Status Effects System
const STATUS_EFFECTS = {
  // CC
  stun:      { duration: 1200, preventsCast: true,  preventsMove: true  },
  root:      { duration: 2000, preventsCast: false, preventsMove: true  },
  slow:      { factor: 0.5,    preventsCast: false, preventsMove: false },
  knockback: { distance: 3,    preventsCast: true,  preventsMove: true  }, // forçado

  // Debuffs
  antiHeal:  { factor: 0.5,    duration: 4000  }, // curas reduzidas 50%
  defenseDown: { factor: 0.2,  duration: 3000  }, // toma 20% mais dano
  bleed:     { dps: 5,         duration: 5000, uncurable: true },

  // Buffs
  shield:    { value: 50 },                        // absorve X dano
  haste:     { factor: 1.5,    duration: 3000  },  // +50% velocidade
  ccImmune:  { duration: 1500 },                   // imune a CC
  cleanse:   { removes: ['stun','root','slow','antiHeal','defenseDown'] },
};

// Diminishing Returns
const DR_TABLE = { 1: 1.0, 2: 0.5, 3: 0.25, 4: 0 };
// player.ccStack[effectType] incrementa a cada aplicação, decai após 15s
```

**Skills expandidas por classe (com novos efeitos):**

**WARRIOR:**
- `Slash` — dano + aplica `defenseDown` por 3s (abre target para burst)
- `Shield Bash` — `stun` 1s (curto mas poderoso, high cooldown 20s)
- `Defensive Stance` — toggle ativo: `ccImmune` contínuo, -30% dano dealt
- `Charge` — dash + `knockback` no destino
- `Riposte` — passiva: 15% chance counter; ao counter aplica `slow` 1.5s

**MAGE:**
- `Fireball` — AoE dano + `burn` (DoT 3s)
- `Frost Bolt` — dano + `root` 2s (★ core CC do Mage)
- `Teleport` — blink de escape + cleanse de 1 CC ao ativar (custo: só 1 CC removido)
- `Mana Shield` — toggle: converte dano em mana cost; enquanto ativo: `purge` 1 buff do atacante
- `Blizzard` (nova skill): AoE canal, aplica `slow` crescente (40%→80%) em área

**RANGER:**
- `Shot` — dano + leve `slow` (kiting tool)
- `Multi Shot` — 3 projéteis em cone, cada um pode aplicar `antiHeal` 2s
- `Evasion` — passiva 20% dodge; quando ativa (novo toggle): +20% velocidade + `ccImmune` 3s (cooldown 25s)
- `Barbed Arrow` (nova skill): `bleed` no alvo (DoT uncurable, 5 dano/s por 6s)
- `Sprint` — toggle velocidade; novo: ao desativar Sprint aplica `root` em quem estiver no raio de 1u

**HEALER:**
- `Heal` — cura single target + remove 1 CC (`cleanse` parcial)
- `Holy Nova` — AoE heal + remove CC de aliados no raio (`cleanse` AoE, forte)
- `Resurrect` — revive aliado (só fora de combate)
- `Divine Shield` — `ccImmune` + `shield` no target por 2s (melhor CC-breaker do jogo)
- `Wrath` (nova skill ofensiva): `antiHeal` no inimigo por 5s (Healer pode punir outros healers)

**BRUISER:**
- `Smash` — dano + `knockback` 2u
- `Riposte` — passiva counter 15%
- `Fortitude` — toggle: `ccImmune` + +30% armor (Bruiser é o mais difícil de stunkar)
- `Whirlwind` — AoE ao redor, aplica `slow` em todos atingidos
- `Ground Slam` (nova skill): `knockup` + dano AoE 3x3 (combina com burst do Mage)

### 3.3 Eventos de Mundo (World Events)

Eventos periódicos que criam concentração de jogadores:

| Evento | Trigger | Mecânica | Reward |
|--------|---------|----------|--------|
| **Field Boss** | A cada 30 min em zona amarela | Boss forte spawn, qualquer um pode atacar. Kill = loot partilhado por % de dano | Material raro para crafting |
| **Meteor Strike** | A cada 45 min em zona vermelha | Meteorito cai em posição aleatória. Contém minério raro. PvP natural para controlar | Adamantite Ore ou Crystal Shard |
| **Merchant Caravan** | A cada 60 min em rota definida | NPC Merchant traversa o mapa carregando materiais valiosos. Mata o NPC = loot everything | Bulk materials, rare recipes |
| **Monster Surge** | A cada 90 min em qualquer zona | Horda de monstros invade vila. Players cooperam. Falhar = vila "under siege" por 10min | XP bonus, Guild Points |

**Implementação no servidor:**
- `WorldEventManager` — timer-based, usa setInterval para checar
- Emite evento `world:event` para todos os players na zona
- Dados: `{ type, position, timeLeft, description }`
- Cliente mostra notificação na tela e marcador no mapa

---

## PARTE 4 — ROTEIRO DE IMPLEMENTAÇÃO

Com base neste estudo, a sequência correta de desenvolvimento:

### Fase Atual (Client Polish) ✅ em andamento
- RespawnPanel, ChatUI, FloatingText, PlayerNameTag, SkillBar — feitos

### Próxima Fase: Combat Depth (2-3 semanas)
1. **Status Effects no servidor** — `CombatEngine` recebe sistema de status effects (stun/root/slow/antiHeal/defenseDown/bleed/shield/cleanse)
2. **Skills expandidas** — 5 skills por classe ganham os novos efeitos listados acima
3. **Diminishing Returns** — implementação no servidor para anti-CC-chain infinita
4. **Feedback visual no cliente** — ícones de status effect no HUD (debuffs acima do HP bar)

### Fase Seguinte: Economia Viva
1. **Gathering nodes no WorldManager** — veios de minério, árvores, ervas no mapa como entities
2. **Skill de vida no PlayerManager** — `skills{}` object com XP independente
3. **Crafting recipes** — `recipes.json` + endpoint `item:craft`
4. **World Events básico** — Field Boss e Meteor Strike

### Fase GvG (futuro)
1. **Guild system** no servidor
2. **Territory control** nas zonas vermelhas
3. **5v5 Crystal-style mode** com pontos de captura e score system

---

## REFERÊNCIAS DESTE ESTUDO

- [Albion Online Spells Wiki](https://wiki.albiononline.com/wiki/Spells)
- [Albion Online Weapon Overview](https://itemlevel.net/an-overview-of-every-weapon-in-albion-online/)
- [Albion Online GvG Mechanics](https://wiki.albiononline.com/wiki/GvG_Mechanics)
- [Crystal Realm Battle Guide](https://forum.albiononline.com/index.php/Thread/99263)
- OSRS Wiki (Gathering/Crafting skills structure)

---

*Última atualização: Junho 2026*
*Próxima revisão: Após implementação de Status Effects*
