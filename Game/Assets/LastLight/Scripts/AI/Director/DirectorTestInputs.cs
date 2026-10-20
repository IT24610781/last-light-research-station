using UnityEngine;
using UnityEngine.InputSystem;

namespace LastLight.AI.Director
{
    /// <summary>
    /// TEST ONLY: fake player health and relay progress for the Director, until the
    /// real health and objective systems exist.
    ///   J = player takes 25% damage      H = heal to full
    ///   U = one more relay online         K = kill (disable) one active robot
    ///   O = reset the Director
    /// </summary>
    public class DirectorTestInputs : MonoBehaviour, IPlayerStatus, IMissionProgress
    {
        [SerializeField, Range(0f, 1f)] private float health = 1f;
        [SerializeField] private int relaysOnline = 0;
        [SerializeField] private int relaysTotal = 2;
        [SerializeField] private EncounterDirector director;

        private float lastDamageTime = -999f;

        public float Health01 => health;
        public float TimeSinceDamaged => Time.time - lastDamageTime;
        public int RelaysOnline => relaysOnline;
        public int RelaysTotal => relaysTotal;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.jKey.wasPressedThisFrame)
            {
                health = Mathf.Max(0f, health - 0.25f);
                lastDamageTime = Time.time;
                Debug.Log($"[DirectorTest] Player damaged: health {health:0.00}");
            }
            if (keyboard.hKey.wasPressedThisFrame)
            {
                health = 1f;
                Debug.Log("[DirectorTest] Player healed");
            }
            if (keyboard.uKey.wasPressedThisFrame)
            {
                relaysOnline = Mathf.Min(relaysTotal, relaysOnline + 1);
                Debug.Log($"[DirectorTest] Relays online: {relaysOnline}/{relaysTotal}");
            }
            if (keyboard.kKey.wasPressedThisFrame && DirectorRobotTag.Active.Count > 0)
            {
                DirectorRobotTag victim = DirectorRobotTag.Active[0];
                Debug.Log($"[DirectorTest] Killed {victim.name} ({victim.Type})");
                victim.gameObject.SetActive(false);
            }
            if (keyboard.oKey.wasPressedThisFrame && director != null)
            {
                director.ResetDirector();
                Debug.Log("[DirectorTest] Director reset");
            }
        }
    }
}
