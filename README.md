# ⚔️ MMORPG — Projeto Unity 6 + Node.js

## Estrutura do Repositório

```
MMORPG/
├── server/           ← SERVIDOR ATIVO (main path) — Node.js, Socket.IO, porta 3000
├── unity-client/     ← CLIENTE UNITY (main path) — Unity 6 URP 3D isométrico
│   └── mmo-client/       Projeto Unity (abrir no Unity Hub)
├── _archive/         ← REFERÊNCIA APENAS — protótipo antigo, não usar em produção
│   └── mmo-slice/        Servidor single-file + cliente browser (fase de protótipo)
├── docs/             ← Documentação do projeto
├── iniciar-mmorpg.bat   Inicia o servidor (atalho rápido)
├── restart-server.bat   Mata e reinicia o servidor
└── push-github.bat      Envia alterações para GitHub
```

> **Regra simples:** tudo que importa está em `server/` e `unity-client/`.  
> `_archive/` existe apenas para consulta histórica.

## Iniciar o servidor

Execute `iniciar-mmorpg.bat` (usa o Node portátil em `_archive/mmo-slice/node-v24.16.0-win-x64/`).

Acesse `http://localhost:3000` para o painel web.

O cliente Unity conecta automaticamente em `ws://localhost:3000`.

## Arquitetura do servidor (`server/`)

- `src/server.js` — entrada + Socket.IO
- `src/managers/PlayerManager.js` — movimento e estado de jogadores
- `src/managers/MonsterManager.js` — IA e spawn de monstros
- `src/managers/CombatEngine.js` — combate autoritativo
- `src/managers/WorldManager.js` — mapa 2400×1800, colisão
- `src/config/constants.js` — tick rate, velocidades, HP
- `src/config/skills.json` — definições de habilidades

## Cliente Unity (`unity-client/mmo-client/`)

Unity 6 (6000.5.0f1), URP, câmera isométrica 3D.

Scripts em `Assets/Scripts/`:
- `GameManager.cs` — orquestrador principal
- `Network/NetworkManager.cs` — WebSocket + Socket.IO v4 sem dependências externas
- `Network/SocketIOParser.cs` — parser Engine.IO v4
- `Player/PlayerController.cs` — movimento com client-side prediction
- `World/WorldState.cs` — estado dos jogadores e monstros
- `World/MonsterController.cs` — representação visual dos monstros

## Stack técnica

| Camada | Tecnologia |
|--------|-----------|
| Servidor | Node.js + Socket.IO v4 |
| Protocolo | Engine.IO v4 / WebSocket |
| Cliente | Unity 6 URP (C#) |
| Mapa | 2400×1800 unidades servidor |
| Tick rate | 20Hz (50ms) |
