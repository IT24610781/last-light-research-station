using UnityEngine;
using UnityEngine.InputSystem;

namespace LastLight.AI.Hunter
{
    /// <summary>
    /// TEST ONLY: creates noises at the mouse position through the real NoiseSystem,
    /// so the Hunter can be tested before player throwing exists.
    ///   N         = quiet noise (e.g. footstep)
    ///   Shift + N = loud noise (e.g. gunshot or thrown can)
    ///   R         = reset all Hunters in the scene
    /// </summary>
    public class HunterNoiseTester : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float quietStrength = 0.4f;
        [SerializeField] private float loudStrength = 1f;

        private Vector3 lastNoisePosition;
        private float lastNoiseTime = -999f;

        private void Start()
        {
            if (viewCamera == null) viewCamera = Camera.main;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null || viewCamera == null) return;

            if (keyboard.nKey.wasPressedThisFrame)
            {
                Ray ray = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
                if (Physics.Raycast(ray, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
                {
                    float strength = keyboard.shiftKey.isPressed ? loudStrength : quietStrength;
                    NoiseSystem.Emit(hit.point, strength, gameObject);
                    lastNoisePosition = hit.point;
                    lastNoiseTime = Time.time;
                    Debug.Log($"[NoiseTester] Noise {strength} at {hit.point}");
                }
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                foreach (HunterAgent hunter in FindObjectsByType<HunterAgent>(FindObjectsSortMode.None))
                    hunter.ResetAgent();
            }
        }

        private void OnDrawGizmos()
        {
            if (Time.time - lastNoiseTime > 1.5f) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(lastNoisePosition, 1f);
        }
    }
}
