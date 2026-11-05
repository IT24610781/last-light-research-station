using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LastLight.Atmosphere;

namespace LastLight.EditorTools
{
    /// <summary>
    /// Editor menu tools (LastLight menu) that dress the station as an abandoned horror lab.
    /// Props are built from primitives with simple materials, so they are cheap to render.
    /// Small items have their colliders removed on purpose (physics optimisation).
    /// </summary>
    public static class AbandonedLabTools
    {
        private const string MaterialFolder = "Assets/LastLight/Materials/LabProps";
        private const string MarkerName = "HorrorLook_Applied";

        // ------------------------------------------------------------------
        // Atmosphere
        // ------------------------------------------------------------------

        [MenuItem("LastLight/Atmosphere/Apply Abandoned Horror Look")]
        public static void ApplyHorrorLook()
        {
            if (GameObject.Find(MarkerName) != null)
            {
                Debug.LogWarning("Horror look is already applied to this scene. Delete the '" +
                                 MarkerName + "' object first if you want to apply it again.");
                return;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.04f, 0.07f, 0.06f);
            RenderSettings.fogDensity = 0.035f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.035f, 0.05f, 0.06f);

            Light[] all = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);

            foreach (Light sun in all.Where(l => l.type == LightType.Directional))
            {
                Undo.RecordObject(sun, "Dim directional light");
                sun.intensity *= 0.15f;
            }

            Light[] lamps = all
                .Where(l => l.type == LightType.Point || l.type == LightType.Spot)
                .OrderBy(l => l.transform.position.x)
                .ThenBy(l => l.transform.position.z)
                .ToArray();

            Color sickly = new Color(0.62f, 0.85f, 0.7f);
            Color emergencyRed = new Color(0.9f, 0.12f, 0.08f);
            int flickering = 0;
            int red = 0;

            for (int i = 0; i < lamps.Length; i++)
            {
                Light lamp = lamps[i];
                Undo.RecordObject(lamp, "Horror light");
                lamp.color = Color.Lerp(lamp.color, sickly, 0.55f);
                lamp.intensity *= 0.6f;

                if (i % 9 == 4)
                {
                    lamp.color = emergencyRed;
                    red++;
                }

                if (i % 4 == 1 && lamp.GetComponent<FlickerLight>() == null)
                {
                    lamp.lightmapBakeType = LightmapBakeType.Realtime; // flicker needs realtime
                    Undo.AddComponent<FlickerLight>(lamp.gameObject);
                    flickering++;
                }
            }

