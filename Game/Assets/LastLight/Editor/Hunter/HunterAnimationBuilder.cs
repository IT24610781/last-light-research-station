using LastLight.AI.Hunter;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Editor tool (Member B): builds the Hunter's animation assets and a rigid-part
/// stand-in robot. Clips animate the parts "Body", "Body/Head" and "Body/GunArm"
/// relative to the object holding the Animator, so the same controller works on
/// the final model if its parts use the same names.
/// Menu: Last Light > Hunter > Build Animated Stand-in on Selected Robots
/// </summary>
public static class HunterAnimationBuilder
{
    private const string AnimFolder = "Assets/LastLight/Animations/Hunter";
    private const string MatFolder = "Assets/LastLight/Materials/AI";
    private const string ControllerPath = AnimFolder + "/HunterRobot.controller";

    // Rest pose (local positions inside the "Visual" object).
    private static readonly Vector3 BodyRest = new Vector3(0f, 0.9f, 0f);
    private static readonly Vector3 HeadRest = new Vector3(0f, 0.6f, 0f);
    private static readonly Vector3 ArmRest = new Vector3(0.45f, 0.15f, 0.15f);

    private const string Body = "Body";
    private const string Head = "Body/Head";
    private const string Arm = "Body/GunArm";

    [MenuItem("Last Light/Hunter/Build Animated Stand-in on Selected Robots")]
    public static void BuildOnSelection()
    {
        AnimatorController controller = BuildAnimationAssets();
        Material bodyMat = GetMaterial("Hunter_Body", new Color(0.18f, 0.2f, 0.23f), Color.black);
        Material accentMat = GetMaterial("Hunter_Accent", new Color(0.85f, 0.55f, 0.1f), Color.black);
        Material visorMat = GetMaterial("Hunter_Visor", new Color(1f, 0.1f, 0.05f), new Color(3f, 0.2f, 0.1f));

        int built = 0;
        foreach (GameObject robot in Selection.gameObjects)
        {
            if (robot.GetComponent<HunterAgent>() == null) continue;
            if (robot.transform.Find("Visual") != null)
            {
                Debug.Log($"[HunterAnimationBuilder] {robot.name} already has a Visual; only re-linking the controller.");
                LinkAnimator(robot, robot.transform.Find("Visual").gameObject, controller);
                continue;
            }

            GameObject visual = BuildStandIn(robot.transform, bodyMat, accentMat, visorMat);
            LinkAnimator(robot, visual, controller);

            // Hide the old capsule mesh; the CharacterController still does collisions.
            var capsuleRenderer = robot.GetComponent<MeshRenderer>();
            if (capsuleRenderer != null)
            {
                Undo.RecordObject(capsuleRenderer, "Hide capsule");
                capsuleRenderer.enabled = false;
            }
            built++;
        }

        Debug.Log($"[HunterAnimationBuilder] Animation assets ready at {AnimFolder}; stand-in built on {built} robot(s). " +
                  "Select objects with a Hunter Agent before running to build on them.");
    }

