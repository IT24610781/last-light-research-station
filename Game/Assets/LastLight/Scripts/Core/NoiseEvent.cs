using UnityEngine;

public readonly struct NoiseEvent
{
    public Vector3 Position { get; }
    public float Strength { get; }
    public float TimeCreated { get; }
    public GameObject Source { get; }

    public NoiseEvent(
        Vector3 position,
        float strength,
        float timeCreated,
        GameObject source)
    {
        Position = position;
        Strength = strength;
        TimeCreated = timeCreated;
        Source = source;
    }
}