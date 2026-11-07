using System;
using System.Collections.Generic;
using UnityEngine;

namespace FunctionalHorrorProps
{
    /// <summary>
    /// Drop-in life for a parked vehicle: a single <c>speedKmh</c> dial spins every
    /// wheel at the matching angular velocity and feeds procedurally-built exhaust smoke.
    /// No Animator, input package, texture or material asset is required — the component
    /// finds the wheels and tailpipe from the imported GLB node names and builds its own
    /// particle smoke at runtime. Add it to a vehicle root and press "Auto-wire", or just
    /// enter play mode and it wires itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FunctionalHorrorVehicle : MonoBehaviour
    {
        [Header("Drive")]
        [Tooltip("Road speed in km/h. 0 = parked: wheels still, engine idles a wisp of smoke.")]
        [Range(0f, 140f)] public float speedKmh = 0f;
        [Tooltip("Flip this if the wheels visually roll the wrong way for your import.")]
        public bool reverseWheelSpin = false;
        [Tooltip("Spin wheels and emit smoke while editing (not only in play mode).")]
        public bool runInEditMode = false;

        [Header("Wheels (auto-filled)")]
        public List<Wheel> wheels = new List<Wheel>();

        [Header("Exhaust smoke")]
        public bool emitExhaust = true;
        [Tooltip("Idle produces a thin wisp even when parked; off = smoke only while moving.")]
        public bool engineRunning = true;
        [Range(0f, 3f)] public float smokeDensity = 1f;
        [Tooltip("Sooty exhaust tint (alpha sets opacity).")]
        public Color smokeColor = new Color(0.30f, 0.29f, 0.27f, 0.55f);
        [Tooltip("Tailpipe nodes smoke is emitted from (auto-filled with the rearmost).")]
        public List<Transform> exhaustPoints = new List<Transform>();

        [Serializable]
        public sealed class Wheel
        {
            public Transform pivot;
            [Tooltip("Axle axis in the vehicle's local space. vehicles roll about X.")]
            public Vector3 spinAxis = Vector3.right;
            [Min(0.02f)] public float radius = 0.34f;
            [NonSerialized] public float angle;
        }

        private readonly List<ParticleSystem> smoke = new List<ParticleSystem>();
        private bool wired;
        private const float WheelMergeFactor = 1.4f; // parts within radius*factor join a wheel

        private void OnEnable()
        {
            if (wheels.Count == 0 && exhaustPoints.Count == 0) AutoWire();
            BuildSmoke();
        }

        private void Reset() { AutoWire(); }

        private void Update()
        {
            if (!Application.isPlaying && !runInEditMode) return;
            float dt = Application.isPlaying ? Time.deltaTime : 1f / 60f;
            SpinWheels(dt);
            DriveSmoke();
        }

        private void SpinWheels(float dt)
        {
            float v = speedKmh / 3.6f;                      // m/s
            float dir = reverseWheelSpin ? -1f : 1f;
            foreach (Wheel w in wheels)
            {
                if (w == null || w.pivot == null) continue;
                float degPerSec = (v / Mathf.Max(0.02f, w.radius)) * Mathf.Rad2Deg * dir;
                w.angle = Mathf.Repeat(w.angle + degPerSec * dt, 360f);
                Vector3 axis = w.spinAxis.sqrMagnitude > 1e-6f ? w.spinAxis.normalized : Vector3.right;
                w.pivot.localRotation = Quaternion.AngleAxis(w.angle, axis);
            }
        }

        private void DriveSmoke()
        {
            if (smoke.Count == 0) return;
            // Idle wisp + a plume that grows with speed, all scaled by density.
            float idle = engineRunning ? 0.18f : 0f;
            float load = Mathf.Clamp01(speedKmh / 90f);
            float rate = (idle + load * 1.0f) * 26f * smokeDensity;
            float burst = (0.25f + load) * (0.9f + 0.6f * load);
            foreach (ParticleSystem ps in smoke)
            {
                if (ps == null) continue;
                var em = ps.emission;
                em.rateOverTime = rate;
                var main = ps.main;
                main.startColor = smokeColor;
                main.startSpeed = 0.25f + load * 1.6f;
                main.startLifetime = 2.6f - load * 0.9f;
                bool on = emitExhaust && rate > 0.01f;
                if (on && !ps.isEmitting) ps.Play();
                else if (!on && ps.isEmitting) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        /// <summary>Find wheels and the tailpipe from GLB node names and build pivots.</summary>
        [ContextMenu("Auto-wire wheels and exhaust")]
        public void AutoWire()
        {
            Unrig();
            var all = GetComponentsInChildren<Transform>(true);

            // Tyres mark wheel centres; rims/hubs/lugs/spokes join the nearest tyre.
            var tyres = new List<Transform>();
            foreach (var t in all)
            {
                string n = t.name.ToLowerInvariant();
                if ((n.Contains("tyre") || n.Contains("tire")) && !n.Contains("spare")) tyres.Add(t);
            }
            var claimed = new HashSet<Transform>();
            foreach (var tyre in tyres)
            {
                float radius = EstimateRadius(tyre);
                var pivot = new GameObject("_WheelPivot").transform;
                pivot.SetParent(transform, false);
                pivot.localRotation = Quaternion.identity;
                pivot.position = WorldCentre(tyre);
                float merge = radius * WheelMergeFactor;
                foreach (var part in all)
                {
                    if (part == transform || claimed.Contains(part)) continue;
                    if (part.parent == null || !IsWheelPart(part.name)) continue;
                    if ((WorldCentre(part) - pivot.position).sqrMagnitude <= merge * merge)
                    {
                        part.SetParent(pivot, true);
                        claimed.Add(part);
                    }
                }
                // Axle is the vehicle's local X; convert to pivot space (pivot has identity local rot).
                wheels.Add(new Wheel { pivot = pivot, spinAxis = Vector3.right, radius = radius });
            }

            // Exhaust: the rearmost / most downstream pipe-like node (most negative local Z).
            Transform tail = null; float bestZ = float.PositiveInfinity;
            foreach (var t in all)
            {
                string n = t.name.ToLowerInvariant();
                bool pipe = n.Contains("exhaust") || n.Contains("muffler") || n.Contains("tailpipe")
                    || n.Contains("silencer") || (n.Contains("stack") && !n.Contains("stackcap"));
                if (!pipe) continue;
                float lz = transform.InverseTransformPoint(WorldCentre(t)).z;
                if (lz < bestZ) { bestZ = lz; tail = t; }
            }
            if (tail != null) exhaustPoints.Add(tail);
            wired = true;
            BuildSmoke();
        }

        private static bool IsWheelPart(string name)
        {
            string n = name.ToLowerInvariant();
            if (n.Contains("steering") || n.Contains("fly") || n.Contains("hand")
                || n.Contains("arch") || n.Contains("well") || n.Contains("house")
                || n.Contains("stop") || n.Contains("wheelchair")) return false;
            return n.Contains("tyre") || n.Contains("tire") || n.Contains("rim") || n.Contains("hub")
                || n.Contains("lug") || n.Contains("spoke") || n.Contains("sidewall") || n.Contains("wheel");
        }

        private static Vector3 WorldCentre(Transform t)
        {
            var r = t.GetComponent<Renderer>();
            return r != null ? r.bounds.center : t.position;
        }

        private static float EstimateRadius(Transform tyre)
        {
            var r = tyre.GetComponent<Renderer>();
            if (r == null) return 0.34f;
            Vector3 e = r.bounds.extents;
            // Tyre is a disc: the two largest extents are its face; radius is their mean.
            float a = Mathf.Max(e.x, Mathf.Max(e.y, e.z));
            float b = (e.x + e.y + e.z) - a - Mathf.Min(e.x, Mathf.Min(e.y, e.z));
            return Mathf.Max(0.08f, (a + b) * 0.5f);
        }

        /// <summary>Return wheel parts to the root and remove generated pivots/smoke — idempotent.</summary>
        [ContextMenu("Remove generated rig")]
        public void Unrig()
        {
            foreach (var pivot in GetComponentsInChildren<Transform>(true))
            {
                if (pivot == null || pivot.name != "_WheelPivot") continue;
                for (int i = pivot.childCount - 1; i >= 0; i--) pivot.GetChild(i).SetParent(transform, true);
            }
            var pivots = new List<Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t != null && t.name == "_WheelPivot") pivots.Add(t);
            foreach (var p in pivots) DestroyImmediateSafe(p.gameObject);
            RemoveGeneratedSmoke();
            wheels.Clear();
            exhaustPoints.Clear();
        }

        private void RemoveGeneratedSmoke()
        {
            smoke.Clear();
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t != null && t.name == "_ExhaustSmoke") DestroyImmediateSafe(t.gameObject);
        }

        private void BuildSmoke()
        {
            RemoveGeneratedSmoke();
            if (!emitExhaust) return;
            foreach (var pt in exhaustPoints)
            {
                if (pt == null) continue;
                var go = new GameObject("_ExhaustSmoke");
                go.transform.SetParent(pt, false);
                go.transform.localPosition = Vector3.zero;
                // Emit back along the vehicle and up: face local -Z of the vehicle.
                go.transform.rotation = Quaternion.LookRotation(
                    transform.TransformDirection(new Vector3(0f, 0.5f, -1f)).normalized, Vector3.up);
                var ps = go.AddComponent<ParticleSystem>();
                ConfigureSmoke(ps);
                smoke.Add(ps);
            }
        }

        private void ConfigureSmoke(ParticleSystem ps)
        {
            ps.Stop();
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 2.4f;
            main.startSpeed = 0.4f;
            main.startSize = 0.12f;
            main.startColor = smokeColor;
            main.gravityModifier = -0.04f;           // drifts upward
            main.maxParticles = 220;
            main.playOnAwake = false;

            var em = ps.emission; em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 11f; shape.radius = 0.035f;

            var sol = ps.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(1f, 3.4f)));

            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f),
                        new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var vel = ps.velocityOverLifetime; vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.y = new ParticleSystem.MinMaxCurve(0.35f);

            var rot = ps.rotationOverLifetime; rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.material = SmokeMaterial();
            r.sortingFudge = 2f;
        }

        private static Material cachedSmokeMat;
        private static Material SmokeMaterial()
        {
            if (cachedSmokeMat != null) return cachedSmokeMat;
            Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply")
                ?? Shader.Find("Sprites/Default");
            var m = new Material(s) { name = "ExhaustSmoke (generated)" };
            m.mainTexture = SoftDot();
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);   // URP: transparent
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);       // alpha blend
            m.renderQueue = 3000;
            cachedSmokeMat = m;
            return m;
        }

        private static Texture2D cachedDot;
        private static Texture2D SoftDot()
        {
            if (cachedDot != null) return cachedDot;
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x + 0.5f) / S - 0.5f, dy = (y + 0.5f) / S - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (0.65f + 0.35f * Mathf.PerlinNoise(x * 0.3f, y * 0.3f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            cachedDot = tex;
            return tex;
        }

        private static void DestroyImmediateSafe(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }
    }
}
