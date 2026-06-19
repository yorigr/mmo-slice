# Economia e Sistema de Gear — Referência de Design

> Baseado em pesquisa de Albion Online, EVE Online, RuneScape e Black Desert Online.
> Alinhado ao Manifesto do projeto: skill-based, hardcore but fair, sem P2W.

---

## 1. Sistema Gear-Based (sem classes fixas)

### 1.1 Conceito Central — "You are what you wear"

Inspirado diretamente no Albion Online: **não existem classes de personagem**.
Seu papel no combate é definido 100% pelo equipamento que você usa.
Trocar de arma = trocar de papel (dano, CC, cura, suporte).

**Benefícios para o nosso jogo:**
- Mais builds possíveis → meta nunca "resolvida" (Princípio Akatsuki ✓)
- Economia mais saudável: todas as categorias de item têm demanda
- Progressão por maestria + gear, não por grinding de nível

### 1.2 Slots de Equipamento e Skills

Cada peça de equipamento fornece skills ativas. O jogador ESCOLHE quais skills ativar
por slot (dentro das opções daquela peça). A seleção pode ser alterada a qualquer
momento fora de combate.

```
Equipamento      Slot de Skill   Hotkey
─────────────────────────────────────────
Arma  (slot Q)   weapon_Q        1
Arma  (slot W)   weapon_W        2
Arma  (slot E)   weapon_E        3
Peitoral (R)     chest_R         4
Capacete (D)     head_D          5
```

**Nota:** Botas fornecerão um 6º slot no futuro (hotkey 6). Adicionado quando o SkillBar suportar.

### 1.3 Famílias de Armas

| Família       | Estilo de Jogo          | Range | Especialidade                     |
|---------------|-------------------------|-------|-----------------------------------|
| `sword`       | Melee — CC/Burst        | 65px  | Stun, knockback, finisher         |
| `greataxe`    | Melee — AoE/Bleed       | 70px  | Bleed DoT, AoE, knockup           |
| `daggers`     | Melee — Veloz/Debuff    | 50px  | Bleed, defenseDown, blink         |
| `mace`        | Melee — Suporte/Tank    | 65px  | DefenseDown, knockback, fortify   |
| `hammer`      | Melee — AoE/Disrupção   | 70px  | Knockback, knockup, AoE slow      |
| `bow`         | Ranged — Kiting         | 400px | Slow, antiHeal, root, AoE         |
| `fire_staff`  | Magic — Burst AoE       | 350px | Burn DoT, AoE, canal devastador   |
| `frost_staff` | Magic — CC              | 320px | Root, slow, congelamento          |
| `arcane_staff`| Magic — Suporte/Debuff  | 300px | Cleanse, purge, pull, antiHeal    |
| `holy_staff`  | Magic — Cura/Suporte    | 300px | Heal, revive, ccImmune, shield    |

### 1.4 Tipos de Armadura

Três arquétipos por slot (capacete, peitoral, botas):

| Tipo   | Estilo              | Habilidades Típicas                         |
|--------|---------------------|---------------------------------------------|
| Pano   | Cura / Dano mágico  | Blink, cleanse, arcane shield, mana amp     |
| Couro  | Mobilidade / Dodge  | Evasion, sprint, CC immunity, rolling dodge |
| Placa  | Tank / CC próprio   | Damage reduction, shield, charge, iron will |

### 1.5 Skill Slots por Arma — Regra de Ouro

> Slots Q e W são **compartilhados** pela família de armas (todos os espadas têm os mesmos Q/W).
> Slot E é a **habilidade única** de cada arma específica dentro da família.
> Itens de mesmo tipo mas tier diferente têm os mesmos slots — apenas Item Power muda.

Exemplo:
```
Espada de Ferro  → Q: [slash | rend]    W: [shield_bash | heavy_blow]   E: execute
Espada de Aço    → Q: [slash | rend]    W: [shield_bash | heavy_blow]   E: charge
(mesmo pool Q/W, E diferente por ser outra espada específica)
```

---

## 2. Economia — Controle de Inflação

### 2.1 Por que inflação destrói MMORPGs

Referências de falha:
- **RuneScape** — "an absolute debacle" (Game Rant, 2024): ouro criado por quests/mobs sem sinks equivalentes causou hiperinflação. Resultado: duping exploits, RMT rampante, desvalorização de drops.
- **New World (lançamento)** — bugs de duplicação, inflação em semanas. Resultado: economia reiniciada, jogadores fugiram.

