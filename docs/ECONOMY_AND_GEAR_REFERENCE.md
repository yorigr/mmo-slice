# Economia e Gear-Based System — Referência de Design

> **Propósito:** Design decisions + pesquisa real de como MMORPGs controlam inflação.
> Leia antes de alterar qualquer constante de economia em `constants.js`.

---

## 1. Sistema Gear-Based ("You Are What You Wear")

### Princípio
Sem classes fixas. O equipamento determina o estilo de jogo. Inspirado no Albion Online.

- **Arma** → define a família de habilidades (3 slots: Q / W / E)
- **Peitoral** → 1 slot de skill (R)
- **Capacete** → 1 slot de skill (D)
- **Botas** → futuro slot (F)

Total: 5 skills ativas simultâneas, todas intercambiáveis trocando gear.

### Skill Pools por Peça
- Peças diferentes do mesmo tipo compartilham skills comuns (Q, W)
- Cada peça tem uma skill exclusiva (E para armas, slot principal para armaduras)
- Exemplo: Espada Longa e Espada Curta têm `skill_slash` e `skill_heavy_blow` em comum, mas E diferente

### Famílias de Armas (gear.json)
| Família | Range | Estilo |
|---------|-------|--------|
| sword | Melee | Tanque/DPS versátil |
| greataxe | Melee | DPS bruto |
| daggers | Melee | Burst/mobilidade |
| mace | Melee | Anti-heal + CC |
| hammer | Melee | AoE + knockback |
| bow | Ranged | DPS à distância |
| fire_staff | Ranged | DoT + área |
| frost_staff | Ranged | CC + slow |
| arcane_staff | Ranged | Burst + interrupt |
| holy_staff | Ranged | Heal + suporte |

### Armaduras (gear.json)
Três materiais × três slots:

| Material | Bônus Principal | Ideal para |
|----------|----------------|------------|
| Cloth | +MaxMana, +Speed | Casters |
| Leather | +Speed, damageReduction leve | Duelistas |
| Plate | +MaxHp, damageReduction | Tanques |

---

## 2. Pesquisa: Como MMORPGs Controlam a Inflação

> Esta seção documenta fatos reais com números concretos. Cada jogo representa um experimento econômico diferente. As lições informam diretamente nossas decisões de design.

---

### 2.1 EVE Online — O Caso Mais Estudado

**Status:** Economia amplamente considerada a mais robusta de qualquer MMORPG.
**Detalhe único:** Publicam relatórios econômicos trimestrais com Dr. Eyjolfur Gudmundsson (economista contratado desde 2007).

**Números reais (2021–2023):**
- Criação diária de ISK via bounties de NPCs: **~2 trilhões de ISK/dia**
- Bounties representam **50%+ de todos os faucets combinados**
- Transaction tax remove apenas **80–90 bilhões/dia** → negligenciável vs. criação
- **Sink principal: destruição de naves em PvP** — cada nave destruída some permanentemente do jogo

**Por que funciona:**
- Players não pagam um "fee" abstrato; eles **fazem algo** (PvP) que naturalmente consome recursos
- Casco + módulos + carregamento de munição = dezenas de componentes craftados, todos destruídos ao mesmo tempo
- O risco de perder a nave é o que dá **valor percebido** ao jogo

**Resultado:** ISK existente cresceu ~50% ao longo de 20 anos, mas acompanhado por crescimento do jogo. Inflação controlada sem intervenção artificial.

**Problema identificado:** Em 2021, a CCP reduziu recompensas de bounty em 50% para forçar mais PvP. Resultado: deflação e perda de 30%+ da base de jogadores em 6 meses. **Deflação é tão perigosa quanto inflação.**

---

### 2.2 Old School RuneScape (OSRS) — Taxação via Grand Exchange

**Status:** Economia estável mas com pressão constante de bots.
**Dado histórico pré-taxa (até dez/2021):** ~2,7 trilhões de GP criados mensalmente no Grand Exchange, principalmente via bossing (Raids, God Wars Dungeon).

**Grand Exchange Tax (implementada dezembro 2021):**
- **1% tax** sobre todas as transações no Grand Exchange
- Removeu **bilhões de GP** semanalmente — efeito cumulativo enorme sobre circulação
- Recepção da comunidade: mista — jogadores veteranos odiaram, novos jogadores aceitaram bem

