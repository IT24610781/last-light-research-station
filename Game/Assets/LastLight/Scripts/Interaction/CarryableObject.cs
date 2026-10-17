using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarryableObject : MonoBehaviour
{
    public Rigidbody Body { get; private set; }

    private void Awake()
    {
        Body = GetComponent<Rigidbody>();
    }
}