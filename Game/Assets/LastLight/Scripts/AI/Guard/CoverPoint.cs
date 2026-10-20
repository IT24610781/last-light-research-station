using System.Collections.Generic;
using UnityEngine;

namespace LastLight.AI.Guard
{
    /// <summary>
    /// A marked position a Guard can take cover at. Only one owner at a time: a Guard must
    /// reserve a cover point before using it and release it when it leaves or dies.
    /// Place these as empty objects directly behind walls, crates and pillars.
    /// </summary>
    public class CoverPoint : MonoBehaviour
    {
        private static readonly List<CoverPoint> all = new List<CoverPoint>();

        /// <summary>Every active cover point in the scene.</summary>
        public static IReadOnlyList<CoverPoint> All { get { return all; } }

        public GameObject Owner { get; private set; }

        public Vector3 Position { get { return transform.position; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            all.Clear();
        }

        private void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
            Owner = null;
        }

        /// <summary>True if nobody has reserved this point, or the asker already owns it.</summary>
        public bool IsAvailableTo(GameObject who)
        {
            return Owner == null || Owner == who;
        }

        public bool TryReserve(GameObject who)
        {
            if (!IsAvailableTo(who)) return false;
            Owner = who;
            return true;
        }

        public void Release(GameObject who)
        {
            if (Owner == who) Owner = null;
        }

        /// <summary>Clears every reservation. Call on level restart.</summary>
        public static void ReleaseAll()
        {
            foreach (CoverPoint point in all) point.Owner = null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Owner == null ? new Color(0.2f, 0.8f, 1f) : Color.yellow;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(0.6f, 1f, 0.6f));
        }
    }
}
