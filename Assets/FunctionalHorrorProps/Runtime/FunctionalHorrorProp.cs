using System;
using UnityEngine;

namespace FunctionalHorrorProps
{
    public enum PropMotionMode { Rotate, Slide, Spin, Rock, Oscillate, Flicker, StretchCable }

    [Serializable]
    public sealed class PropMotion
    {
        public Transform target;
        public Transform start;
        public Transform end;
        public PropMotionMode mode;
        public Vector3 axis = Vector3.up;
        [Tooltip("Degrees for rotation; metres for sliding; degrees/second for spinning.")]
        public float amount = 90f;
        [Min(0f)] public float frequency = 1f;
        public float phase;
        [Tooltip("Rocking runner radius in metres; 0 disables rolling translation.")] public float rollingRadius;
        public Light light;
        [Tooltip("Active light intensity; negative captures the scene light value.")]
        public float activeLightIntensity = -1f;
        public Renderer renderer;
        [ColorUsage(true, true)] public Color emissionColor = new Color(2f, 0.15f, 0.03f);

        [NonSerialized] internal Vector3 restPosition;
        [NonSerialized] internal Vector3 restScale;
        [NonSerialized] internal Quaternion restRotation;
        [NonSerialized] internal float restIntensity;
        [NonSerialized] internal MaterialPropertyBlock block;
        [NonSerialized] internal MaterialPropertyBlock restBlock;
        [NonSerialized] internal float spinSeconds;
    }

    /// <summary>Local-space interactions for articulated models. No Animator or input package required.</summary>
    [DisallowMultipleComponent]
    public sealed class FunctionalHorrorProp : MonoBehaviour
    {
        [SerializeField] private string displayName = "Horror prop";
        [SerializeField] private PropMotion[] motions = Array.Empty<PropMotion>();
        [SerializeField, Min(0.01f)] private float transitionSeconds = 0.8f;
        [SerializeField] private bool startActive;
        [Tooltip("Restore the authored pose when disabled; otherwise freeze and resume.")]
        [SerializeField] private bool restoreOnDisable = true;
        [SerializeField] private AudioSource loopAudio;

        private bool initialized;
        private AudioSource capturedAudio;
        private float authoredAudioVolume;
        private bool activeState;
        private float activeAmount;
        private bool settled;
        private float elapsed;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        public string DisplayName => displayName;
        public bool IsActive => activeState;
        public bool HasInteraction => motions != null && motions.Length > 0;
        public float ActiveAmount => activeAmount;

        private void Awake() { Initialize(); CaptureAudioSettings(); ApplyPose(activeState ? 1f : 0f, 0f); }

        /// <summary>Assign an optional looping sound; its authored volume is the fully active level.</summary>
        public void ConfigureAudio(AudioSource source)
        {
            StopAudio();
            loopAudio = source;
            capturedAudio = null;
            CaptureAudioSettings();
        }

        private void CaptureAudioSettings()
        {
            if (loopAudio == null || capturedAudio == loopAudio) return;
            capturedAudio = loopAudio;
            authoredAudioVolume = loopAudio.volume;
            loopAudio.playOnAwake = false;
            loopAudio.loop = true;
        }

        private void UpdateAudio()
        {
            CaptureAudioSettings();
            if (loopAudio == null) return;
            if (activeAmount > 0f)
            {
                loopAudio.volume = authoredAudioVolume * Mathf.SmoothStep(0f, 1f, activeAmount);
                if (!loopAudio.isPlaying && loopAudio.clip != null) loopAudio.Play();
            }
            else StopAudio();
        }

        private void StopAudio()
        {
            if (capturedAudio == null) return;
            capturedAudio.Stop();
            capturedAudio.volume = authoredAudioVolume;
        }

        public void Configure(string label, PropMotion[] entries, float duration = 0.8f)
        {
            if (initialized) RestorePose();
            displayName = label;
            motions = entries ?? Array.Empty<PropMotion>();
            transitionSeconds = Mathf.Max(0.01f, duration);
            initialized = false;
            Initialize();
        }

