// SocketIOParser.cs
// Parser mínimo para o protocolo Socket.IO v4 sobre WebSocket raw.
//
// Por que manual e não usar SocketIOUnity?
//   O SocketIOUnity é pesado (~5 deps), quebra frequentemente entre versões do Unity
//   e o protocolo Socket.IO v4 sobre texto é simples o suficiente para parsear em ~50 linhas.
//   Isso nos dá controle total, zero dependências extras e facilidade de debug.
//
// Formato dos pacotes Socket.IO v4:
//   "40"           → ENGINE_IO connect (servidor confirma abertura da sessão)
//   "2"            → ENGINE_IO ping (servidor testa se o cliente está vivo)
//   "3"            → ENGINE_IO pong (cliente responde ao ping — NÓS enviamos isso)
//   "42[...]"      → MESSAGE + EVENT (a maioria das mensagens do jogo)
//   "41"           → disconnect do namespace
//
// Referência: https://socket.io/docs/v4/socket-io-protocol/

using System;
using System.Text;
using UnityEngine;

namespace MMORPG.Network
{
    /// <summary>
    /// Resultado do parsing de uma mensagem Socket.IO.
    /// </summary>
    public readonly struct SocketIOMessage
    {
        public readonly SocketIOMessageType Type;
        public readonly string EventName;   // Só preenchido se Type == Event
        public readonly string JsonData;    // Payload JSON bruto, pode ser null

        public SocketIOMessage(SocketIOMessageType type, string eventName = null, string jsonData = null)
        {
            Type = type;
            EventName = eventName;
            JsonData = jsonData;
        }
    }

    public enum SocketIOMessageType
    {
        Unknown,
        EngineOpen,     // "0{...}" — Engine.IO handshake (cliente deve responder com "40")
        Connect,        // "40" — Socket.IO namespace connect confirmado pelo servidor
        Ping,           // "2"  — keepalive do servidor
        Disconnect,     // "41" — servidor pediu desconexão
        Event,          // "42[...]" — mensagem real do jogo
    }

    public static class SocketIOParser
    {
        // Socket.IO Engine.IO packet types (primeiro caractere)
        private const char EIO_OPEN    = '0';
        private const char EIO_CLOSE   = '1';
        private const char EIO_PING    = '2';
        private const char EIO_PONG    = '3';
        private const char EIO_MESSAGE = '4'; // Prefixo de todos os pacotes Socket.IO reais

        // Socket.IO namespace packet types (segundo caractere quando EIO = 4)
        private const char SIO_CONNECT    = '0'; // "40"
        private const char SIO_DISCONNECT = '1'; // "41"
        private const char SIO_EVENT      = '2'; // "42"

        /// <summary>
        /// Parseia uma mensagem recebida do WebSocket e retorna o tipo e dados.
        /// Nunca lança exceção — retorna SocketIOMessageType.Unknown em caso de erro.
        /// </summary>
        public static SocketIOMessage Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return new SocketIOMessage(SocketIOMessageType.Unknown);

            // Pacotes de uma letra: ping "2", close "1"
            if (raw.Length == 1)
            {
                return raw[0] switch
                {
                    EIO_PING  => new SocketIOMessage(SocketIOMessageType.Ping),
                    EIO_CLOSE => new SocketIOMessage(SocketIOMessageType.Disconnect),
                    _         => new SocketIOMessage(SocketIOMessageType.Unknown)
                };
            }

            // Engine.IO open: "0{...}" — handshake inicial do servidor
            // Socket.IO v4: após receber isto, o cliente DEVE enviar "40" para conectar ao namespace
            if (raw[0] == EIO_OPEN)
                return new SocketIOMessage(SocketIOMessageType.EngineOpen);

            // Pacotes com prefixo Engine.IO "4" (mensagens Socket.IO reais)
            if (raw[0] != EIO_MESSAGE)
                return new SocketIOMessage(SocketIOMessageType.Unknown);

            if (raw.Length < 2)
                return new SocketIOMessage(SocketIOMessageType.Unknown);

            char sioType = raw[1];

            if (sioType == SIO_CONNECT)
                return new SocketIOMessage(SocketIOMessageType.Connect);

            if (sioType == SIO_DISCONNECT)
                return new SocketIOMessage(SocketIOMessageType.Disconnect);

            if (sioType == SIO_EVENT)
                return ParseEvent(raw);

            return new SocketIOMessage(SocketIOMessageType.Unknown);
        }

        // Parseia "42["nome_evento",{dados}]"
        // Procura o primeiro '[', extrai o nome do evento e o payload JSON
        private static SocketIOMessage ParseEvent(string raw)
        {
            // Formato esperado: 42["eventName",{...}]
            // raw[0] = '4', raw[1] = '2', raw[2] = '['
            int arrayStart = raw.IndexOf('[');
            if (arrayStart < 0)
            {
                Debug.LogWarning($"[SocketIOParser] Evento sem array: {raw}");
                return new SocketIOMessage(SocketIOMessageType.Unknown);
            }

            // Nome do evento: primeira string dentro do array
            // raw[arrayStart+1] deve ser '"'
            int nameStart = arrayStart + 2; // pula '[' e '"'
            int nameEnd = raw.IndexOf('"', nameStart);
            if (nameEnd < 0)
            {
                Debug.LogWarning($"[SocketIOParser] Não achou fim do nome do evento: {raw}");
                return new SocketIOMessage(SocketIOMessageType.Unknown);
            }

            string eventName = raw.Substring(nameStart, nameEnd - nameStart);

            // Dados JSON: tudo após a primeira vírgula até o último ']'
            int commaPos = raw.IndexOf(',', nameEnd);
            string jsonData = null;

            if (commaPos >= 0)
            {
                int jsonStart = commaPos + 1;
                int jsonEnd = raw.LastIndexOf(']');
                if (jsonEnd > jsonStart)
                    jsonData = raw.Substring(jsonStart, jsonEnd - jsonStart);
            }

            return new SocketIOMessage(SocketIOMessageType.Event, eventName, jsonData);
        }

        /// <summary>
        /// Constrói a string de emit Socket.IO v4 para enviar ao servidor.
        /// Resultado: 42["eventName",{jsonPayload}]
        /// </summary>
        public static string BuildEmit(string eventName, string jsonPayload = null)
        {
            var sb = new StringBuilder("42[\"");
            sb.Append(eventName);
            sb.Append('"');

            if (!string.IsNullOrEmpty(jsonPayload))
            {
                sb.Append(',');
                sb.Append(jsonPayload);
            }

            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>
        /// String de pong para responder ao ping do servidor. Deve ser enviada como-está.
        /// </summary>
        public static string PongMessage => "3";

        /// <summary>
        /// String de ping para keepalive client-initiated. Deve ser enviada como-está.
        /// </summary>
        public static string PingMessage => "2";

        /// <summary>
        /// Pacote Socket.IO v4 para solicitar conexão ao namespace padrão "/".
        /// Em Socket.IO v4 (EIO=4), o CLIENTE deve enviar este pacote após receber
        /// o handshake Engine.IO ("0{...}"). O servidor responde com outro "40" confirmando.
        /// </summary>
        public static string NamespaceConnectMessage => "40";
    }
}
