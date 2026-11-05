using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LastLight.EditorTools.Hunter
{
    /// <summary>
    /// Builds in-place animation clips for the Onua model (Sound Hunter) from its bones,
    /// plus an Animator Controller with the same parameters HunterPresentation uses
    /// (Speed, Attack, Dead), so no gameplay code has to change.
    ///
    /// How it works: every pose is described as simple angles in the CHARACTER's frame
    /// (pitch = lean forward/back, yaw = turn, roll = tilt). For each frame, the builder
    /// walks the bone hierarchy from the top, applies those angles to each bone's
    /// inherited orientation, and stores the resulting local rotation as a keyframe.
    /// This makes the clips independent of how each bone's axes were set up in Blender.
    /// The arms are also lowered from the T-pose by aiming each upper arm downward.
    ///
    /// Use: select the Onua_Hunter model IN THE SCENE, then
    ///      Last Light > Hunter > Build Onua Animations on Selected Model.
    /// A report is printed to the Console.
    /// </summary>
    public static class OnuaHunterAnimationBuilder
    {
        private const string Folder = "Assets/LastLight/Animations/Hunter/Onua";
        private const float FrameRate = 30f;
        private const float WalkReferenceSpeed = 2.5f; // m/s where the blend tree is fully "walk"

        private const float IdleLength = 2.4f;
        private const float WalkCycle = 0.9f;
        private const float AttackLength = 0.6f;
        private const float DeathLength = 1.2f;

        private const string MenuNormal = "Last Light/Hunter/Build Onua Animations on Selected Model";
        private const string MenuFlipped = "Last Light/Hunter/Build Onua Animations (Front Flipped)";

        private static readonly string[] CentreParts = { "waist", "spine1", "spine2", "spine3", "neck", "head" };
        private static readonly string[] SidedParts = { "bicep", "forearm", "hand", "thigh", "shin", "ankle" };

        [MenuItem(MenuNormal)]
        private static void BuildNormal() => Build(false);

        [MenuItem(MenuFlipped)]
        private static void BuildFlipped() => Build(true);

        [MenuItem(MenuNormal, true)]
        private static bool CanBuildNormal() => CanBuild();

        [MenuItem(MenuFlipped, true)]
        private static bool CanBuildFlipped() => CanBuild();

        private static bool CanBuild()
        {
            GameObject go = Selection.activeGameObject;
            return go != null && !EditorUtility.IsPersistent(go) && !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        // ------------------------------------------------------------------ build

        private static void Build(bool flipFront)
        {
            Transform root = Selection.activeGameObject.transform;
            var rig = new Rig(root, flipFront);

            var report = new StringBuilder();
            rig.Describe(report);

            if (rig.Animated.Count == 0)
            {
                Debug.LogError("[Onua] No Onua bones found under the selected object. " +
                               "Select the Onua_Hunter model in the Hierarchy (the scene object, not the file).\n" + report);
                return;
            }

            EnsureFolder(Folder);
            AnimationClip idle = Bake(rig, "Onua_Idle", IdleLength, true, IdlePose);
            AnimationClip walk = Bake(rig, "Onua_Walk", WalkCycle, true, WalkPose);
            AnimationClip attack = Bake(rig, "Onua_Attack", AttackLength, false, AttackPose);
            AnimationClip death = Bake(rig, "Onua_Death", DeathLength, false, DeathPose);
            AnimatorController controller = BuildController(idle, walk, attack, death);

            Animator animator = root.GetComponent<Animator>();
            if (animator == null) animator = Undo.AddComponent<Animator>(root.gameObject);
            Undo.RecordObject(animator, "Assign Onua controller");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            EditorUtility.SetDirty(animator);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.AppendLine($"Created clips and controller in {Folder}. Animator on '{root.name}' now uses OnuaHunter.controller.");
            Debug.Log(report.ToString(), root);
        }

        // ------------------------------------------------------------------ poses
        // Angles in degrees. pitch > 0 = lean/swing BACKWARD for limbs hanging down,
        // forward/down for the torso and head. For legs and arms: negative pitch = forward.

        private static Pose IdlePose(float t)
        {
            float s = Mathf.Sin(2f * Mathf.PI * t / IdleLength);
            var p = new Pose();
            p.Set("spine2", 2f + 1.5f * s);
            p.Set("head", -1f * s, 18f * s);            // slow look left/right (scanning)
            p.Sides("bicep", -3f * s, -3f * s);
            p.Sides("forearm", -15f - 3f * s, -15f - 3f * s);
            p.Sides("shin", 4f, 4f);
            p.Sides("ankle", -4f, -4f);
            p.Bob = 0.012f * s;                          // breathing
            return p;
        }

        private static Pose WalkPose(float t)
        {
            float a = 2f * Mathf.PI * t / WalkCycle;
            float s = Mathf.Sin(a), c = Mathf.Cos(a);
            var p = new Pose();

            float thighL = -24f * s, thighR = 24f * s;
            float shinL = 6f + 30f * Mathf.Max(0f, c);   // knee bends while the leg swings forward
            float shinR = 6f + 30f * Mathf.Max(0f, -c);
            p.Sides("thigh", thighL, thighR);
            p.Sides("shin", shinL, shinR);
            p.Sides("ankle", -0.7f * (thighL + shinL), -0.7f * (thighR + shinR)); // keep feet flatter

            p.Sides("bicep", 16f * s, -16f * s);         // arms swing opposite to the legs
            p.Sides("forearm", -20f, -20f);
            p.Set("spine2", 6f);                         // lean into the walk
            p.Set("spine3", 0f, -6f * s);                // chest twist
            p.Set("head", -3f, 6f * s);                  // head stays looking ahead
            p.Bob = 0.03f * Mathf.Cos(2f * a) - 0.01f;   // up and down twice per cycle
            return p;
        }

        private static Pose AttackPose(float t)
        {
            // Raise both arms (and weapons) forward overhead, then slam down.
            float arm = Keys(t, 0f, 0f, 0.18f, -140f, 0.30f, -35f, 0.42f, -35f, 0.60f, 0f);
            float fore = Keys(t, 0f, -15f, 0.18f, -35f, 0.30f, -10f, 0.42f, -10f, 0.60f, -15f);
            float spine = Keys(t, 0f, 0f, 0.18f, -8f, 0.30f, 18f, 0.42f, 18f, 0.60f, 0f);
            float brace = Keys(t, 0f, 0f, 0.30f, 1f, 0.42f, 1f, 0.60f, 0f);

            var p = new Pose();
            p.Sides("bicep", arm, arm);
            p.Sides("forearm", fore, fore);
            p.Set("spine2", spine * 0.6f);
            p.Set("spine3", spine * 0.4f);
            p.Set("head", -spine * 0.5f);
            float thighL = -12f * brace, thighR = 8f * brace, shin = 15f * brace;
            p.Sides("thigh", thighL, thighR);
            p.Sides("shin", shin, shin);
            p.Sides("ankle", -0.7f * (thighL + shin), -0.7f * (thighR + shin));
            p.Bob = -0.06f * brace;
            return p;
        }

        private static Pose DeathPose(float t)
        {
            // Small recoil back, then topple forward onto the floor (pivot at the toes).
            var p = new Pose
            {
                Topple = Keys(t, 0f, 0f, 0.20f, -6f, 0.65f, 35f, 0.95f, 88f, 1.05f, 84f, 1.20f, 86f)
            };
            float spine = Keys(t, 0f, 0f, 0.35f, 25f, 0.95f, 8f, 1.20f, 8f);
            float arm = Keys(t, 0f, 0f, 0.40f, -60f, 0.95f, -10f, 1.20f, -10f);
            float legs = Keys(t, 0f, 0f, 0.50f, 1f, 1.20f, 1f);
            p.Set("spine2", spine);
            p.Set("head", spine * 0.5f);
            p.Sides("bicep", arm, arm);
            p.Sides("forearm", -25f, -25f);
            p.Sides("thigh", -10f * legs, -10f * legs);
            p.Sides("shin", 20f * legs, 20f * legs);
            return p;
        }

        /// <summary>Smooth keyframe interpolation: Keys(t, time0, value0, time1, value1, ...).</summary>
        private static float Keys(float t, params float[] kv)
        {
            if (t <= kv[0]) return kv[1];
            for (int i = 0; i + 3 < kv.Length; i += 2)
            {
                if (t <= kv[i + 2])
                {
                    float u = Mathf.InverseLerp(kv[i], kv[i + 2], t);
                    u = u * u * (3f - 2f * u);
                    return Mathf.Lerp(kv[i + 1], kv[i + 3], u);
                }
            }
            return kv[kv.Length - 1];
        }

        // ------------------------------------------------------------------ baking

        private static AnimationClip Bake(Rig rig, string name, float length, bool loop, Func<float, Pose> poseAt)
        {
            string path = $"{Folder}/{name}.anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = name };
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.ClearCurves();
            clip.frameRate = FrameRate;

            int frames = Mathf.Max(1, Mathf.RoundToInt(length * FrameRate));
            var rotCurves = new Dictionary<Transform, AnimationCurve[]>();
            foreach (Transform b in rig.Animated)
                rotCurves[b] = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
            var posCurves = new Dictionary<Transform, AnimationCurve[]>();
            foreach (Transform b in rig.Positioned)
                posCurves[b] = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };

            var previous = new Dictionary<Transform, Quaternion>();
            var local = new Dictionary<Transform, Quaternion>();
            var localPos = new Dictionary<Transform, Vector3>();

            for (int f = 0; f <= frames; f++)
            {
                float t = length * f / frames;
                rig.Evaluate(poseAt(t), local, localPos);

                foreach (Transform b in rig.Animated)
                {
                    Quaternion q = local[b];
                    if (previous.TryGetValue(b, out Quaternion pq) && Quaternion.Dot(pq, q) < 0f)
                        q = new Quaternion(-q.x, -q.y, -q.z, -q.w); // keep the shortest path between keys
                    previous[b] = q;

                    AnimationCurve[] c = rotCurves[b];
                    c[0].AddKey(t, q.x);
                    c[1].AddKey(t, q.y);
                    c[2].AddKey(t, q.z);
                    c[3].AddKey(t, q.w);
                }

                foreach (Transform b in rig.Positioned)
                {
                    Vector3 p = localPos[b];
                    AnimationCurve[] c = posCurves[b];
                    c[0].AddKey(t, p.x);
                    c[1].AddKey(t, p.y);
                    c[2].AddKey(t, p.z);
                }
            }

            foreach (KeyValuePair<Transform, AnimationCurve[]> kv in rotCurves)
            {
                string bonePath = AnimationUtility.CalculateTransformPath(kv.Key, rig.Root);
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.x", kv.Value[0]);
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.y", kv.Value[1]);
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.z", kv.Value[2]);
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.w", kv.Value[3]);
            }

            foreach (KeyValuePair<Transform, AnimationCurve[]> kv in posCurves)
            {
                string bonePath = AnimationUtility.CalculateTransformPath(kv.Key, rig.Root);
                clip.SetCurve(bonePath, typeof(Transform), "localPosition.x", kv.Value[0]);
                clip.SetCurve(bonePath, typeof(Transform), "localPosition.y", kv.Value[1]);
                clip.SetCurve(bonePath, typeof(Transform), "localPosition.z", kv.Value[2]);
            }

            clip.EnsureQuaternionContinuity();

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimatorController BuildController(AnimationClip idle, AnimationClip walk,
                                                          AnimationClip attack, AnimationClip death)
        {
            string path = Folder + "/OnuaHunter.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, WalkReferenceSpeed);
            sm.defaultState = locomotion;

            AnimatorState attackState = sm.AddState("Attack");
            attackState.motion = attack;
            AnimatorStateTransition toAttack = locomotion.AddTransition(attackState);
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            toAttack.hasExitTime = false;
            toAttack.duration = 0.08f;
            AnimatorStateTransition back = attackState.AddTransition(locomotion);
            back.hasExitTime = true;
            back.exitTime = 0.9f;
            back.duration = 0.1f;

            AnimatorState deathState = sm.AddState("Death");
            deathState.motion = death;
            AnimatorStateTransition toDeath = sm.AddAnyStateTransition(deathState);
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            toDeath.hasExitTime = false;
            toDeath.duration = 0.1f;
            toDeath.canTransitionToSelf = false;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        // ------------------------------------------------------------------ bone names

        private static readonly (string suffix, char side)[] SideSuffixes =
        {
            (".left", 'L'), ("_left", 'L'), (".right", 'R'), ("_right", 'R'),
            (".l", 'L'), ("_l", 'L'), ("-l", 'L'), (".r", 'R'), ("_r", 'R'), ("-r", 'R')
        };

        private static readonly string[] Tags = { "_fk", "_ik", "_def", "_deform", "_jnt", "_bone" };

        /// <summary>"OnuaRIG_bicep_FK.L" -> "bicep" with side 'L'.</summary>
        private static string BaseName(string raw, out char side)
        {
            string s = raw;
            int colon = s.LastIndexOf(':');
            if (colon >= 0) s = s.Substring(colon + 1);

            // Blender duplicate suffix ".001"
            if (s.Length > 4 && s[s.Length - 4] == '.' &&
                char.IsDigit(s[s.Length - 1]) && char.IsDigit(s[s.Length - 2]) && char.IsDigit(s[s.Length - 3]))
                s = s.Substring(0, s.Length - 4);

            if (s.StartsWith("OnuaRIG", StringComparison.OrdinalIgnoreCase)) s = s.Substring(7);
            s = s.Trim('_', '.', '-', ' ').ToLowerInvariant();

            foreach (string prefix in new[] { "def-", "org-", "mch-", "def_" })
                if (s.StartsWith(prefix)) s = s.Substring(prefix.Length);

            side = 'C';
            foreach ((string suffix, char sd) in SideSuffixes)
            {
                if (s.EndsWith(suffix))
                {
                    side = sd;
                    s = s.Substring(0, s.Length - suffix.Length);
                    break;
                }
            }

            bool stripped = true;
            while (stripped)
            {
                stripped = false;
                foreach (string tag in Tags)
                {
                    if (s.EndsWith(tag))
                    {
                        s = s.Substring(0, s.Length - tag.Length);
                        stripped = true;
                    }
                }
            }
            return s.Trim('_', '.', '-', ' ');
        }

        // ------------------------------------------------------------------ helpers

        private sealed class Pose
        {
            public readonly Dictionary<string, Vector3> Parts = new Dictionary<string, Vector3>(); // x pitch, y yaw, z roll
            public float Bob;     // metres up/down
            public float Topple;  // degrees, whole body falls forward around the toes

            public void Set(string key, float pitch, float yaw = 0f, float roll = 0f) =>
                Parts[key] = new Vector3(pitch, yaw, roll);

            public void Sides(string part, float pitchLeft, float pitchRight)
            {
                Set(part + ".L", pitchLeft);
                Set(part + ".R", pitchRight);
            }
        }

        private sealed class Rig
        {
            private const float HangOutward = 10f * Mathf.Deg2Rad; // arms hang slightly away from the body

            public readonly Transform Root;
            public readonly Vector3 Up, Forward, Right;
            public readonly Transform BobBone;
            public readonly List<Transform> Animated = new List<Transform>();
            public readonly List<Transform> Positioned = new List<Transform>();

            // Parts that Blender held in place with constraints (weapons, weapon treads):
            // in Unity they sit directly under the armature, so we make them follow a bone.
            private readonly Dictionary<Transform, Transform> follow = new Dictionary<Transform, Transform>();
            private readonly Dictionary<Transform, Quaternion> restWorldRot = new Dictionary<Transform, Quaternion>();
            private readonly Dictionary<Transform, Vector3> restWorldPos = new Dictionary<Transform, Vector3>();

            private readonly string frontAxis, frontSource;
            private readonly Dictionary<string, List<Transform>> parts = new Dictionary<string, List<Transform>>();
            private readonly Dictionary<Transform, string> partOf = new Dictionary<Transform, string>();
            private readonly Dictionary<Transform, Quaternion> restLocalRot = new Dictionary<Transform, Quaternion>();
            private readonly Dictionary<Transform, Vector3> aim = new Dictionary<Transform, Vector3>();
            private readonly Dictionary<Transform, float> side = new Dictionary<Transform, float>();
            private readonly Vector3 bobRestLocalPos, bobRestWorldPos, pivot;

            public Rig(Transform root, bool flipFront)
            {
                Root = root;

                var skinBones = new HashSet<Transform>();
                foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    foreach (Transform b in smr.bones)
                        if (b != null) skinBones.Add(b);

                var candidates = new List<(Transform t, string name, char side)>();
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    restLocalRot[t] = t.localRotation;
                    restWorldRot[t] = t.rotation;
                    restWorldPos[t] = t.position;
                    if (t == root || t.GetComponent<Renderer>() != null) continue;
                    bool isBone = skinBones.Contains(t) || t.name.StartsWith("OnuaRIG", StringComparison.OrdinalIgnoreCase);
                    if (!isBone) continue;
                    string baseName = BaseName(t.name, out char s);
                    candidates.Add((t, baseName, s));
                }

                // All bones with this name, keeping only the top-most ones (not inside another match).
                List<Transform> FindTop(string baseName, char s)
                {
                    var all = candidates.Where(c => c.name == baseName && c.side == s).Select(c => c.t).ToList();
                    return all.Where(t => !HasAncestorIn(t, all)).ToList();
                }

                foreach (string key in CentreParts) Register(key, FindTop(key, 'C'));
                foreach (string key in SidedParts)
                {
                    Register(key + ".L", FindTop(key, 'L'));
                    Register(key + ".R", FindTop(key, 'R'));
                }

                // Which way does the model face? Use the face bone in front of the head if present.
                Up = root.up;
                Vector3 front = root.forward;
                frontSource = "assumed (no usable face bone)";
                Transform head = parts["head"].FirstOrDefault();
                Transform face = FindTop("face", 'C').FirstOrDefault();
                if (head != null && face != null)
                {
                    Vector3 d = Vector3.ProjectOnPlane(face.position - head.position, Up);
                    if (d.magnitude > 0.01f)
                    {
                        front = d.normalized;
                        frontSource = "detected from the face bone";
                    }
                }
                front = SnapToRootAxis(front, root, out frontAxis);
                if (flipFront)
                {
                    front = -front;
                    frontAxis = FlipAxisName(frontAxis);
                    frontSource += ", then flipped";
                }
                Forward = front;
                Right = Vector3.Cross(Up, Forward).normalized;

                // Upper arms: remember the direction to the elbow so we can aim it downward.
                foreach (string sideKey in new[] { "L", "R" })
                {
                    foreach (Transform bicep in parts["bicep." + sideKey])
                    {
                        Transform elbow = parts["forearm." + sideKey].FirstOrDefault(f => f.IsChildOf(bicep));
                        if (elbow == null)
                            elbow = candidates.Select(c => c.t).FirstOrDefault(t => t.parent == bicep);
                        if (elbow == null) continue;

                        Vector3 worldDir = elbow.position - bicep.position;
                        if (worldDir.sqrMagnitude < 1e-8f) continue;
                        aim[bicep] = Quaternion.Inverse(bicep.rotation) * worldDir;

                        float sign = Mathf.Sign(Vector3.Dot(bicep.position - root.position, Right));
                        side[bicep] = sign != 0f ? sign : (sideKey == "R" ? 1f : -1f);
                    }
                }

                // Bone used for the body bob and the death fall: the top-most one above the parts.
                BobBone = PickBob(FindTop("root", 'C')) ?? PickBob(FindTop("character_root", 'C'))
                          ?? parts["waist"].FirstOrDefault() ?? parts["spine1"].FirstOrDefault();

                if (BobBone != null)
                {
                    bobRestLocalPos = BobBone.localPosition;
                    bobRestWorldPos = BobBone.position;
                }
                pivot = root.position + Forward * 0.25f; // toes

                Animated.AddRange(partOf.Keys);
                if (BobBone != null && !Animated.Contains(BobBone))
                {
                    Animated.Add(BobBone);
                    Positioned.Add(BobBone);
                }

                // Loose parts next to the body bones: sided ones follow that hand, others the nearest bone.
                if (BobBone != null && BobBone.parent != null && BobBone.parent != root)
                {
                    List<Transform> bodyBones = BobBone.GetComponentsInChildren<Transform>(true)
                        .Where(b => b.GetComponent<Renderer>() == null).ToList();
                    foreach (Transform loose in BobBone.parent)
                    {
                        if (loose == BobBone || loose.GetComponent<Renderer>() != null) continue;
                        BaseName(loose.name, out char s);
                        Transform target = null;
                        if (s == 'L' || s == 'R')
                        {
                            List<Transform> hands = parts["hand." + s];
                            target = hands.FirstOrDefault(h => h.name.IndexOf("_FK", StringComparison.OrdinalIgnoreCase) >= 0)
                                     ?? hands.FirstOrDefault();
                        }
                        if (target == null)
                            target = bodyBones.OrderBy(b => (b.position - loose.position).sqrMagnitude).FirstOrDefault();
                        if (target == null) continue;

                        follow[loose] = target;
                        Animated.Add(loose);
                        Positioned.Add(loose);
                    }
                }
            }

            private void Register(string key, List<Transform> bones)
            {
                parts[key] = bones;
                foreach (Transform b in bones)
                    if (!partOf.ContainsKey(b)) partOf[b] = key;
            }

            private Transform PickBob(List<Transform> list)
            {
                Transform best = null;
                int bestCount = 0;
                foreach (Transform t in list)
                {
                    int n = partOf.Keys.Count(p => p.IsChildOf(t));
                    if (n > bestCount) { best = t; bestCount = n; }
                }
                return best;
            }

            private static bool HasAncestorIn(Transform t, List<Transform> list)
            {
                for (Transform p = t.parent; p != null; p = p.parent)
                    if (list.Contains(p)) return true;
                return false;
            }

            /// <summary>Computes every animated bone's local rotation (and the bob bone's position) for a pose.</summary>
            public void Evaluate(Pose pose, Dictionary<Transform, Quaternion> localOut, Dictionary<Transform, Vector3> localPosOut)
            {
                Quaternion topple = Quaternion.AngleAxis(pose.Topple, Right);
                Vector3 up = topple * Up, fwd = topple * Forward, right = Right;
                var world = new Dictionary<Transform, Quaternion>();
                var worldPos = new Dictionary<Transform, Vector3>();

                Vector3 bobLocalPos = Vector3.zero;
                if (BobBone != null)
                {
                    Vector3 fall = pivot + topple * (bobRestWorldPos - pivot) - bobRestWorldPos;
                    Vector3 offset = Up * pose.Bob + fall;
                    bobLocalPos = bobRestLocalPos + BobBone.parent.InverseTransformVector(offset);
                }

                Quaternion WorldOf(Transform t)
                {
                    if (t == null || t == Root) return Root.rotation;
                    if (world.TryGetValue(t, out Quaternion cached)) return cached;

                    Quaternion inherited = WorldOf(t.parent) * restLocalRot[t];
                    Quaternion delta = Quaternion.identity;

                    if (partOf.TryGetValue(t, out string key))
                    {
                        pose.Parts.TryGetValue(key, out Vector3 a);
                        delta = Quaternion.AngleAxis(a.y, up) * Quaternion.AngleAxis(a.x, right) * Quaternion.AngleAxis(a.z, fwd);

                        if (aim.TryGetValue(t, out Vector3 aimLocal))
                        {
                            Vector3 hang = (-up * Mathf.Cos(HangOutward) + right * side[t] * Mathf.Sin(HangOutward)).normalized;
                            delta *= Quaternion.FromToRotation(inherited * aimLocal, hang);
                        }
                    }
                    if (t == BobBone) delta *= topple;

                    Quaternion result = delta * inherited;
                    world[t] = result;
                    return result;
                }

                // Posed world position, built from the posed rotations down the hierarchy.
                Vector3 PosOf(Transform t)
                {
                    if (t == null || t == Root) return Root.position;
                    if (worldPos.TryGetValue(t, out Vector3 cached)) return cached;
                    Vector3 local = t == BobBone ? bobLocalPos : t.localPosition;
                    Vector3 result = PosOf(t.parent) + WorldOf(t.parent) * Vector3.Scale(t.parent.lossyScale, local);
                    worldPos[t] = result;
                    return result;
                }

                foreach (Transform b in Animated)
                {
                    if (follow.ContainsKey(b)) continue;
                    localOut[b] = Quaternion.Inverse(WorldOf(b.parent)) * WorldOf(b);
                }
                if (BobBone != null) localPosOut[BobBone] = bobLocalPos;

                // Followers keep their rest offset from the bone they follow.
                foreach (KeyValuePair<Transform, Transform> kv in follow)
                {
                    Transform f = kv.Key, target = kv.Value;
                    Quaternion delta = WorldOf(target) * Quaternion.Inverse(restWorldRot[target]);
                    Quaternion rot = delta * restWorldRot[f];
                    Vector3 pos = PosOf(target) + delta * (restWorldPos[f] - restWorldPos[target]);
                    localOut[f] = Quaternion.Inverse(f.parent.rotation) * rot;
                    localPosOut[f] = f.parent.InverseTransformPoint(pos);
                }
            }

            public void Describe(StringBuilder sb)
            {
                sb.AppendLine("[Onua] Animation build report");
                sb.AppendLine($"Front of model: {frontAxis} ({frontSource}). {RotationAdvice(frontAxis)}");
                foreach (KeyValuePair<string, List<Transform>> kv in parts)
                {
                    string names = kv.Value.Count == 0 ? "MISSING" : string.Join(", ", kv.Value.Select(t => t.name));
                    sb.AppendLine($"  {kv.Key,-10} {names}");
                }
                sb.AppendLine($"  bob/fall   {(BobBone != null ? BobBone.name : "MISSING")}");
                foreach (KeyValuePair<Transform, Transform> kv in follow)
                    sb.AppendLine($"  follower   {kv.Key.name} -> {kv.Value.name}");
                sb.AppendLine($"  arms aimed: {aim.Count}. Bones animated: {Animated.Count}.");
            }

            private static string RotationAdvice(string axis)
            {
                switch (axis)
                {
                    case "+Z": return "Already faces Unity forward, no turn needed.";
                    case "-Z": return "Set Rotation Y = 180 on Onua_Hunter when it is inside the Hunter.";
                    case "+X": return "Set Rotation Y = -90 on Onua_Hunter when it is inside the Hunter.";
                    default: return "Set Rotation Y = 90 on Onua_Hunter when it is inside the Hunter.";
                }
            }
        }

        private static Vector3 SnapToRootAxis(Vector3 dir, Transform root, out string axisName)
        {
            (Vector3 v, string n)[] axes =
            {
                (root.forward, "+Z"), (-root.forward, "-Z"), (root.right, "+X"), (-root.right, "-X")
            };
            int best = 0;
            for (int i = 1; i < axes.Length; i++)
                if (Vector3.Dot(dir, axes[i].v) > Vector3.Dot(dir, axes[best].v)) best = i;
            axisName = axes[best].n;
            return axes[best].v;
        }

        private static string FlipAxisName(string axis) =>
            axis == "+Z" ? "-Z" : axis == "-Z" ? "+Z" : axis == "+X" ? "-X" : "+X";
    }
}