**Outros sinks ativos:**
- **Bonds** (dinheiro real → GP): cria demanda "infinita" por GP para pagar membership
- **Runes consumíveis**: cada spell usada consome runes; sempre há demanda por runecrafters
- **Death tax em instâncias**: ao morrer em certas instâncias, paga GP para recuperar itens (até 500k+ por morte)

**Problemas de inflação não resolvidos:**
- Bots geram GP fora do GE (venda direta entre jogadores), evitando a taxa completamente
- Items raros de boss têm inflação descontrolada (Twisted Bow ultrapassou 1,2 bilhões de GP)

**Lição:** Mesmo 1% de taxa em volume alto é poderoso. O problema vem de gold que bypass o mercado centralizado.

---

### 2.3 Black Desert Online (BDO) — Controle por Teto de Preços

**Status:** Economia artificialmente estável mas com sérias críticas estruturais.

**Mecanismo:**
- Cada item tem **preço mínimo e máximo** fixados pelo jogo — players não podem vender fora da faixa
- Resultado: inflação de preços é **impossível** por design (price caps impedem)

**Problemas graves:**
- **Bots massivos**: como o silver não inflaciona livremente, o único jeito de ganhar competitividade é volume de grind. Botting é endêmico.
- **Pay-to-Win via Pearl Shop**: pets (coleta automática) e expansões de peso são basicamente obrigatórios para competir; vendidos por dinheiro real via troca com outros jogadores
- **Silver acumula sem pressão de saída**: o teto de preço não impede acúmulo, só impede compra de poder

**Lição:** Price caps não controlam inflação — apenas ocultam os sintomas. O silver acumula de qualquer jeito; a diferença é que items raros chegam ao cap instantaneamente e ficam inacessíveis para novos jogadores.

---

### 2.4 Final Fantasy XIV (FFXIV) — Taxa de Mercado por Servidor

**Status:** Economia funcional, mas menos player-driven que EVE.

**Mecânica:**
- Tudo que vai ao **Market Board** paga de **3% a 5% de taxa** (varia por cidade; Limsa Lominsa tem menor taxa, incentivando concentração de mercado)
- Comprador paga preço cheio; vendedor recebe (100% – taxa%)
- Não existe trade direto player → player para a maioria dos itens: **tudo passa pelo Market Board obrigatoriamente**

**Sinks adicionais:**
- **Teleport fees**: custo fixo de Gil para teletransporte entre cidades (reduz acúmulo passivo)
- **Repair costs**: todo gear degrada e precisa de Gil ou materiais para reparar
- **Market listing fee**: taxa de renovação se item não vendeu no prazo

**Resultado:** Funciona bem para o contexto. Mas FFXIV tem menor pressão inflacionária por design: melhores itens vêm de **conteúdo de grupo** (raids, não mercado). A economia é suporte, não núcleo.

**Lição:** Forçar todos os trades a passar por um canal taxado é eficiente. Não resolve inflação se os itens mais valiosos não passam pelo mercado — no nosso jogo, queremos que **tudo** seja player-crafted.

---

### 2.5 New World (Amazon) — Estudo de Caso de Falha

**Status:** Economia quebrou múltiplas vezes, forçando shutdown temporário do mercado.

**Falha #1 (lançamento, out/2021): HTML injection em chat**
- Bug permitia que players injetassem HTML no chat; combinado com outro exploit, possibilitava duplicação de gold
- O mercado foi **desativado por completo** para correção emergencial, forçando players a negociar manualmente

**Falha #2 (dez/2021): Furniture duplication**
- Mobília podia ser duplicada infinitamente com exploit de timing entre animações
- Inundou o mercado com items raros, colapsando preços de gold e itens premium
- Segunda pausa do mercado; Amazon compensou afetados com gold artificial

**Falha de design: over-correction → deflação**
- Para combater a inflação pós-dupes, Amazon adicionou múltiplos sinks agressivos simultaneamente
- Resultado: jogadores acumulavam gold tão devagar que não conseguiam pagar crafting fees, repair costs e market listing fees
- Deflação travou a progressão de novos jogadores e afastou crafters

**Lições críticas:**
1. **Teste exploits antes do lançamento** — duplication bugs destroem mais rapidamente do que qualquer inflação orgânica
2. **Nunca corrija inflação com sinks abruptos** — o ajuste tem que ser gradual (semanas, não um patch)
3. **Calibre faucets e sinks juntos** — sinks agressivos sem reduzir faucets criam deflação
4. **Validação server-side é obrigatória** — nunca confie no cliente para quantidades de items ou gold

---

