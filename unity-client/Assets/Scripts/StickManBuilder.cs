// StickManBuilder.cs
// Cria um stick man proceduralmente usando primitivos Unity.
// Sem dependência de assets externos — funciona em qualquer cena.
//
// Hierarquia gerada como filhos do GameObject alvo:
//   Root
//   ├── Head  (Sphere)
//   ├── Body  (Cylinder vertical)
//   ├── ArmL  (Cylinder horizontal, rotacionado 90° Z)
//   ├── ArmR  (idem, lado oposto)
//   ├── LegL  (Cylinder vertical, deslocado -X)
//   └── LegR  (idem, +X)
//
// Uso:
//   StickManBuilder.Build(gameObject, Color.red);
//   — limpa filhos anteriores e reconstrói o stick man com a cor dada.
//
//   StickManBuilder.ClassColor("warrior") → cor padrão por classe.

using UnityEngine;

namespace MMORPG
{
    public static class StickManBuilder
    {
        // ─── Dimensões (em unidades Unity) ───────────────────────────────────────
        private const float HeadRadius  = 0.18f;
        private const float BodyHeight  = 0.45f;
        private const float BodyRadius  = 0.07f;
        private const float LimbRadius  = 0.05f;
        private const float ArmLength   = 0.32f;
        private const float LegLength   = 0.38f;

        // Posições Y — base Y=0 é o chão (pé do personagem)
        private static float BodyBotY   => 0f;
        private static float BodyTopY   => BodyBotY + BodyHeight;
        private static float BodyMidY   => (BodyBotY + BodyTopY) * 0.5f;
        private static float HeadCenterY=> BodyTopY + HeadRadius + 0.02f;
        private static float ShoulderY  => BodyTopY - 0.04f;
        private static float HipY       => BodyBotY + 0.05f;

        // ─── API Pública ──────────────────────────────────────────────────────────

        /// <summary>
        /// Constrói o stick man como filhos de <paramref name="parent"/>.
        /// Remove todos os filhos existentes antes de construir.
        /// </summary>
        public static void Build(GameObject parent, Color color)
        {
            if (parent == null) return;

            // Remove filhos anteriores (ex: esfera placeholder do prefab)
            for (int i = parent.transform.childCount - 1; i >= 0; i--)
                Object.Destroy(parent.transform.GetChild(i).gameObject);

            // Material compartilhado entre todos os membros
            Material mat = CreateMaterial(color);

            // Cabeça
            AddPart(parent, "Head", PrimitiveType.Sphere, mat,
                pos:   new Vector3(0f, HeadCenterY, 0f),
                scale: Vector3.one * HeadRadius * 2f);

            // Corpo (Cylinder Unity = altura total 2u, então dividimos por 2)
            AddPart(parent, "Body", PrimitiveType.Cylinder, mat,
                pos:   new Vector3(0f, BodyMidY, 0f),
                scale: new Vector3(BodyRadius * 2f, BodyHeight * 0.5f, BodyRadius * 2f));

            // Braços (Cylinder rotacionado 90° no Z para ficar horizontal)
            AddPart(parent, "ArmL", PrimitiveType.Cylinder, mat,
                pos:   new Vector3(-(ArmLength * 0.5f + BodyRadius), ShoulderY, 0f),
                scale: new Vector3(LimbRadius * 2f, ArmLength * 0.5f, LimbRadius * 2f),
                rot:   Quaternion.Euler(0f, 0f, 90f));

            AddPart(parent, "ArmR", PrimitiveType.Cylinder, mat,
                pos:   new Vector3(ArmLength * 0.5f + BodyRadius, ShoulderY, 0f),
                scale: new Vector3(LimbRadius * 2f, ArmLength * 0.5f, LimbRadius * 2f),
                rot:   Quaternion.Euler(0f, 0f, 90f));

            // Pernas
            float legOffsetX = LimbRadius * 2.5f;
            float legMidY    = HipY - LegLength * 0.5f;

            AddPart(parent, "LegL", PrimitiveType.Cylinder, mat,
                pos:   new Vector3(-legOffsetX, legMidY, 0f),
                scale: new Vector3(LimbRadius * 2f, LegLength * 0.5f, LimbRadius * 2f));

            AddPart(parent, "LegR", PrimitiveType.Cylinder, mat,
                pos:   new Vector3(legOffsetX, legMidY, 0f),
                scale: new Vector3(LimbRadius * 2f, LegLength * 0.5f, LimbRadius * 2f));
        }

        /// <summary>
        /// Cor padrão por classe para distinção visual rápida durante protótipo.
        /// Pode ser substituída por texturas ou modelos ao amadurecer o projeto.
        /// </summary>
        public static Color ClassColor(string playerClass) => playerClass?.ToLower() switch
        {
            "warrior" => new Color(0.7f, 0.15f, 0.10f), // Vermelho escuro
            "mage"    => new Color(0.15f, 0.20f, 0.85f), // Azul
            "ranger"  => new Color(0.15f, 0.60f, 0.20f), // Verde
            "healer"  => new Color(0.90f, 0.85f, 0.15f), // Amarelo
            "bruiser" => new Color(0.50f, 0.30f, 0.10f), // Marrom
            _         => new Color(0.75f, 0.75f, 0.75f), // Cinza (local / desconhecido)
        };

        // ─── Privado ──────────────────────────────────────────────────────────────

        private static Material CreateMaterial(Color color)
        {
            // Tenta URP primeiro (Unity 6); fallback para Standard (HDRP / Built-in)
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");

            var mat = new Material(shader);
            mat.color = color;
            return mat;
        }

        private static GameObject AddPart(
            GameObject parent,
            string     partName,
            PrimitiveType type,
            Material   mat,
            Vector3    pos,
            Vector3    scale,
            Quaternion rot = default)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = partName;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale    = scale;
            go.transform.localRotation = (rot == default) ? Quaternion.identity : rot;

            // Material compartilhado
            var rend = go.GetComponent<Renderer>();
            if (rend != null) rend.sharedMaterial = mat;

            // Remove colliders nos membros visuais — colisão tratada no servidor
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            return go;
        }
    }
}