            GameObject marker = new GameObject(MarkerName);
            Undo.RegisterCreatedObjectUndo(marker, "Horror look marker");
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("Horror look applied: " + lamps.Length + " lamps tinted, " +
                      flickering + " flickering, " + red + " emergency red. Fog and ambient set.");
        }

        // ------------------------------------------------------------------
        // Props
        // ------------------------------------------------------------------

        [MenuItem("LastLight/Lab Props/Shelf With Files and Equipment")]
        public static void CreateShelf()
        {
            System.Random rnd = new System.Random(System.Environment.TickCount);
            Transform root = NewRoot("LabShelf");
            Material metal = GetMaterial("Lab_ShelfMetal", new Color(0.12f, 0.14f, 0.15f), 0.35f, 0.6f);

            const float w = 1.8f, h = 2.0f, d = 0.45f;
            Part(root, PrimitiveType.Cube, "SideL", new Vector3(-w / 2f, h / 2f, 0f), new Vector3(0.05f, h, d), metal, true);
            Part(root, PrimitiveType.Cube, "SideR", new Vector3(w / 2f, h / 2f, 0f), new Vector3(0.05f, h, d), metal, true);
            Part(root, PrimitiveType.Cube, "Back", new Vector3(0f, h / 2f, -d / 2f + 0.01f), new Vector3(w, h, 0.02f), metal, true);

            float[] levels = { 0.1f, 0.55f, 1.0f, 1.45f, 1.95f };
            foreach (float y in levels)
            {
                Part(root, PrimitiveType.Cube, "Board", new Vector3(0f, y, 0f), new Vector3(w, 0.04f, d), metal, true);
            }
            for (int i = 0; i < levels.Length - 1; i++)
            {
                FillLevel(root, rnd, levels[i] + 0.02f, w - 0.14f, d - 0.1f);
            }

            Finish(root);
        }

        [MenuItem("LastLight/Lab Props/Cupboard (one door ajar)")]
        public static void CreateCupboard()
        {
            System.Random rnd = new System.Random(System.Environment.TickCount);
            Transform root = NewRoot("LabCupboard");
            Material body = GetMaterial("Lab_CupboardGreen", new Color(0.16f, 0.24f, 0.2f), 0.25f, 0.1f);

            const float w = 0.95f, h = 1.9f, d = 0.5f;
            Part(root, PrimitiveType.Cube, "SideL", new Vector3(-w / 2f, h / 2f, 0f), new Vector3(0.03f, h, d), body, true);
            Part(root, PrimitiveType.Cube, "SideR", new Vector3(w / 2f, h / 2f, 0f), new Vector3(0.03f, h, d), body, true);
            Part(root, PrimitiveType.Cube, "Top", new Vector3(0f, h, 0f), new Vector3(w, 0.03f, d), body, true);
            Part(root, PrimitiveType.Cube, "Back", new Vector3(0f, h / 2f, -d / 2f + 0.01f), new Vector3(w, h, 0.02f), body, true);

            float[] levels = { 0.05f, 0.5f, 0.95f, 1.4f };
            foreach (float y in levels)
            {
                Part(root, PrimitiveType.Cube, "Board", new Vector3(0f, y, 0f), new Vector3(w - 0.06f, 0.03f, d - 0.04f), body, true);
            }
            for (int i = 0; i < 3; i++)
            {
                FillLevel(root, rnd, levels[i] + 0.015f, w - 0.14f, d - 0.12f);
            }

            float leafW = w / 2f - 0.01f;
            float leafH = h - 0.08f;
            MakeDoor(root, "DoorL", new Vector3(-w / 2f, h / 2f, d / 2f), leafW, leafH, body, +1f, 0f);
            MakeDoor(root, "DoorR", new Vector3(w / 2f, h / 2f, d / 2f), leafW, leafH, body, -1f, 35f);

            Finish(root);
        }

        [MenuItem("LastLight/Lab Props/Lab Table With Equipment")]
        public static void CreateLabTable()
        {
            System.Random rnd = new System.Random(System.Environment.TickCount);
            Transform root = NewRoot("LabTable");
            Material wood = GetMaterial("Lab_DarkWood", new Color(0.2f, 0.14f, 0.1f), 0.15f, 0f);
            Material steel = GetMaterial("Lab_ShelfMetal", new Color(0.12f, 0.14f, 0.15f), 0.35f, 0.6f);

            Part(root, PrimitiveType.Cube, "Top", new Vector3(0f, 0.9f, 0f), new Vector3(2f, 0.06f, 0.9f), wood, true);
            foreach (float sx in new[] { -0.93f, 0.93f })
            {
                foreach (float sz in new[] { -0.38f, 0.38f })
                {
                    Part(root, PrimitiveType.Cube, "Leg", new Vector3(sx, 0.45f, sz), new Vector3(0.06f, 0.9f, 0.06f), steel, true);
                }
            }

            float top = 0.93f;
            for (int i = 0; i < 4; i++) Beaker(root, rnd, new Vector3(R(rnd, -0.8f, 0.8f), top, R(rnd, -0.3f, 0.3f)));
            for (int i = 0; i < 2; i++) Flask(root, rnd, new Vector3(R(rnd, -0.8f, 0.8f), top, R(rnd, -0.3f, 0.3f)));
            Jar(root, new Vector3(0.5f, top, 0.1f));

            // Simple microscope: base, arm, tube.
            Part(root, PrimitiveType.Cube, "ScopeBase", new Vector3(-0.6f, top + 0.015f, 0f), new Vector3(0.14f, 0.03f, 0.2f), steel, false);
            Part(root, PrimitiveType.Cylinder, "ScopeArm", new Vector3(-0.6f, top + 0.12f, -0.05f), new Vector3(0.03f, 0.1f, 0.03f), steel, false, new Vector3(-15f, 0f, 0f));
            Part(root, PrimitiveType.Cylinder, "ScopeTube", new Vector3(-0.6f, top + 0.24f, 0.0f), new Vector3(0.04f, 0.07f, 0.04f), steel, false, new Vector3(25f, 0f, 0f));

            // Knocked-over beaker on the edge.
            Material glass = GlassMaterial();
            Part(root, PrimitiveType.Cylinder, "FallenBeaker", new Vector3(0.1f, top + 0.05f, 0.4f), new Vector3(0.1f, 0.07f, 0.1f), glass, false, new Vector3(0f, 0f, 90f));

            for (int i = 0; i < 6; i++)
            {
                PaperSheet(root, rnd, new Vector3(R(rnd, -0.9f, 0.9f), top + 0.002f + i * 0.0006f, R(rnd, -0.35f, 0.35f)));
            }

            Finish(root);
        }

        [MenuItem("LastLight/Lab Props/Scattered Papers (floor)")]
        public static void CreateScatteredPapers()
        {
            System.Random rnd = new System.Random(System.Environment.TickCount);
            Transform root = NewRoot("ScatteredPapers");
            for (int i = 0; i < 28; i++)
            {
                float angle = R(rnd, 0f, 360f) * Mathf.Deg2Rad;
                float radius = Mathf.Sqrt((float)rnd.NextDouble()) * 1.4f;
                Vector3 p = new Vector3(Mathf.Cos(angle) * radius, 0.004f + i * 0.0006f, Mathf.Sin(angle) * radius);
                PaperSheet(root, rnd, p);
            }
            Finish(root);
        }

        // ------------------------------------------------------------------
        // Building blocks
        // ------------------------------------------------------------------

        private static void FillLevel(Transform root, System.Random rnd, float topY, float usableWidth, float depth)
        {
            float x = -usableWidth / 2f;
            while (x < usableWidth / 2f - 0.1f)
            {
                double roll = rnd.NextDouble();
                float z = R(rnd, -0.04f, 0.04f);

                if (roll < 0.12)
                {
                    x += R(rnd, 0.1f, 0.3f); // gap, looks abandoned
                }
                else if (roll < 0.62)
                {
                    float t = R(rnd, 0.03f, 0.07f);
                    float ht = R(rnd, 0.26f, 0.36f);
                    float lean = rnd.NextDouble() < 0.2 ? R(rnd, -18f, 18f) : 0f;
                    Part(root, PrimitiveType.Cube, "File", new Vector3(x + t / 2f, topY + ht / 2f, z),
                        new Vector3(t, ht, depth * 0.7f), FileMaterial(rnd), false, new Vector3(0f, 0f, lean));
                    x += t + (Mathf.Abs(lean) > 0.01f ? 0.05f : 0.002f);
                }
                else if (roll < 0.8)
                {
                    Beaker(root, rnd, new Vector3(x + 0.05f, topY, z));
                    x += 0.13f;
                }
                else if (roll < 0.92)
                {
                    Jar(root, new Vector3(x + 0.07f, topY, z));
                    x += 0.18f;
                }
                else
                {
                    Flask(root, rnd, new Vector3(x + 0.07f, topY, z));
                    x += 0.17f;
                }
            }
        }

        private static void Beaker(Transform root, System.Random rnd, Vector3 basePos)
        {
            Part(root, PrimitiveType.Cylinder, "Beaker", basePos + new Vector3(0f, 0.07f, 0f),
                new Vector3(0.1f, 0.07f, 0.1f), GlassMaterial(), false);
        }

        private static void Jar(Transform root, Vector3 basePos)
        {
            Material liquid = GetMaterial("Lab_SpecimenGreen", new Color(0.1f, 0.35f, 0.15f), 0.8f, 0f, new Color(0.05f, 0.45f, 0.12f));
            Part(root, PrimitiveType.Cylinder, "SpecimenJar", basePos + new Vector3(0f, 0.1f, 0f),
                new Vector3(0.14f, 0.1f, 0.14f), liquid, false);
        }

        private static void Flask(Transform root, System.Random rnd, Vector3 basePos)
        {
            Material glass = GlassMaterial();
            Part(root, PrimitiveType.Sphere, "Flask", basePos + new Vector3(0f, 0.07f, 0f), Vector3.one * 0.14f, glass, false);
            Part(root, PrimitiveType.Cylinder, "FlaskNeck", basePos + new Vector3(0f, 0.17f, 0f), new Vector3(0.04f, 0.04f, 0.04f), glass, false);
        }

        private static void PaperSheet(Transform root, System.Random rnd, Vector3 localPos)
        {
            Material paper = GetMaterial("Lab_Paper", new Color(0.72f, 0.7f, 0.62f), 0.05f, 0f);
            Part(root, PrimitiveType.Cube, "Paper", localPos, new Vector3(0.21f, 0.004f, 0.297f), paper, false,
                new Vector3(R(rnd, -4f, 4f), R(rnd, 0f, 360f), R(rnd, -4f, 4f)));
        }

        private static void MakeDoor(Transform root, string name, Vector3 hinge, float leafW, float leafH, Material mat, float side, float yaw)
        {
            GameObject pivot = new GameObject(name);
            pivot.transform.SetParent(root, false);
            pivot.transform.localPosition = hinge;
            pivot.transform.localEulerAngles = new Vector3(0f, yaw * -side, 0f);
            Part(pivot.transform, PrimitiveType.Cube, "Leaf", new Vector3(side * leafW / 2f, 0f, 0.015f), new Vector3(leafW, leafH, 0.03f), mat, true);
        }

        private static Material FileMaterial(System.Random rnd)
        {
            switch (rnd.Next(5))
            {
                case 0: return GetMaterial("Lab_FileManila", new Color(0.55f, 0.47f, 0.3f), 0.1f, 0f);
                case 1: return GetMaterial("Lab_FileRed", new Color(0.35f, 0.1f, 0.1f), 0.1f, 0f);
                case 2: return GetMaterial("Lab_FileBlue", new Color(0.12f, 0.18f, 0.3f), 0.1f, 0f);
                case 3: return GetMaterial("Lab_FileGrey", new Color(0.7f, 0.7f, 0.65f), 0.1f, 0f);
                default: return GetMaterial("Lab_FileBlack", new Color(0.05f, 0.05f, 0.05f), 0.2f, 0f);
            }
        }

        private static Material GlassMaterial()
        {
            return GetMaterial("Lab_Glass", new Color(0.6f, 0.8f, 0.8f), 0.9f, 0f);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static GameObject Part(Transform parent, PrimitiveType type, string name, Vector3 localPos,
            Vector3 localScale, Material mat, bool keepCollider, Vector3? euler = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            if (euler.HasValue) go.transform.localEulerAngles = euler.Value;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            return go;
        }

        private static Transform NewRoot(string name)
        {
            GameObject root = new GameObject(name);
            root.transform.position = SpawnPoint();
            return root.transform;
        }

        private static Vector3 SpawnPoint()
        {
            SceneView view = SceneView.lastActiveSceneView;
            Vector3 p = view != null ? view.pivot : Vector3.zero;
            if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 6f))
            {
                p = hit.point;
            }
            return p;
        }

        private static void Finish(Transform root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.isStatic = true;
            }
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Create lab prop");
            Selection.activeGameObject = root.gameObject;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static float R(System.Random rnd, float min, float max)
        {
            return min + (float)rnd.NextDouble() * (max - min);
        }

        private static Material GetMaterial(string name, Color color, float smoothness, float metallic, Color? emission = null)
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
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
