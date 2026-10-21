using UnityEngine;

namespace LastLight.Atmosphere
{
    /// <summary>
    /// Makes a realtime light flicker and occasionally black out, like failing lab power.
    /// Must be on a Realtime light: baked lights cannot change at runtime.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class FlickerLight : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float flickerSpeed = 12f;
        [SerializeField, Range(0f, 1f)] private float flickerDepth = 0.35f;
        [Tooltip("Chance per second that the light cuts out briefly.")]
        [SerializeField, Range(0f, 1f)] private float blackoutChance = 0.05f;
        [SerializeField, Min(0.02f)] private float blackoutDuration = 0.18f;

        private Light cachedLight;
        private float baseIntensity;
        private float seed;
        private float blackoutUntil;

        private void Awake()
        {
            cachedLight = GetComponent<Light>();
            baseIntensity = cachedLight.intensity;
            seed = Random.value * 100f;
        }

        private void OnDisable()
        {
            if (cachedLight != null) cachedLight.intensity = baseIntensity;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return; // paused

            if (Time.time >= blackoutUntil && Random.value < blackoutChance * Time.deltaTime)
            {
                blackoutUntil = Time.time + blackoutDuration;
            }

            if (Time.time < blackoutUntil)
            {
                cachedLight.intensity = baseIntensity * 0.05f;
                return;
            }

            float noise = Mathf.PerlinNoise(seed, Time.time * flickerSpeed);
            cachedLight.intensity = baseIntensity * (1f - flickerDepth + flickerDepth * noise);
        }
    }
}
