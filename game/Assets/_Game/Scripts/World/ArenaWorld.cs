using UnityEngine;

namespace Arena
{
    /// <summary>
    /// The arena (doc §8.1): round floor of radius 12 m with a low wall, two gates (left and right as seen from the
    /// camera, arch + two torches), dark background, warm light focused on the arena. Uses the gladiator arena
    /// model when it is present, otherwise builds a simple stone ring from code.
    /// </summary>
    public class ArenaWorld
    {
        public readonly Transform root;
        public readonly SpawnGate[] gates = new SpawnGate[2];
        public Transform boardAnchor;
        public Vector3 CamForwardFlat { get; private set; }
        public Vector3 CamRightFlat { get; private set; }

        public ArenaWorld(Transform parent)
        {
            root = new GameObject("World").transform;
            root.SetParent(parent, false);
            var cfg = GameConfig.Current;
            float R = cfg.arena.radius;
            float yaw = cfg.camera.yawDeg * Mathf.Deg2Rad;
            CamForwardFlat = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
            CamRightFlat = new Vector3(Mathf.Cos(yaw), 0, -Mathf.Sin(yaw));

            BuildLights();
            bool model = BuildModel(R);
            BuildFloorAndWall(R, !model);

            // gates at the left and right of the screen
            gates[0] = new SpawnGate(root, -CamRightFlat * (R + 0.3f), "Sol Kapı");
            gates[1] = new SpawnGate(root, CamRightFlat * (R + 0.3f), "Sağ Kapı");

            // the scoreboard stands behind the arena, facing the camera
            boardAnchor = new GameObject("BoardAnchor").transform;
            boardAnchor.SetParent(root, false);
            boardAnchor.position = CamForwardFlat * (R + cfg.board.distanceBehind) + Vector3.up * cfg.board.height;
            boardAnchor.rotation = Quaternion.LookRotation(CamForwardFlat, Vector3.up);
        }

        void BuildLights()
        {
            var sun = new GameObject("KeyLight").AddComponent<Light>();
            sun.transform.SetParent(root, false);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.86f, 0.68f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.None;
            sun.transform.rotation = Quaternion.Euler(52f, GameConfig.Current.camera.yawDeg - 35f, 0);

            var fill = new GameObject("FillLight").AddComponent<Light>();
            fill.transform.SetParent(root, false);
            fill.type = LightType.Directional;
            fill.color = new Color(0.55f, 0.6f, 0.95f);
            fill.intensity = 0.35f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(35f, GameConfig.Current.camera.yawDeg + 160f, 0);

            // warm spot on the centre: the light is focused on the arena, the background stays dark
            var spot = new GameObject("CenterGlow").AddComponent<Light>();
            spot.transform.SetParent(root, false);
            spot.type = LightType.Point;
            spot.range = 22f;
            spot.intensity = 6f;
            spot.color = new Color(1f, 0.7f, 0.45f);
            spot.transform.position = new Vector3(0, 7f, 0);
        }

        bool BuildModel(float R)
        {
            var prefab = Resources.Load<GameObject>("ArenaModel/Arena");
            if (prefab == null) return false;
            var go = Object.Instantiate(prefab, root, false);
            go.name = "ArenaModel";
            Renderer floor = null;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (r.name == "Arena") floor = r;
                // the bone spikes stick out in front of the scoreboard and the near wall: keep the view clean
                if (r.name == "WoodSpikes") r.enabled = false;
            }
            if (floor == null) return true;

            // model space (identity transform): the ring is round in x; the gate (Portal) sticks out toward -z
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            var b0 = floor.bounds;
            var ringCenter = new Vector3(b0.center.x, 0, b0.max.z - b0.size.x * 0.5f);
            float outer = b0.size.x * 0.5f;
            var col = floor.gameObject.AddComponent<MeshCollider>();

