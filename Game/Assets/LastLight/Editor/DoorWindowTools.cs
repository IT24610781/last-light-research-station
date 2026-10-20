using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastLight.EditorTools
{
    /// <summary>
    /// Changes the window on the plain square doors made by SquareDoorTools.
    /// Both options are solid and opaque: nothing is see-through, and the door's solid collider is untouched.
    ///  - Solid + Vent Slats: no window at all, horizontal slats instead.
    ///  - Round Porthole: a round, opaque dark disc in a metal ring (a different shape from the square window).
    /// Run SquareDoorTools first. Safe to run either option repeatedly or switch between them.
    /// </summary>
    public static class DoorWindowTools
    {
        private const string MaterialFolder = "Assets/LastLight/Materials/LabProps";
        private const string SquareDetail = "SquareDoorDetail";

        [MenuItem("LastLight/Doors/Window Style - Solid With Vent Slats")]
        public static void SolidWithVentSlats()
        {
            Apply(false);
        }

        [MenuItem("LastLight/Doors/Window Style - Round Porthole (opaque)")]
        public static void RoundPorthole()
        {
            Apply(true);
        }

        private static void Apply(bool porthole)
        {
            StationSlidingDoor[] doors = Object.FindObjectsByType<StationSlidingDoor>(FindObjectsSortMode.None);
            if (doors.Length == 0)
            {
                Debug.LogWarning("No StationSlidingDoor found. Stop Play mode and open the ResearchStation scene first.");
                return;
            }

            Material metal = GetMaterial("Door_SquareMetal", new Color(0.35f, 0.36f, 0.37f), 0.5f, 0.8f);
            Material dark = GetMaterial("Door_PortholeDark", new Color(0.03f, 0.04f, 0.04f), 0.4f, 0.2f);

            int changed = 0;
            foreach (StationSlidingDoor door in doors)
            {
                foreach (Transform leaf in door.transform)
                {
                    Transform group = leaf.Find(SquareDetail);
                    if (group == null) continue; // run SquareDoorTools first

                    // Remove any earlier window-style parts.
                    List<GameObject> old = new List<GameObject>();
                    foreach (Transform part in group)
                    {
                        string n = part.name;
                        if (n == "Window" || n == "WindowFrame" || n == "VentSlat" || n == "Porthole" || n == "PortholeRing")
                        {
                            old.Add(part.gameObject);
                        }
                    }
                    foreach (GameObject go in old) Undo.DestroyObjectImmediate(go);

                    foreach (float side in new[] { -1f, 1f })
                    {
                        if (porthole)
                        {
                            // Cylinder's axis is Y, so turn it 90 degrees to face along X.
                            Part(group, PrimitiveType.Cylinder, "PortholeRing", new Vector3(side * 0.093f, 1.75f, 0f),
                                new Vector3(0.46f, 0.006f, 0.46f), new Vector3(0f, 0f, 90f), metal);
                            Part(group, PrimitiveType.Cylinder, "Porthole", new Vector3(side * 0.099f, 1.75f, 0f),
                                new Vector3(0.36f, 0.006f, 0.36f), new Vector3(0f, 0f, 90f), dark);
                        }
                        else
                        {
                            for (int i = 0; i < 5; i++)
                            {
                                Part(group, PrimitiveType.Cube, "VentSlat", new Vector3(side * 0.095f, 1.55f + i * 0.08f, 0f),
                                    new Vector3(0.012f, 0.03f, 0.5f), Vector3.zero, metal);
                            }
                        }
                    }
                    changed++;
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log((porthole ? "Round porthole" : "Solid vent-slat") + " style applied to " + changed + " door leaves.");
        }

        private static void Part(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 localScale, Vector3 euler, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.transform.localEulerAngles = euler;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // visual only
            Undo.RegisterCreatedObjectUndo(go, "Door window style");
        }

        private static Material GetMaterial(string name, Color color, float smoothness, float metallic)
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
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