### 2.6 Albion Online — Blueprint para o Nosso Sistema

**Status:** Economia player-driven mais saudável de MMORPG ativo. Mais próximo do que queremos construir.

**Estrutura completa:**
- **100% player-crafted**: todo gear é fabricado por players; nenhum item de boss vai diretamente equipado
- **Sem bind-on-pickup**: qualquer item pode ser vendido, tradado ou deixado no banco
- **Full loot em red/black zones**: morrer = perder o que está equipado + inventário

**Sistema de destruição na morte (o mais relevante para nós):**

| Zona | Destruição por peça | Loot por outros |
|------|---------------------|-----------------|
| Blue (segura) | 0% | Não |
| Yellow | 0% gear, ~10% recursos | Não |
| Red | **~33% cada peça** | Sim (restante cai no chão) |
| Black | **~50% cada peça** | Sim (restante cai no chão) |

- A **33% trash rate** é rolada **individualmente por peça** — não sobre o conjunto total
- Exemplo: morrer em red zone com 4 peças → em média 1,32 peças destruídas, 2,68 dropadas
- Itens dropados no chão desaparecem em ~5 minutos se não coletados

**Escassez controlada por tier:**
- T3 (básico): ~85 conjuntos completos craftados por cluster/dia
- T4: ~28 conjuntos/cluster/dia (3x mais raro que T3)
- T5: ~9 conjuntos/cluster/dia (9x mais raro que T3)
- Cada tier requer materiais do tier anterior → a cadeia de crafting inteira é afetada

**Por que funciona:**
- Destruição constante cria **demanda perpétua por crafting** — a profissão de crafter tem emprego garantido
- Cada morte em red zone = estatisticamente 1–2 peças precisam ser recraftadas
- O risco concreto de perder gear dá peso real a cada decisão de entrar em PvP

**Sistema Focus (relevante para nosso crafting):**
- Cada conta tem **20.000 focus/semana** (regenera ~2.857/dia)
- Usar focus durante crafting: retorno de **53% dos materiais** de volta
- Sem focus: **0% de retorno** — todos os materiais consumidos sem retorno
- Incentivo enorme para focar em uma especialização em vez de craftar tudo de uma vez

---

## 3. Design Anti-inflação do Nosso Jogo

### 3.1 Faucets (Entradas de Gold/Itens)
| Fonte | Volume | Notas |
|-------|--------|-------|
| Loot de monstros (gold) | Médio | Escala com tier e tipo de monstro |
| Drops de itens de monstros | Baixo | Chance base 60%, items de baixo tier |
| Venda no mercado | Neutro | Redistribui, não cria gold novo |

### 3.2 Sinks (Saídas de Gold/Itens)
| Mecanismo | Taxa | Implementado |
|-----------|------|:---:|
| Market tax | 5% sobre vendas | ✓ |
| Custo de reparo | 15% × (1 – dur/100) × valor | ✓ |
| Overhead de crafting | 5% do valor estimado | ✓ |
| **Destruição na morte** | **25%–50% por peça** | ✓ |
| Failure rate de crafting | 0%–30% por tier | ✓ |
| Focus sem uso: materiais perdidos | 20% extras | ✓ |
| Itens no chão despawnam | 60s | ✓ |

### 3.3 Sistema Focus de Crafting

```
craftingFocus: máx 20.000 por player (regenera 2.000/hora)

COM focus:   eficiência 100% — apenas os materiais base são consumidos
SEM focus:   eficiência 80% — 20% a mais de materiais são desperdiçados
```

Efeito econômico: crafters sem foco destroem 20% extras de materiais sem produzir nada extra.
Incentivo para especialização: players que tentam ser ferreiro + alquimista + curtidor ao mesmo tempo ficam sempre sem foco.

### 3.4 Failure Rate por Tier

| Tier | Taxa de falha (skill 1) | Redução por level de skill |
|------|------------------------|---------------------------|
| T1 | 0% | — |
| T2 | 5% | –0.5%/level |
| T3 | 10% | –1%/level |
| T4 | 20% | –2%/level |
| T5 | 30% | –3%/level |

Falha = materiais consumidos, item não criado. Em T5 sem especialização: efetivamente 50%+ de desperdício total (30% falha × materiais extras por falta de foco).

---

## 4. Destruição de Itens na Morte

### 4.1 Racional

Sem destruição permanente, a economia satura: itens antigos nunca saem do jogo, preços colapsam gradualmente, crafters ficam sem clientes. A destruição na morte cria a **demanda recorrente** que sustenta o loop inteiro de gathering → crafting → uso → morte → recrafting.

