using UnityEngine;

[CreateAssetMenu(menuName = "Last Light/Gun Definition")]
public class GunDefinition : ScriptableObject
{
    [Min(1f)] public float damage = 25f;
    [Min(0.01f)] public float firingInterval = 0.15f;
    [Min(1f)] public float range = 50f;
    [Min(0f)] public float noiseStrength = 12f;
    [Min(1)] public int magazineCapacity = 12;
    [Min(0.1f)] public float reloadDuration = 2f;
    public bool automatic = true;
}