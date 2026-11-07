using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// <summary>
    /// Owns every fighter: pooled per costume (never created/destroyed during the stream, doc §10.4), ticks them,
    /// keeps the spatial grid and the list of dangerous areas (tornado, spin kick, shock wave) for the brains.
    /// </summary>
    public class FighterSystem
    {
        public readonly List<Fighter> Active = new List<Fighter>(128);
        public readonly List<Hazard> Hazards = new List<Hazard>(8);
        readonly Dictionary<int, Stack<Fighter>> pool = new Dictionary<int, Stack<Fighter>>();
        readonly SpatialGrid grid;
        readonly Transform root;
        readonly RuntimeAnimatorController controller;
        int nextUid = 1;
        public const float TargetHeight = 1.9f;
        static readonly List<Fighter> tmp = new List<Fighter>(64);

        public FighterSystem(Transform parent)
        {
            root = new GameObject("Fighters").transform;
            root.SetParent(parent, false);
            grid = new SpatialGrid(GameConfig.Current.arena.radius + 4f, 2f);
            controller = Resources.Load<RuntimeAnimatorController>("Anim/Fighter");
            if (controller == null) Debug.LogError("[Arena] Resources/Anim/Fighter.controller yok — Arena > Kurulum çalıştırın.");
        }

        public int AliveCount
        {
            get { int n = 0; foreach (var f in Active) if (f.IsAlive) n++; return n; }
        }

        public Fighter Spawn(Viewer v, Vector3 pos, Vector3 runTo, float now)
        {
            var f = Take(v.costumeId);
            Active.Add(f);
            f.Activate(v, pos, runTo, now);
            if (!f.IsBot) Sfx.I?.Join();
            v.state = ViewerState.InArena;
            return f;
        }

        public void Despawn(Fighter f)
        {
            if (!f.active) return;
            // release attack-slot counters others hold on this fighter
            foreach (var o in Active) if (o.brain.target == f) o.brain.ClearTarget();
            RemoveHazard(f);
            f.Deactivate();
            Active.Remove(f);
            if (!pool.TryGetValue(f.costume.id, out var st)) pool[f.costume.id] = st = new Stack<Fighter>();
            st.Push(f);
        }

        public void DespawnAll()
        {
            for (int i = Active.Count - 1; i >= 0; i--) Despawn(Active[i]);
        }

        public void Tick(float dt, float now)
        {
            grid.Rebuild(Active);
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                if (i >= Active.Count) continue;
                var f = Active[i];
                if (f.active) f.Tick(dt, now);
            }
        }

        public void Query(Vector3 p, float r, List<Fighter> results, Fighter exclude) => grid.Query(p, r, results, exclude);

        public Fighter NearestEnemy(Fighter f, float r)
        {
            grid.Query(f.Pos, r, tmp, f);
            Fighter best = null; float bd = float.MaxValue;
            foreach (var o in tmp)
            {
                if (!o.IsTargetable) continue;
                float d = (o.Pos - f.Pos).sqrMagnitude;
                if (d < bd) { bd = d; best = o; }
            }
            return best;
        }

        public void AddHazard(Fighter owner, float radius)
        {
            RemoveHazard(owner);
            Hazards.Add(new Hazard { owner = owner, radius = radius });
        }

        public void RemoveHazard(Fighter owner)
        {
            for (int i = Hazards.Count - 1; i >= 0; i--) if (Hazards[i].owner == owner) Hazards.RemoveAt(i);
        }

        // ------------------------------------------------------------------ construction
        Fighter Take(int costumeId)
        {
            if (pool.TryGetValue(costumeId, out var st) && st.Count > 0) return st.Pop();
            return Build(CostumeCatalog.Get(costumeId));
        }

        Fighter Build(Costume c)
        {
            var go = new GameObject("Fighter");
            go.transform.SetParent(root, false);
            var f = go.AddComponent<Fighter>();
            var body = new GameObject("Body").transform;
            body.SetParent(go.transform, false);

            string modelName = CostumeCatalog.StarterModel;
            var prefab = Resources.Load<GameObject>("Stickman/" + modelName);
            GameObject modelGo;
            Animator animator;
            if (prefab != null)
            {
                modelGo = Object.Instantiate(prefab, body, false);
                animator = modelGo.GetComponent<Animator>();
                if (animator == null) animator = modelGo.AddComponent<Animator>();
            }
            else
            {
                // fallback when the licensed pack is missing (fresh clone of the public repo): Quaternius mannequin
                Debug.LogWarning("[Arena] Model yok: Resources/Stickman/" + modelName);
                modelGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.Destroy(modelGo.GetComponent<Collider>());
                modelGo.transform.SetParent(body, false);
                modelGo.transform.localPosition = Vector3.up;
                animator = modelGo.AddComponent<Animator>();
            }
            modelGo.name = "Model";
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            animator.Rebind();
            animator.Update(0f);

            foreach (var r in modelGo.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
            }

            // normalise height so every costume is the same size
            var rig = new FighterRig { info = Accessories.Measure(modelName, body, animator) };
            float scale = rig.info.height > 0.1f ? TargetHeight / rig.info.height : 1f;
            modelGo.transform.localScale *= scale;
            animator.Update(0f);
            rig.info = Accessories.Measure(modelName + "@n", body, animator);

            rig.head = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : modelGo.transform;
            rig.neck = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Neck) : modelGo.transform;
            rig.chest = animator.isHuman ? (animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest)) : modelGo.transform;
            rig.hips = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : modelGo.transform;
            rig.handR = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            rig.handL = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            rig.footR = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightFoot) : null;

            Accessories.CaptureRest(body, rig.head, rig.neck, rig.chest, rig.hips, rig.handL, rig.handR, rig.footR);
            Gear.PrepareBase(rig, modelGo.transform);
            rig.crown = Accessories.BuildGifterCrown(rig, body);
            if (System.Environment.CommandLine.Contains("-gallery") && rig.accessoryRenderers.Count > 0)
            {
                var a0 = rig.accessoryRenderers[0];
                Debug.Log($"[Arena] {c.name}: h={rig.info.height:0.00} headR={rig.info.headR:0.00} top={rig.info.headTop:0.00} hc={rig.info.headCenter} faceZ={rig.info.faceZ:0.00} acc={rig.accessoryRenderers.Count} acc0 world={a0.transform.position} lossy={a0.transform.lossyScale} bounds={a0.bounds.size} head={rig.head.position} headLossy={rig.head.lossyScale} active={a0.gameObject.activeInHierarchy} mat={a0.sharedMaterial?.shader?.name}");
            }

            var relay = modelGo.AddComponent<AnimEventRelay>();
            relay.fighter = f;

            // round blob shadow + gifter aura under the feet
            f.blob = MeshKit.Part("Blob", MeshKit.Quad, Mats.Fx(false, FxTex.Soft, new Color(0, 0, 0, 0.45f)), go.transform, new Vector3(0, 0.08f, 0), new Vector3(90, 0, 0), new Vector3(1.1f, 1.1f, 1f)).GetComponent<Renderer>();
            f.aura = Fx.AttachedGlow(go.transform, new Color(1f, 0.8f, 0.2f, 0.75f), 0.9f);
            f.aura.enabled = false;

            Fighter.Layerize(go);
            f.Setup(nextUid++, c, body, modelGo.transform, animator, rig, scale);
            go.SetActive(false);
            return f;
        }
    }
}
