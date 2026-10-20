using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerDeathHandler : MonoBehaviour
{
    private Health health;
    private PlayerInput playerInput;
    private PlayerGrabber grabber;
    private Behaviour[] playerBehaviours;

    private bool deathHandled;

    private void Awake()
    {
        health = GetComponent<Health>();
        playerInput = GetComponent<PlayerInput>();
        grabber = GetComponent<PlayerGrabber>();

        playerBehaviours = new Behaviour[]
        {
            GetComponent<PlayerShooter>(),
            GetComponent<PlayerInteractor>(),
            GetComponent<PlayerGrabber>(),
            GetComponent<PlayerSprintNoise>(),
            GetComponent<PlayerMovement>(),
            GetComponent<PlayerLook>()
        };
    }

    private void OnEnable()
    {
        health.Died += HandleDeath;
    }

    private void Start()
    {
        if (health.IsDead)
        {
            HandleDeath(null);
        }
    }

    private void OnDisable()
    {
        health.Died -= HandleDeath;
    }

    private void HandleDeath(GameObject damageSource)
    {
        if (deathHandled)
        {
            return;
        }

        deathHandled = true;

        // Release any carried object and restore its physics.
        if (grabber != null)
        {
            grabber.Drop();
        }

        // Disabling PlayerShooter also cancels unfinished reloading.
        foreach (Behaviour behaviour in playerBehaviours)
        {
            if (behaviour != null)
            {
                behaviour.enabled = false;
            }
        }

        playerInput.DeactivateInput();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log(
            "Player died. Movement, combat and interaction stopped.",
            this);
    }
}