### 4.2 Tabela de Destruição por Zona

| Zona | Taxa por peça | Loot por outros | Uso |
|------|--------------|-----------------|-----|
| `safe` | 0% | Não | Área inicial, tutoriais |
| `yellow` | 25% | Não | Overworld PvM padrão |
| `red` | 33% | Sim | Zonas PvP abertas |
| `black` | 50% | Sim | GvG, end-game hardcore |

### 4.3 Roll Individual por Peça

```
handlePlayerDeath(player, zoneType):
  rate = DEATH_DESTROY_RATES[zoneType]  // 0 / 0.25 / 0.33 / 0.50
  canLoot = (zoneType === 'red' || zoneType === 'black')

  Para cada slot em [weapon, chest, head, boots]:
    if random() < rate:
      → DESTRUÍDO: slot = null, skills relacionadas = null
    elif canLoot:
      → DROPADO: item criado no mundo, slot = null
    else:
      → MANTIDO: player fica com o item (yellow zone)

  Para cada stack no inventário:
    qty -= ceil(qty * DEATH_RESOURCE_LOSS_RATE)  // 10% destruídos
```

### 4.4 Constantes (constants.js)
```js
DEATH_DESTROY_RATES:    { safe: 0.00, yellow: 0.25, red: 0.33, black: 0.50 },
DEATH_RESOURCE_LOSS_RATE: 0.10,  // 10% de materiais do inventário destruídos
```

### 4.5 Evento Emitido ao Cliente

```json
player:death_loot {
  "destroyed": [{ "slot": "weapon", "gearId": "sword" }],
  "dropped":   [{ "slot": "chest",  "gearId": "plate_chest", "itemId": "uuid-xyz" }],
  "kept":      [{ "slot": "head",   "gearId": "leather_cap" }]
}
```

O cliente usa esse evento para atualizar a UI (mostrar quais itens foram perdidos, animação de destruição etc).

### 4.6 Progressão de Zonas (Roadmap)

| Fase | Zonas disponíveis | Tipo de morte |
|------|------------------|--------------|
| **Atual** | `overworld` (yellow) | 25% destruído, sem loot por outros |
| Fase 3 | Red zones abertas | 33% destruído, full loot |
| Fase 4 | Black zones (GvG) | 50% destruído, full loot |

---

## 5. Skills de Crafting e Gathering

Cada atividade de crafting ou gathering tem skill própria com XP e level independentes.

### Gathering Skills
| Skill | Recurso coletado | Ferramenta |
|-------|-----------------|-----------|
| `mining` | Minério de ferro, prata, ouro, gemas | Picareta (nível requerido varia) |
| `woodcutting` | Madeira, troncos, fibra de árvore | Machado |
| `herbalism` | Ervas medicinais, raízes, flores | Foice |
| `hunting` | Pele, carne, ossos | Arco ou armadilha |
| `fishing` | Peixe, coral, conchas | Vara de pescar |

### Crafting Skills
| Skill | O que produz | Materiais principais |
|-------|-------------|---------------------|
| `smithing` | Armas e armaduras de metal (plate) | Minério + carvão |
| `leatherwork` | Armaduras de couro | Pele curtida + reagentes |
| `alchemy` | Poções, consumíveis, reagentes especiais | Ervas + pó de cristal |
| `fletching` | Arcos, flechas, bestas | Madeira + pena + corda |
| `runecrafting` | Cajados rúnicos, robes mágicos (cloth) | Pó rúnico + pedra de éter |

---

## 6. Status de Implementação

### Completo
- [x] Sistema gear-based sem classes
- [x] `gear.json` com 10 famílias de arma + 9 tipos de armadura
- [x] `skills.json` flat (55 skills, indexadas por `skill_id`)
- [x] Status effects completos + Diminishing Returns em CC
- [x] Constantes de economia em `constants.js`
- [x] Estrutura de gatheringSkills + craftingSkills no player state
- [x] **Destruição de itens na morte** com zoneType por zona

### Próximas Fases
- [ ] Crafting real com consumo de materiais, failure rate e focus
- [ ] Gathering com nós de recurso no mapa, animação e tempo de coleta
- [ ] Market Board com 5% de taxa e sistema de listagem
- [ ] Red Zones com full loot + 33% destruição
- [ ] Sistema de reparo com custo de gold
- [ ] Durability por item (degrade com uso, quebre ao 0)
