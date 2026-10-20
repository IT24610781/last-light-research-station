using System.Collections.Generic;
using UnityEngine;

namespace LastLight.AI.Hunter
{
    /// <summary>One remembered noise. Positions are where the sound came from, not where the player is now.</summary>
    public sealed class NoiseMemory
    {
        public Vector3 Position { get; private set; }

        /// <summary>Strength after distance attenuation, as the Hunter heard it.</summary>
        public float PerceivedStrength { get; private set; }

        public float TimeHeard { get; private set; }

        /// <summary>Game time until which this noise is ignored because no route reached it.</summary>
        public float RejectedUntil { get; set; } = -1f;

        public NoiseMemory(Vector3 position, float perceivedStrength, float timeHeard)
        {
            Position = position;
            PerceivedStrength = perceivedStrength;
            TimeHeard = timeHeard;
        }

        /// <summary>A new sound near this one updates it instead of creating a duplicate.</summary>
        public void Refresh(Vector3 position, float perceivedStrength, float timeHeard)
        {
            Position = position;
            PerceivedStrength = Mathf.Max(PerceivedStrength, perceivedStrength);
            TimeHeard = timeHeard;
        }
    }

    /// <summary>
    /// What the Hunter knows but cannot currently sense: a bounded list of noises
    /// and the last place it saw the player. Old evidence is forgotten, so the
    /// Hunter acts on aging, incomplete information rather than the player's true position.
    /// </summary>
    public sealed class HunterMemory
    {
        private readonly List<NoiseMemory> noises = new List<NoiseMemory>();
        private readonly int capacity;
        private readonly float mergeRadius;

        public IReadOnlyList<NoiseMemory> Noises => noises;

        public bool HasLastSeen { get; private set; }
        public Vector3 LastSeenPosition { get; private set; }
        public float LastSeenTime { get; private set; }

        public HunterMemory(int capacity, float mergeRadius)
        {
            this.capacity = Mathf.Max(1, capacity);
            this.mergeRadius = mergeRadius;
        }

        public void AddNoise(Vector3 position, float perceivedStrength, float timeHeard)
        {
            float mergeRadiusSqr = mergeRadius * mergeRadius;
            foreach (NoiseMemory existing in noises)
            {
                if ((existing.Position - position).sqrMagnitude <= mergeRadiusSqr)
                {
                    existing.Refresh(position, perceivedStrength, timeHeard);
                    return;
                }
            }

            // Bounded memory: when full, forget the oldest noise.
            if (noises.Count >= capacity)
            {
                int oldest = 0;
                for (int i = 1; i < noises.Count; i++)
                    if (noises[i].TimeHeard < noises[oldest].TimeHeard) oldest = i;
                noises.RemoveAt(oldest);
            }

            noises.Add(new NoiseMemory(position, perceivedStrength, timeHeard));
        }

        public void Remove(NoiseMemory noise) => noises.Remove(noise);

        public bool Contains(NoiseMemory noise) => noise != null && noises.Contains(noise);

        public void SetLastSeen(Vector3 position, float time)
        {
            HasLastSeen = true;
            LastSeenPosition = position;
            LastSeenTime = time;
        }

        public void ClearLastSeen() => HasLastSeen = false;

        /// <summary>Removes evidence older than its memory duration.</summary>
        public void Forget(float now, float noiseDuration, float lastSeenDuration)
        {
            noises.RemoveAll(n => now - n.TimeHeard > noiseDuration);
            if (HasLastSeen && now - LastSeenTime > lastSeenDuration)
                HasLastSeen = false;
        }

        public void Clear()
        {
            noises.Clear();
            HasLastSeen = false;
        }
    }
}
