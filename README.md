# ⚔️ MMO Vertical Slice v2 — Netcode + Casting

Fundação testável do projeto. A v1 provou movimento e combate autoritativos;
a v2 adiciona o sistema de **casting interrompível** (o "micro casting" que você queria).

## O que está provado (testado, não só escrito)

- ✅ Servidor autoritativo + tick loop 20Hz
- ✅ Validação de movimento (speedhack rejeitado)
- ✅ 3 abilities: Slash (instantâneo), Heavy Blow (cast 0,4s), Frost Bolt (cast 0,9s, ranged)
- ✅ Cast com tempo: só resolve quando o tempo passa
- ✅ **Interrupção por dano**: tomar golpe durante o cast cancela (e custa a mana)
- ✅ Cooldown e mana forçados no servidor
- ✅ Alcance ranged validado
- ✅ Client-side prediction no movimento

## Rodar

Precisa de Node.js 18+.

```bash
cd mmo-slice
npm install
node server.js
# abra http://localhost:3000 em DUAS abas
```

## Controles

- Mover: W A S D (ou setas)
- `1` Slash · `2` Heavy Blow · `3` Frost Bolt
- Bolt mira automaticamente no inimigo mais próximo
- Barra amarela = cast em andamento. Acerte o inimigo durante o cast dele para interromper.

## Testes

```bash
node test.js           # lógica de movimento/combate v1 (13 checks)
node test-network.js   # rede v1 (9 checks)
node test-casting.js   # sistema de casting v2 (20 checks)
node test-net-v2.js    # rede v2: casting + interrupção + mana (8 checks)
```
Total: **50 verificações automatizadas**, todas passando.

## Decisão de design embutida

Mana é consumida no **início** do cast. Se você for interrompido, perde a mana —
esse é o risco que torna a interrupção significativa (estilo hardcore). Fácil de
mudar se quiser que interrupção devolva a mana.

## Próximo passo de menor risco

1. **Sentir o feel** com 2 abas: o jogo do gato-e-rato de interromper casts é
   divertido? Responsivo? Esse é o teste que decide tudo.
2. Adicionar **dano vindo de equipamento** (primeiro passo do skill-based).
3. Só depois: persistência (banco), classes de verdade, zonas.

## Arquivos

- `server.js` — servidor autoritativo v2 (lógica em funções puras testáveis)
- `public/index.html` — cliente canvas
- `test*.js` — suítes de teste
- `server-v1-backup.js` — versão anterior (referência)
