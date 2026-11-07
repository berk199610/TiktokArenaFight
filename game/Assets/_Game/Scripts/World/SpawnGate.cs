using UnityEngine;

namespace Arena
{
    /// <summary>
    /// A gate: simple arch + two torches (doc §8.1). Entering characters appear here with a lightning strike,
    /// a glowing ground ring and dust, then run into the arena (doc §6.4, §9).
    /// </summary>
    public class SpawnGate
    {
        public readonly Transform t;
        public readonly Vector3 inward;
        readonly Light[] torchLights = new Light[2];

        public Vector3 SpawnPoint => t.position + inward * 1.1f;

        public SpawnGate(Transform parent, Vector3 pos, string name)
        {
            t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = pos;
            inward = (-pos).normalized;
            t.rotation = Quaternion.LookRotation(inward, Vector3.up);

            var stone = Mats.Lit(new Color(0.5f, 0.42f, 0.36f));
            var dark = Mats.Lit(new Color(0.08f, 0.06f, 0.06f));
            float w = 2.4f, h = 3.4f;
            MeshKit.Part("PillarL", MeshKit.Cube, stone, t, new Vector3(-w * 0.5f - 0.3f, h * 0.5f, 0), Vector3.zero, new Vector3(0.6f, h, 0.8f));
            MeshKit.Part("PillarR", MeshKit.Cube, stone, t, new Vector3(w * 0.5f + 0.3f, h * 0.5f, 0), Vector3.zero, new Vector3(0.6f, h, 0.8f));
            MeshKit.Part("Arch", MeshKit.Cylinder, stone, t, new Vector3(0, h, 0), new Vector3(90, 0, 0), new Vector3(w + 1.2f, 0.4f, w + 0.4f));
            MeshKit.Part("Beam", MeshKit.Cube, stone, t, new Vector3(0, h + 0.15f, 0), Vector3.zero, new Vector3(w + 1.8f, 0.5f, 0.9f));
            MeshKit.Part("Dark", MeshKit.Cube, dark, t, new Vector3(0, h * 0.5f, -0.35f), Vector3.zero, new Vector3(w, h, 0.1f));

            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1 : 1) * (w * 0.5f + 0.75f);
                var torch = new GameObject("Torch" + i).transform;
                torch.SetParent(t, false);
                torch.localPosition = new Vector3(x, 0, 0.6f);
                MeshKit.Part("Stick", MeshKit.Cylinder, Mats.Lit(new Color(0.3f, 0.2f, 0.12f)), torch, new Vector3(0, 1.1f, 0), Vector3.zero, new Vector3(0.12f, 1.1f, 0.12f));
                MeshKit.Part("Bowl", MeshKit.Cone(1f, 1.4f, 10), Mats.Lit(new Color(0.25f, 0.2f, 0.18f)), torch, new Vector3(0, 2.2f, 0), Vector3.zero, new Vector3(0.18f, 0.2f, 0.18f));
                MeshKit.Part("Flame", MeshKit.Sphere, Mats.Fx(true, FxTex.Soft, new Color(1f, 0.55f, 0.15f, 1f) * 2.5f), torch, new Vector3(0, 2.6f, 0), Vector3.zero, new Vector3(0.6f, 0.9f, 0.6f));
                var l = new GameObject("TorchLight").AddComponent<Light>();
                l.transform.SetParent(torch, false);
                l.transform.localPosition = new Vector3(0, 2.8f, 0.3f);
                l.type = LightType.Point;
                l.range = 6f;
                l.intensity = 3f;
                l.color = new Color(1f, 0.6f, 0.25f);
                l.shadows = LightShadows.None;
                torchLights[i] = l;
            }
        }

        public void Tick(float now)
        {
            // flicker
            for (int i = 0; i < 2; i++)
                if (torchLights[i] != null) torchLights[i].intensity = 2.6f + Mathf.PerlinNoise(now * 6f, i * 3.1f) * 1.2f;
            if (Random.value < 0.25f && Fx.I != null)
                Fx.I.FireBurst(t.TransformPoint(new Vector3((Random.value < 0.5f ? -1 : 1) * 1.95f, 2.2f, 0.6f)));
        }

        public void Strike() => Fx.I.Lightning(SpawnPoint);
    }
}
