// StickManBuilder.cs
// Constrói um personagem low-poly estilo Albion Online usando primitivos Unity.
// Sem dependência de assets externos — funciona em qualquer cena.
//
// Hierarquia gerada como filhos do GameObject alvo:
//   Root
//   ├── Body         (Cube largo — tronco com armadura)
//   ├── Head         (Cube — capacete low-poly)
//   ├── Visor        (Cube fino — rosto/viseira)
//   ├── ShoulderL/R  (Cubes — ombreiras/pauldrons)
//   ├── ArmL/R       (Cylinders — braços)
//   ├── HandL/R      (Spheres — mãos)
//   ├── LegL/R       (Cylinders — pernas)
//   └── Weapon       (Cube longo — arma genérica)
//
// Estilo "blocky" intencional — mesmo visual que o Albion Online usa em protótipos.
// Quando assets reais forem integrados, este builder será removido.

using UnityEngine;

namespace MMORPG
{
    public static class StickManBuilder
    {
        // Proporções em unidades Unity
        private const float BodyW  = 0.38f;
        private const float BodyD  = 0.22f;
        private const float BodyH  = 0.52f;
        private const float HeadW  = 0.30f;
        private const float HeadH  = 0.28f;
        private const float HeadD  = 0.26f;
        private const float ArmR   = 0.075f;
        private const float ArmH   = 0.30f;
        private const float LegR   = 0.09f;
        private const float LegH   = 0.38f;
        private const float PadW   = 0.14f;
        private const float PadH   = 0.10f;

        // Y relativo ao chão (y=0 = pés)
        private static float LegMidY    => LegH * 0.5f;
        private static float BodyBotY   => LegH;
        private static float BodyMidY   => BodyBotY + BodyH * 0.5f;
        private static float BodyTopY   => BodyBotY + BodyH;
        private static float ShoulderY  => BodyTopY - 0.04f;
        private static float HeadMidY   => BodyTopY + 0.02f + HeadH * 0.5f;
        private static float ArmMidY    => ShoulderY - ArmH * 0.5f - PadH * 0.4f;

        // ─── API Pública ──────────────────────────────────────────────────────────

        public static void Build(GameObject parent, Color armorColor)
        {
            if (parent == null) return;

            for (int i = parent.transform.childCount - 1; i >= 0; i--)
                Object.Destroy(parent.transform.GetChild(i).gameObject);

            Material matArmor = Mat(armorColor);
            Material matSkin  = Mat(new Color(0.82f, 0.70f, 0.58f));
            Material matDark  = Mat(Color.Lerp(armorColor, Color.black, 0.45f));
            Material matMetal = Mat(new Color(0.62f, 0.62f, 0.65f));

            float legX = LegR * 1.3f;
            Cyl(parent, "LegL",  matDark,  new Vector3(-legX, LegMidY, 0f), new Vector3(LegR*2, LegH*0.5f, LegR*2));
            Cyl(parent, "LegR",  matDark,  new Vector3( legX, LegMidY, 0f), new Vector3(LegR*2, LegH*0.5f, LegR*2));

            Cube(parent, "Body", matArmor, new Vector3(0f, BodyMidY, 0f), new Vector3(BodyW, BodyH, BodyD));
            Cube(parent, "Head", matArmor, new Vector3(0f, HeadMidY, 0f), new Vector3(HeadW, HeadH, HeadD));
            Cube(parent, "Visor", matDark, new Vector3(0f, HeadMidY, -(HeadD*0.5f+0.005f)), new Vector3(HeadW*0.7f, HeadH*0.35f, 0.04f));

            float padX = BodyW * 0.5f + PadW * 0.5f;
            Cube(parent, "ShoulderL", matMetal, new Vector3(-padX, ShoulderY, 0f), new Vector3(PadW, PadH, BodyD*0.9f));
            Cube(parent, "ShoulderR", matMetal, new Vector3( padX, ShoulderY, 0f), new Vector3(PadW, PadH, BodyD*0.9f));

            float armX = padX + PadW*0.5f + ArmR;
            Cyl(parent, "ArmL",  matArmor, new Vector3(-armX, ArmMidY, 0f), new Vector3(ArmR*2, ArmH*0.5f, ArmR*2));
            Cyl(parent, "ArmR",  matArmor, new Vector3( armX, ArmMidY, 0f), new Vector3(ArmR*2, ArmH*0.5f, ArmR*2));

            float handY = ArmMidY - ArmH * 0.5f - 0.04f;
            Sph(parent, "HandL", matSkin, new Vector3(-armX, handY, 0f), Vector3.one * 0.09f);
            Sph(parent, "HandR", matSkin, new Vector3( armX, handY, 0f), Vector3.one * 0.09f);

            Cube(parent, "Weapon", matMetal,
                new Vector3(armX + 0.06f, handY - 0.18f, -0.04f),
                new Vector3(0.06f, 0.55f, 0.06f),
                Quaternion.Euler(10f, 0f, 8f));
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private static Material Mat(Color c)
        {
            var s   = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(s) { color = c };
            if (mat.HasProperty("_Smoothness"))  mat.SetFloat("_Smoothness",  0.20f);
            else if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.20f);
            return mat;
        }

        private static void Setup(GameObject go, GameObject p, string n, Material mat,
                                  Vector3 pos, Vector3 sc, Quaternion rot = default)
        {
            go.name = n;
            go.transform.SetParent(p.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale    = sc;
            go.transform.localRotation = rot == default ? Quaternion.identity : rot;
            var r = go.GetComponent<Renderer>();
            if (r) r.sharedMaterial = mat;
            var col = go.GetComponent<Collider>();
            if (col) Object.Destroy(col);
        }

        private static void Cube(GameObject p, string n, Material mat, Vector3 pos, Vector3 sc,
                                  Quaternion rot = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Setup(go, p, n, mat, pos, sc, rot);
        }

        private static void Cyl(GameObject p, string n, Material mat, Vector3 pos, Vector3 sc)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Setup(go, p, n, mat, pos, sc);
        }

        private static void Sph(GameObject p, string n, Material mat, Vector3 pos, Vector3 sc)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Setup(go, p, n, mat, pos, sc);
        }
    }
}