Referências de sucesso:
- **EVE Online** — contratou economista real (Dr. Eyjolfur Gudmundsson, 2007). Equilíbrio entre ISK faucets (bounties) e ISK sinks (transaction taxes, ship destruction). Publica quarterly economic reports. Funciona há 20 anos.
- **Albion Online** — sistema full loot + Black Market (NPC destrói itens para equilibrar drops) + Global Discount (ajusta taxa Silver→Gold dinamicamente contra inflação).
- **Guild Wars 2** — gem store (conversão moeda real→gemas→gold) como válvula reguladora. Funciona pois cosmetics ≠ power.

### 2.2 Princípio Faucet vs Sink

```
FAUCETS (criam moeda/itens):          SINKS (destroem moeda/itens):
─────────────────────────────         ────────────────────────────────
• Mobs dropam gold e itens            • Full loot em zonas vermelhas
• Quest rewards                       • Durability degrada → repair costs
• Venda de materiais para NPC         • Market tax (% por transação)
• Gathering resources                 • Crafting overhead (silver fee)
                                      • Guild creation cost
                                      • Morte em zona segura → perde 20% durability
```

**Regra de ouro (EVE/Albion):** sinks devem escalar com riqueza do jogador.
Taxas fixas (ex: "10 gold para reparar") se tornam insignificantes quando player tem milhões.
Taxas percentuais (ex: "5% do valor do item") funcionam em toda a vida do jogo.

### 2.3 Nossos Mecanismos Anti-Inflação

#### A. Durabilidade de Itens

- Toda peça de equipamento começa com `durability: 100`
- Perde 1 de durabilidade por hit recebido (em combate)
- `durability = 0` → item fica inutilizável até ser reparado
- **Custo de reparo:** `Math.ceil(item.value * (1 - durability/100) * 0.15)` silver
  - Item de 100 gold em 0% durabilidade: custa ~15 silver para reparar
  - Item de 1000 gold em 0% durabilidade: custa ~150 silver para reparar
  - Escala com valor → eficaz contra todos os tiers

#### B. Taxas de Mercado

- **5% de taxa sobre VENDA** (não listagem — reduz listing walls)
- Taxa vai para um "pool de queima" — removida do jogo
- Escala percentualmente → funciona em todos os tiers

#### C. Full Loot em Zonas PvP

- Zonas vermelhas e pretas: morte = perde TODO o equipamento e inventário
- Garante destruição constante de itens de alto tier
- Loop auto-regulador: mais PvP → mais destruição → mais demanda por crafting → mais gathering

#### D. Custo Overhead de Crafting

- Toda receita de craft exige um `silverCost` mínimo (custo de "ferramentas")
- `silverCost = Math.floor(item.value * 0.05)` (5% do valor do item final)
- Vai para sink — nunca para outro player
- Exemplo: craftar uma espada de 200 gold custa 10 silver em fees

### 2.4 Punição por Crafting (Anti-Inflação de Itens)

Mecanismo inspirado no **sistema de "focus" do Albion** e nas **taxas de falha do RuneScape antigo**:

#### Focus System

Cada jogador tem um pool de `craftingFocus` que regenera 2.000/hora (máx 20.000).

| Cenário                    | Resultado                              |
|----------------------------|----------------------------------------|
| Craft **com** focus        | 100% de eficiência — saída completa    |
| Craft **sem** focus        | 80% de eficiência — 20% dos materiais **desperdiçados** (sink) |

Craftar em bulk sem focus = lucro negativo → anti-inflação natural.
Crafters sérios gerenciam focus como recurso escasso.

#### Failure Rate por Tier

Taxa de falha quando a **skill de crafting** é baixa para o tier do item:

| Tier | Skill Mínima (sem penalidade) | Falha em abaixo do mínimo          |
|------|-------------------------------|-------------------------------------|
| T1   | Skill ≥ 1                     | Nunca falha                        |
| T2   | Skill ≥ 15                    | `max(0, (25-skill) × 2)%`          |
| T3   | Skill ≥ 30                    | `max(0, (45-skill) × 2)%`          |
| T4   | Skill ≥ 50                    | `max(0, (70-skill) × 2)%`          |

Em caso de falha: **materiais consumidos, item NÃO criado**.
Isso incentiva upskilling antes de produzir alto tier — e destrói materiais no processo.

#### Exemplos

