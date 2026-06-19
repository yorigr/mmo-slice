// SessionManager — persistência de sessão para reconexão de jogadores.
//
// Quando um player desconecta, o servidor salva seu estado associado a um token UUID.
// Se o cliente reconectar dentro da janela SESSION_TTL_MS e enviar o mesmo token,
// o estado é restaurado (posição, HP, mana, XP, gold, inventário).
//
// O token é gerado pelo servidor e enviado no payload "player:joined".
// O cliente Unity deve armazená-lo (PlayerPrefs) e reenviá-lo em "player:join".
//
// Após restauração, o token é consumido (use-once). Um novo token é emitido.

const SESSION_TTL_MS  = 30_000; // janela de 30s para reconectar
const CLEANUP_INTERVAL = 60_000; // limpa sessões expiradas a cada 60s

class SessionManager {
  constructor() {
    // token → { state: {...}, zoneId: string, expiresAt: number }
    this.sessions = new Map();

    // Limpeza periódica para não vazar memória
    this._cleanup = setInterval(() => this._purgeExpired(), CLEANUP_INTERVAL);
  }

  /**
   * Salva o estado do jogador associado ao token.
   * Chamado no evento "disconnect" do socket.
   *
   * @param {string} token  - UUID gerado no join (socketTokens.get(socket.id))
   * @param {object} state  - PlayerState completo (será copiado — não mantém referência viva)
   * @param {string} zoneId - ID da zona onde o player estava
   */
  save(token, state, zoneId) {
    if (!token || !state) return;

    // Copia rasa + arrays novos para não manter referência ao objeto live
    const snapshot = {
      ...state,
      cooldowns: { ...state.cooldowns },
      inventory: state.inventory ? [...state.inventory] : [],
    };

    this.sessions.set(token, {
      state:    snapshot,
      zoneId:   zoneId || 'overworld',
      expiresAt: Date.now() + SESSION_TTL_MS,
    });
  }

  /**
   * Restaura e consome a sessão associada ao token.
   * Retorna { state, zoneId } se válida, null se expirada/inexistente.
   *
   * O token é removido após uso — o servidor emite um novo token.
   *
   * @param {string} token
   * @returns {{ state: object, zoneId: string } | null}
   */
  restore(token) {
    if (!token) return null;

    const session = this.sessions.get(token);
    if (!session) return null;
    if (session.expiresAt < Date.now()) {
      this.sessions.delete(token);
      return null;
    }

    this.sessions.delete(token); // consume
    return { state: session.state, zoneId: session.zoneId };
  }

  /** Quantidade de sessões ativas (para diagnóstico). */
  get size() { return this.sessions.size; }

  _purgeExpired() {
    const now = Date.now();
    for (const [token, session] of this.sessions) {
      if (session.expiresAt < now) this.sessions.delete(token);
    }
  }

  destroy() {
    clearInterval(this._cleanup);
    this.sessions.clear();
  }
}

module.exports = SessionManager;
