using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastLight.EditorTools
{
    /// <summary>
    /// Replaces the sci-fi kit door models with plain, flat, square doors (school / hospital style).
    /// The StationSlidingDoor script, the solid panel collider and the E interaction are kept,
    /// so the doors still slide open and closed exactly as before.
    /// The kit door models are only hidden (not deleted), so Ctrl+Z or a scene revert brings them back.
    /// </summary>
    public static class SquareDoorTools
    {
        private const string MaterialFolder = "Assets/LastLight/Materials/LabProps";
        private const string SquareDetail = "SquareDoorDetail";
        private const string OldRustDetail = "HorrorDoorDetail";

        [MenuItem("LastLight/Doors/Replace With Plain Square Doors")]
        public static void ReplaceWithSquareDoors()
        {
            StationSlidingDoor[] doors = Object.FindObjectsByType<StationSlidingDoor>(FindObjectsSortMode.None);
            if (doors.Length == 0)
            {
                Debug.LogWarning("No StationSlidingDoor found. Stop Play mode and open the ResearchStation scene first.");
                return;
            }

            Material paint = GetMaterial("Door_SquarePaint", new Color(0.17f, 0.2f, 0.18f), 0.15f, 0.1f, null);
            Material metal = GetMaterial("Door_SquareMetal", new Color(0.35f, 0.36f, 0.37f), 0.5f, 0.8f, null);
            Material glass = GetMaterial("Door_SquareGlass", new Color(0.02f, 0.03f, 0.03f), 0.9f, 0f, null);

            int leavesDone = 0;

            foreach (StationSlidingDoor door in doors)
            {
                foreach (Transform leaf in door.transform)
                {
                    if (!leaf.name.ToLower().Contains("leaf")) continue;
                    if (leaf.Find(SquareDetail) != null) continue; // already replaced

                    // Remove the earlier rust decoration, if it was applied.
                    Transform rust = leaf.Find(OldRustDetail);
                    if (rust != null) Undo.DestroyObjectImmediate(rust.gameObject);

                    // Hide the sci-fi kit door model and the small status inset.
                    List<GameObject> toHide = new List<GameObject>();
                    foreach (Transform child in leaf)
                    {
                        if (child.name == "Solid sliding panel" || child.name == SquareDetail) continue;
                        if (child.name == "Door status inset" || PrefabUtility.IsPartOfPrefabInstance(child.gameObject))
                        {
                            toHide.Add(child.gameObject);
                        }
                    }
                    foreach (GameObject go in toHide)
                    {
                        Undo.RecordObject(go, "Hide old door part");
                        go.SetActive(false);
                    }

                    // Plain painted slab.
                    Transform panel = leaf.Find("Solid sliding panel");
                    if (panel != null)
                    {
                        Renderer panelRenderer = panel.GetComponent<Renderer>();
                        if (panelRenderer != null)
                        {
                            Undo.RecordObject(panelRenderer, "Square door paint");
                            panelRenderer.sharedMaterial = paint;
                        }
                    }

                    // Simple square details on both faces: window, push bar, kick plate.
                    GameObject group = new GameObject(SquareDetail);
                    group.transform.SetParent(leaf, false);
                    Undo.RegisterCreatedObjectUndo(group, "Square door detail");

                    foreach (float side in new[] { -1f, 1f })
                    {
                        Detail(group.transform, "Window", new Vector3(side * 0.095f, 1.75f, 0f), new Vector3(0.012f, 0.4f, 0.4f), glass);
                        Detail(group.transform, "WindowFrame", new Vector3(side * 0.093f, 1.75f, 0f), new Vector3(0.01f, 0.46f, 0.46f), metal);
                        Detail(group.transform, "PushBar", new Vector3(side * 0.115f, 1.05f, 0f), new Vector3(0.03f, 0.04f, 0.7f), metal);
                        Detail(group.transform, "KickPlate", new Vector3(side * 0.095f, 0.2f, 0f), new Vector3(0.012f, 0.35f, 1.0f), metal);
                    }
                    leavesDone++;
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("Square doors: " + leavesDone + " leaves replaced on " + doors.Length + " doors.");
        }

        private static void Detail(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // visual only
        }

        private static Material GetMaterial(string name, Color color, float smoothness, float metallic, Color? emission)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/LastLight/Materials", "LabProps");
            }

            string path = MaterialFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material m = new Material(shader);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