```
Player com smithing 10 tentando craftar T2 (min 15):
  fail = max(0, (25 - 10) × 2) = 30% de chance de falhar
  
Player com smithing 20 tentando craftar T2 (min 15):
  fail = max(0, (25 - 20) × 2) = 10% de chance de falhar

Player com smithing 25+ tentando craftar T2:
  fail = 0% — maestria garante sucesso
```

### 2.5 Tiers de Item

```
T1 — Ferrugem/Osso     Iniciantes, drop comum de mobs fracos
T2 — Ferro/Couro       Nível 5+, craft ou drop de mobs médios
T3 — Aço/Escama        Nível 15+, craft (skill req) ou dungeons
T4 — Rúnico/Adamantite Nível 30+, craft avançado, full loot reward
T5 — Lendário          Raro drop, não craftável — sink natural por PvP
```

Cada tier tem: `tier: 1..5`, `itemPower: tier × 200` (base), `value: tier × baseValue`.

---

## 3. Status Effects System

Ver `COMBAT_AND_PROGRESSION_REFERENCE.md` para definições completas.

Resumo dos efeitos implementados no servidor:

| Efeito        | Mecanismo                                     | DR (Diminishing Returns)? |
|---------------|-----------------------------------------------|---------------------------|
| `stun`        | Impede cast E movimento                        | Sim                       |
| `root`        | Impede movimento, cast ok                      | Sim                       |
| `slow`        | Reduz velocidade × factor                      | Sim                       |
| `knockback`   | Empurra X unidades, impede cast momentâneo     | Sim                       |
| `antiHeal`    | Cura recebida × factor (0.5 = -50%)            | Não                       |
| `defenseDown` | Dano recebido +factor                          | Não                       |
| `bleed`       | DoT incurável por cleanse                      | Não                       |
| `shield`      | Absorve dano antes do HP                       | Não                       |
| `haste`       | Velocidade × factor                            | Não                       |
| `ccImmune`    | Ignora qualquer CC enquanto ativo              | Não                       |
| `cleanse`     | Remove debuffs do self                         | Não                       |
| `purge`       | Remove buffs do alvo                           | Não                       |

**Diminishing Returns (DR):**
```
Mesma CC aplicada ao mesmo alvo:
  1ª vez → 100% da duração
  2ª vez → 50%
  3ª vez → 25%
  4ª vez → 0% (imune)
  Resetado após 15s sem aquele CC
```

---

## 4. Roadmap de Implementação

### Fase Atual — Combat + Gear Foundation

- [x] Status Effects definidos (COMBAT_AND_PROGRESSION_REFERENCE.md)
- [ ] Remover sistema de classes → PlayerManager gear-based
- [ ] CombatEngine: skills derivadas de equipment.selectedSkills
- [ ] CombatEngine: Status Effects com DR no servidor
- [ ] skills.json refatorado para flat (sem keying por classe)
- [ ] gear.json: famílias de armas e armaduras com skill pools

### Próxima Fase — Economia Base

- [ ] Durabilidade: campo `durability` em itens, evento `item:repair`
- [ ] Market tax: 5% ao vender para NPC
- [ ] Crafting sistema: `item:craft` event + receitas + failure rate
- [ ] Focus system: `craftingFocus` no player state
- [ ] Player skill de crafting/gathering: `gatheringSkills` e `craftingSkills` separados

### Fase Futura — Economia Avançada

- [ ] Player-to-player trading
- [ ] Auction House com 5% de taxa
- [ ] Zonas PvP (vermelho/preto) com full loot
- [ ] Black Market NPC (Albion-style): absorve itens de alto tier para equilibrar drops
- [ ] Silver-to-Premium conversion (cosmetics only, não P2W)

---

## 5. Referências Citadas

- [Albion Online Wiki — Spells](https://wiki.albiononline.com/wiki/Spells)
- [Albion Online — Comparison of Equipment/Spells](https://wiki.albiononline.com/wiki/Comparison_of_Equipment/Spells)
- [EVE Online — ISK Sink or Faucet](https://fastercapital.com/content/ISK-Sink-or-ISK-Faucet--The-Economic-Balance-in-EVE-Online.html)
- [Designing Game Economies — Medium](https://medium.com/@msahinn21/designing-game-economies-inflation-resource-management-and-balance-fa1e6c894670)
- [CCP contrata economista para EVE Frontier](https://www.gamedeveloper.com/business/ccp-hires-new-economy-head-to-legitimize-eve-frontier-s-in-game-economy)