    [MenuItem("Last Light/Hunter/Rebuild Animation Assets Only")]
    public static void RebuildAssetsOnly()
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        BuildAnimationAssets();
        Debug.Log("[HunterAnimationBuilder] Animation assets rebuilt.");
    }

    // ---------------- Animation assets ----------------

    private static AnimatorController BuildAnimationAssets()
    {
        EnsureFolder(AnimFolder);
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (existing != null) return existing;

        AnimationClip idle = SaveClip(MakeIdle(), "Hunter_Idle", loop: true);
        AnimationClip move = SaveClip(MakeMove(), "Hunter_Move", loop: true);
        AnimationClip attack = SaveClip(MakeAttack(), "Hunter_Attack", loop: false);
        AnimationClip death = SaveClip(MakeDeath(), "Hunter_Death", loop: false);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        // Locomotion blend: Idle at 0 m/s, Move at the motor's normal top speed.
        AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(idle, 0f);
        tree.AddChild(move, 2.5f);
        sm.defaultState = locomotion;

        AnimatorState attackState = sm.AddState("Attack");
        attackState.motion = attack;
        AnimatorStateTransition toAttack = locomotion.AddTransition(attackState);
        toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        toAttack.hasExitTime = false;
        toAttack.duration = 0.05f;
        AnimatorStateTransition backToLoco = attackState.AddTransition(locomotion);
        backToLoco.hasExitTime = true;
        backToLoco.exitTime = 0.9f;
        backToLoco.duration = 0.1f;

        AnimatorState deathState = sm.AddState("Death");
        deathState.motion = death;
        AnimatorStateTransition toDeath = sm.AddAnyStateTransition(deathState);
        toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
        toDeath.canTransitionToSelf = false;
        toDeath.duration = 0.05f;

        AssetDatabase.SaveAssets();
        return controller;
    }

    private static AnimationClip MakeIdle()
    {
        var c = new AnimationClip();
        float[] t = { 0f, 1f, 2f };
        Pos(c, Body, t, BodyRest, BodyRest + new Vector3(0f, 0.03f, 0f), BodyRest);   // breathing bob
        Rot(c, Body, t, Vector3.zero, Vector3.zero, Vector3.zero);
        float[] th = { 0f, 0.5f, 1f, 1.5f, 2f };
        Pos(c, Head, th, HeadRest, HeadRest, HeadRest, HeadRest, HeadRest);
        Rot(c, Head, th, Vector3.zero, new Vector3(0f, 15f, 0f), Vector3.zero, new Vector3(0f, -15f, 0f), Vector3.zero); // scanning
        Pos(c, Arm, t, ArmRest, ArmRest, ArmRest);
        Rot(c, Arm, t, Vector3.zero, Vector3.zero, Vector3.zero);
        return c;
    }

    private static AnimationClip MakeMove()
    {
        var c = new AnimationClip();
        float[] t = { 0f, 0.15f, 0.3f, 0.45f, 0.6f };
        Vector3 up = BodyRest + new Vector3(0f, 0.05f, 0f);
        Pos(c, Body, t, BodyRest, up, BodyRest, up, BodyRest);                       // stride bob
        Vector3 lean = new Vector3(8f, 0f, 0f);
        Rot(c, Body, t, lean + new Vector3(0, 0, 3), lean, lean + new Vector3(0, 0, -3), lean, lean + new Vector3(0, 0, 3)); // sway
        Pos(c, Head, t, HeadRest, HeadRest, HeadRest, HeadRest, HeadRest);
        Vector3 look = new Vector3(-8f, 0f, 0f);                                      // keeps looking ahead
        Rot(c, Head, t, look, look, look, look, look);
        Pos(c, Arm, t, ArmRest, ArmRest, ArmRest, ArmRest, ArmRest);
        Rot(c, Arm, t, new Vector3(-10, 0, 0), new Vector3(-14, 0, 0), new Vector3(-10, 0, 0), new Vector3(-14, 0, 0), new Vector3(-10, 0, 0));
        return c;
    }

    private static AnimationClip MakeAttack()
    {
        var c = new AnimationClip();
        float[] t = { 0f, 0.08f, 0.3f, 0.5f };
        Pos(c, Body, t, BodyRest, BodyRest, BodyRest, BodyRest);
        Rot(c, Body, t, Vector3.zero, new Vector3(-6, 0, 0), Vector3.zero, Vector3.zero); // braces against recoil
        Pos(c, Head, t, HeadRest, HeadRest, HeadRest, HeadRest);
        Rot(c, Head, t, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero);
        Vector3 kick = ArmRest + new Vector3(0f, 0f, -0.1f);
        Pos(c, Arm, t, ArmRest, kick, ArmRest, ArmRest);                               // recoil
        Rot(c, Arm, t, new Vector3(-10, 0, 0), new Vector3(-25, 0, 0), new Vector3(-10, 0, 0), new Vector3(-10, 0, 0));
        return c;
    }

    private static AnimationClip MakeDeath()
    {
        var c = new AnimationClip();
        float[] t = { 0f, 0.3f, 1f, 1.2f };
        Pos(c, Body, t, BodyRest, BodyRest + new Vector3(0, -0.05f, 0), new Vector3(0, 0.35f, 0), new Vector3(0, 0.35f, 0)); // drops
        Rot(c, Body, t, Vector3.zero, new Vector3(20, 0, 0), new Vector3(85, 0, 0), new Vector3(85, 0, 0));                 // topples
        Pos(c, Head, t, HeadRest, HeadRest, HeadRest, HeadRest);
        Rot(c, Head, t, Vector3.zero, new Vector3(0, 0, 10), new Vector3(0, 0, 30), new Vector3(0, 0, 30));
        Pos(c, Arm, t, ArmRest, ArmRest, ArmRest, ArmRest);
        Rot(c, Arm, t, Vector3.zero, new Vector3(20, 0, 0), new Vector3(40, 0, 0), new Vector3(40, 0, 0));
        return c;
    }

    // Curves for all three components, so unanimated axes keep their rest value.
    private static void Pos(AnimationClip clip, string path, float[] times, params Vector3[] values) =>
        SetVector(clip, path, "m_LocalPosition", times, values);

    private static void Rot(AnimationClip clip, string path, float[] times, params Vector3[] values) =>
        SetVector(clip, path, "localEulerAnglesRaw", times, values);

    private static void SetVector(AnimationClip clip, string path, string property, float[] times, Vector3[] values)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            var keys = new Keyframe[times.Length];
            for (int i = 0; i < times.Length; i++) keys[i] = new Keyframe(times[i], values[i][axis]);
            string name = property + "." + "xyz"[axis];
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), name), new AnimationCurve(keys));
        }
    }

    private static AnimationClip SaveClip(AnimationClip clip, string name, bool loop)
    {
        string path = $"{AnimFolder}/{name}.anim";
        AssetDatabase.DeleteAsset(path);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    // ---------------- Stand-in robot ----------------

    private static GameObject BuildStandIn(Transform robot, Material bodyMat, Material accentMat, Material visorMat)
    {
        // The capsule pivot is at its centre, so the visual's feet sit 1 m below it.
        GameObject visual = Empty("Visual", robot, new Vector3(0f, -1f, 0f));
        Undo.RegisterCreatedObjectUndo(visual, "Build Hunter stand-in");

        Part("Base", visual.transform, new Vector3(0f, 0.25f, 0f), new Vector3(0.55f, 0.5f, 0.45f), accentMat);

        GameObject body = Empty("Body", visual.transform, BodyRest);
        Part("BodyMesh", body.transform, Vector3.zero, new Vector3(0.7f, 0.8f, 0.5f), bodyMat);

        GameObject head = Empty("Head", body.transform, HeadRest);
        Part("HeadMesh", head.transform, Vector3.zero, new Vector3(0.4f, 0.3f, 0.35f), bodyMat);
        Part("Visor", head.transform, new Vector3(0f, 0.03f, 0.18f), new Vector3(0.3f, 0.08f, 0.02f), visorMat);

        GameObject arm = Empty("GunArm", body.transform, ArmRest);
        Part("ArmMesh", arm.transform, new Vector3(0f, 0f, 0.15f), new Vector3(0.15f, 0.15f, 0.5f), accentMat);
        Part("Barrel", arm.transform, new Vector3(0f, 0f, 0.5f), new Vector3(0.06f, 0.06f, 0.3f), bodyMat);

        return visual;
    }

    private static void LinkAnimator(GameObject robot, GameObject visual, AnimatorController controller)
    {
        Animator animator = visual.GetComponent<Animator>();
        if (animator == null) animator = Undo.AddComponent<Animator>(visual);
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        if (robot.GetComponent<HunterPresentation>() == null) Undo.AddComponent<HunterPresentation>(robot);
    }

    private static GameObject Empty(string name, Transform parent, Vector3 localPosition)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        return go;
    }

    private static void Part(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        // No collider: the CharacterController handles collisions, and part
        // colliders would block the robot's own line-of-sight raycasts.
        Object.DestroyImmediate(cube.GetComponent<Collider>());
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localScale = scale;
        cube.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static Material GetMaterial(string name, Color color, Color emission)
    {
        EnsureFolder(MatFolder);
        string path = $"{MatFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (emission.maxColorComponent > 0f && mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
        }
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