            float s = ArenaModelFit.FootprintToRadius * R / Mathf.Max(0.01f, outer);
            float wall = Place(go, floor, ringCenter, s);
            // second pass: scale so the inner wall sits just outside the 12 m fighting circle
            if (wall > 1f && wall < 1000f)
            {
                s *= (R + 0.7f) / wall;
                wall = Place(go, floor, ringCenter, s);
            }
            Debug.Log($"[Arena] Arena modeli: ölçek {s:0.00}, iç duvar {wall:0.0} m");
            Object.Destroy(col);
            return true;
        }

        /// <summary>Scale, rotate (gate to the left of the screen), centre the ring, put the floor at y=0.
        /// Returns the median distance from the centre to the inner wall.</summary>
        float Place(GameObject go, Renderer floor, Vector3 ringCenter, float s)
        {
            var rot = Quaternion.Euler(0, ArenaModelFit.YawOffset + GameConfig.Current.camera.yawDeg, 0);
            go.transform.localScale = Vector3.one * s;
            go.transform.rotation = rot;
            var c = rot * (ringCenter * s);
            go.transform.position = new Vector3(-c.x, 0, -c.z);
            Physics.SyncTransforms();
            var b = floor.bounds;
            float floorY = b.min.y;
            if (Physics.Raycast(new Vector3(0, b.max.y + 5f, 0), Vector3.down, out var hit, 500f)) floorY = hit.point.y;
            go.transform.position += Vector3.up * (-floorY + ArenaModelFit.FloorLift);
            Physics.SyncTransforms();
            var d = new System.Collections.Generic.List<float>();
            for (int a = 0; a < 16; a++)
            {
                var dir = Quaternion.Euler(0, a * 22.5f, 0) * Vector3.forward;
                if (Physics.Raycast(new Vector3(0, 1.0f, 0), dir, out var h, 200f)) d.Add(h.distance);
            }
            if (d.Count == 0) return -1f;
            d.Sort();
            return d[d.Count / 2];
        }

        void BuildFloorAndWall(float R, bool full)
        {
            // fighting circle: subtle sand disc (always) so the 12 m circle reads clearly on the phone
            var sand = MeshKit.Part("FightCircle", MeshKit.Cone(1f, 1f, 64, true), Mats.Lit(new Color(0.78f, 0.62f, 0.42f)), root, new Vector3(0, -0.05f, 0), Vector3.zero, new Vector3(R + 0.2f, 0.06f, R + 0.2f));
            sand.SetActive(full || ArenaModelFit.DrawSand);
            var edge = MeshKit.Part("CircleEdge", MeshKit.FlatRing(0.985f, 1.02f, 96), Mats.Fx(false, Texture2D.whiteTexture, new Color(1f, 0.85f, 0.55f, 0.35f)), root, new Vector3(0, 0.02f, 0), Vector3.zero, new Vector3(R, 1, R));
            if (!full) return;
            // simple low stone wall
            var wall = Mats.Lit(new Color(0.42f, 0.36f, 0.32f));
            MeshKit.Part("Wall", MeshKit.Tube(1f, 1f, 64), wall, root, Vector3.zero, Vector3.zero, new Vector3(R + 0.4f, 0.9f, R + 0.4f));
            MeshKit.Part("WallTop", MeshKit.Torus(1f, 0.02f, 64, 6), wall, root, new Vector3(0, 0.9f, 0), Vector3.zero, new Vector3(R + 0.4f, 10f, R + 0.4f));
            MeshKit.Part("Ground", MeshKit.Cone(1f, 1f, 48, true), Mats.Lit(new Color(0.12f, 0.1f, 0.1f)), root, new Vector3(0, -0.3f, 0), Vector3.zero, new Vector3(R * 3f, 0.2f, R * 3f));
        }
    }

    /// <summary>Hand-tuned numbers for fitting the gladiator arena model (set after looking at it).</summary>
    public static class ArenaModelFit
    {
        public static float FootprintToRadius = 1.55f;
        public static float YawOffset = 90f;   // the model's gate (Portal) ends up at the left of the screen
        public static float FloorLift = 0f;
        public static bool DrawSand = false;
    }
}
