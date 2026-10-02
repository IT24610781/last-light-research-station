using System;
using UnityEngine;

public static class NoiseSystem
{
    public static event Action<NoiseEvent> NoiseProduced;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEvents()
    {
        NoiseProduced = null;
    }

    public static void Emit(
        Vector3 position,
        float strength,
        GameObject source)
    {
        NoiseEvent noise = new NoiseEvent(
            position,
            Mathf.Max(0f, strength),
            UnityEngine.Time.time,
            source);

        NoiseProduced?.Invoke(noise);
    }
}