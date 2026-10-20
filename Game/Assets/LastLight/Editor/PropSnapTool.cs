using UnityEditor;
using UnityEngine;

namespace LastLight.EditorTools
{
    /// <summary>
    /// Snaps selected lab props (shelf, cupboard, table) against the nearest wall.
    /// The prop's BACK is turned to the wall and its open FRONT faces into the room.
    /// First drag the prop roughly into the room you want, then run the menu item.
    /// </summary>
    public static class PropSnapTool
    {
        private const float WallGap = 0.02f;
        private static readonly Vector3[] Directions = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };

        [MenuItem("LastLight/Lab Props/Snap Selected To Nearest Wall (back to wall)")]
        public static void SnapSelectedToWall()
        {
            Transform[] selected = Selection.transforms;
            if (selected.Length == 0)
            {
                Debug.LogWarning("Select a shelf, cupboard or table in the Hierarchy first.");
                return;
            }

            foreach (Transform prop in selected)
            {
                Snap(prop);
            }
        }

        private static void Snap(Transform prop)
        {
            if (!TryGetBounds(prop, out Bounds bounds))
            {
                Debug.LogWarning(prop.name + " has no renderers to measure.");
                return;
            }

            float bestDistance = float.MaxValue;
            Vector3 bestDirection = Vector3.zero;
            foreach (Vector3 direction in Directions)
            {
                float distance = DistanceToWall(prop, bounds.center, direction);
                if (distance >= 0f && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestDirection = direction;
                }
            }

            if (bestDirection == Vector3.zero)
            {
                Debug.LogWarning(prop.name + ": no wall found within 8 m. Move it closer to a wall first.");
                return;
            }

            Undo.RecordObject(prop, "Snap prop to wall");

            // Back (-Z) toward the wall means the front (+Z) points away from it, into the room.
            prop.rotation = Quaternion.LookRotation(-bestDirection, Vector3.up);

            TryGetBounds(prop, out bounds);
            float distanceAfterTurn = DistanceToWall(prop, bounds.center, bestDirection);
            if (distanceAfterTurn < 0f) return;

            float extent = Mathf.Abs(bestDirection.x) > 0.5f ? bounds.extents.x : bounds.extents.z;
            prop.position += bestDirection * (distanceAfterTurn - extent - WallGap);
        }

        private static float DistanceToWall(Transform prop, Vector3 origin, Vector3 direction)
        {
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, 8f, ~0, QueryTriggerInteraction.Ignore);
            float best = -1f;
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.IsChildOf(prop)) continue;                              // ignore the prop itself
                if (hit.collider.GetComponentInParent<CharacterController>() != null) continue; // ignore the player
                if (best < 0f || hit.distance < best) best = hit.distance;
            }
            return best;
        }

        private static bool TryGetBounds(Transform prop, out Bounds bounds)
        {
            Renderer[] renderers = prop.GetComponentsInChildren<Renderer>();
            bounds = new Bounds(prop.position, Vector3.zero);
            if (renderers.Length == 0) return false;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return true;
        }
    }
}