        /// <summary>Capture authored local transforms once, after the model hierarchy exists.</summary>
        public void Initialize()
        {
            if (initialized) return;
            if (motions == null) motions = Array.Empty<PropMotion>();
            foreach (PropMotion motion in motions)
            {
                if (motion == null) continue;
                motion.spinSeconds = 0f;
                if (motion.target != null)
                {
                    motion.restPosition = motion.target.localPosition;
                    motion.restScale = motion.target.localScale;
                    motion.restRotation = motion.target.localRotation;
                }
                if (motion.light != null) motion.restIntensity = motion.activeLightIntensity >= 0f ? motion.activeLightIntensity : motion.light.intensity;
                if (motion.renderer != null)
                {
                    motion.block = new MaterialPropertyBlock();
                    motion.restBlock = new MaterialPropertyBlock();
                    motion.renderer.GetPropertyBlock(motion.restBlock);
                }
            }
            initialized = true;
            activeState = startActive;
            activeAmount = startActive ? 1f : 0f;
            elapsed = 0f;
            settled = false;
        }

        /// <summary>Start this prop's configured animation and optional sound.</summary>
        public void Activate() { SetActiveState(true); }
        /// <summary>Stop looping motion or close a mechanism smoothly.</summary>
        public void Deactivate() { SetActiveState(false); }
        public void Toggle() { Initialize(); SetActiveState(!activeState); }
        public void SetActiveState(bool value) { Initialize(); activeState = value; settled = false; }

        private void Update()
        {
            Initialize();
            if (settled) return;
            activeAmount = Mathf.MoveTowards(activeAmount, activeState ? 1f : 0f,
                Time.deltaTime / Mathf.Max(0.01f, transitionSeconds));
            UpdateAudio();
            elapsed += Time.deltaTime;
            ApplyPose(Mathf.SmoothStep(0f, 1f, activeAmount), elapsed);
            // Integrate speed during live playback so switching a motor off coasts to a stop,
            // rather than rewinding its accumulated angle as the activation amount fades.
            foreach (PropMotion motion in motions)
            {
                if (motion == null || motion.mode != PropMotionMode.Spin || motion.target == null) continue;
                motion.spinSeconds += Time.deltaTime * Mathf.SmoothStep(0f, 1f, activeAmount);
                Vector3 axis = motion.axis.sqrMagnitude > 0.000001f ? motion.axis.normalized : Vector3.up;
                motion.target.localRotation = motion.restRotation
                    * Quaternion.AngleAxis(motion.amount * motion.spinSeconds, axis);
            }
            settled = !activeState && activeAmount <= 0f;
        }

        /// <summary>Deterministic preview/test pose. Does not change the requested toggle state.</summary>
        public void ApplyPose(float amount, float seconds)
        {
            Initialize();
            amount = Mathf.Clamp01(amount);
            foreach (PropMotion motion in motions)
            {
                if (motion == null || motion.mode == PropMotionMode.StretchCable) continue;
                Vector3 axis = motion.axis.sqrMagnitude > 0.000001f ? motion.axis.normalized : Vector3.up;
                float wave = Mathf.Sin(seconds * motion.frequency * Mathf.PI * 2f + motion.phase);
                if (motion.mode == PropMotionMode.Flicker)
                {
                    // Fixed harmonics provide irregular flicker without random global state.
                    float pulse = Mathf.Sin(seconds * 39.7f + motion.phase)
                        + 0.55f * Mathf.Sin(seconds * 81.3f + 1.7f);
                    float strength = amount * (pulse > -0.35f ? 1f : 0.06f);
                    if (motion.light != null) motion.light.intensity = motion.restIntensity * strength;
                    if (motion.renderer != null)
                    {
                        motion.renderer.GetPropertyBlock(motion.block);
                        motion.block.SetColor(EmissionColor, motion.emissionColor * strength);
                        motion.renderer.SetPropertyBlock(motion.block);
                    }
                    continue;
                }
                if (motion.target == null) continue;
                if (motion.mode == PropMotionMode.Slide)
                    motion.target.localPosition = motion.restPosition + axis * (motion.amount * amount);
                else
                {
                    float angle = motion.amount * amount;
                    if (motion.mode == PropMotionMode.Spin) angle *= seconds;
                    else if (motion.mode == PropMotionMode.Rock || motion.mode == PropMotionMode.Oscillate)
                        angle *= wave;
                    motion.target.localRotation = motion.restRotation * Quaternion.AngleAxis(angle, axis);
                    if (motion.mode == PropMotionMode.Rock && motion.rollingRadius > 0f)
                        motion.target.localPosition = motion.restPosition + Vector3.forward * (angle * Mathf.Deg2Rad * motion.rollingRadius);
                }
            }
            // Flexible connectors are evaluated after the mechanisms that move their endpoints.
            foreach (PropMotion motion in motions)
            {
                if (motion == null || motion.mode != PropMotionMode.StretchCable
                    || motion.target == null || motion.start == null || motion.end == null) continue;
                Transform parent = motion.target.parent;
                Vector3 delta = motion.end.position - motion.start.position;
                Vector3 localDelta = parent != null ? parent.InverseTransformVector(delta) : delta;
                motion.target.localPosition = parent != null ? parent.InverseTransformPoint(motion.start.position) : motion.start.position;
                if (localDelta.sqrMagnitude < 0.000001f) continue;
                motion.target.localRotation = Quaternion.FromToRotation(Vector3.up, localDelta.normalized);
                motion.target.localScale = new Vector3(motion.restScale.x, localDelta.magnitude, motion.restScale.z);
            }
        }

        /// <summary>Showcase/preview pose: doors and drawers follow <paramref name="openAmount"/>,
        /// while continuous motions (flicker, spin, rock, oscillate) always run at full and are
        /// driven by <paramref name="seconds"/>. Lets a preview open a door without dimming a lamp.</summary>
        public void ApplyShowcasePose(float openAmount, float seconds)
        {
            Initialize();
            openAmount = Mathf.Clamp01(openAmount);
            foreach (PropMotion motion in motions)
            {
                if (motion == null || motion.mode == PropMotionMode.StretchCable) continue;
                float amount = (motion.mode == PropMotionMode.Rotate || motion.mode == PropMotionMode.Slide) ? openAmount : 1f;
                Vector3 axis = motion.axis.sqrMagnitude > 0.000001f ? motion.axis.normalized : Vector3.up;
                float wave = Mathf.Sin(seconds * motion.frequency * Mathf.PI * 2f + motion.phase);
                if (motion.mode == PropMotionMode.Flicker)
                {
                    float pulse = Mathf.Sin(seconds * 39.7f + motion.phase)
                        + 0.55f * Mathf.Sin(seconds * 81.3f + 1.7f);
                    float strength = amount * (pulse > -0.35f ? 1f : 0.06f);
                    if (motion.light != null) motion.light.intensity = motion.restIntensity * strength;
                    if (motion.renderer != null)
                    {
                        motion.renderer.GetPropertyBlock(motion.block);
                        motion.block.SetColor(EmissionColor, motion.emissionColor * strength);
                        motion.renderer.SetPropertyBlock(motion.block);
                    }
                    continue;
                }
                if (motion.target == null) continue;
                if (motion.mode == PropMotionMode.Slide)
                    motion.target.localPosition = motion.restPosition + axis * (motion.amount * amount);
                else
                {
                    float angle = motion.amount * amount;
                    if (motion.mode == PropMotionMode.Spin) angle *= seconds;
                    else if (motion.mode == PropMotionMode.Rock || motion.mode == PropMotionMode.Oscillate)
                        angle *= wave;
                    motion.target.localRotation = motion.restRotation * Quaternion.AngleAxis(angle, axis);
                    if (motion.mode == PropMotionMode.Rock && motion.rollingRadius > 0f)
                        motion.target.localPosition = motion.restPosition + Vector3.forward * (angle * Mathf.Deg2Rad * motion.rollingRadius);
                }
            }
        }

        public void RestorePose()
        {
            if (!initialized) return;
            foreach (PropMotion motion in motions)
            {
                if (motion == null) continue;
                if (motion.target != null)
                {
                    motion.target.localPosition = motion.restPosition;
                    motion.target.localScale = motion.restScale;
                    motion.target.localRotation = motion.restRotation;
                }
                if (motion.light != null) motion.light.intensity = motion.restIntensity;
                if (motion.renderer != null) motion.renderer.SetPropertyBlock(motion.restBlock);
            }
        }

        private void OnDisable()
        {
            StopAudio();
            if (!restoreOnDisable || !initialized) return;
            RestorePose();
            activeState = false;
            activeAmount = 0f;
            elapsed = 0f;
            settled = false;
            foreach (PropMotion motion in motions)
                if (motion != null) motion.spinSeconds = 0f;
        }
    }
}
