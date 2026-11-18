using UnityEngine;

/// <summary>
/// Стартовая low-poly модель человека, собирается кодом из примитивов.
/// Кости названы по стандарту Unity Humanoid, поэтому SkinAnimator одинаково
/// работает и с этой моделью, и с импортированным FBX.
/// </summary>
public static class ProceduralCharacter
{
    private const float HipHeight = 0.94f;
    private const float ThighLength = 0.44f;
    private const float ShinLength = 0.41f;
    private const float ArmLength = 0.30f;
    private const float ForearmLength = 0.27f;
    private const float SpineLength = 0.18f;
    private const float ChestLength = 0.24f;
    private const float NeckLength = 0.14f;
    private const float HeadOffset = 0.17f;
    private const float ShoulderX = 0.10f;
    private const float ArmX = 0.17f;
    private const float LegX = 0.11f;

    public static GameObject Build(Transform parent, SkinDefinition skin)
    {
        if (skin == null) skin = SkinCatalog.Default;
        float bulk = Mathf.Clamp(skin.Bulk <= 0f ? 1f : skin.Bulk, 0.6f, 1.8f);

        Material body = Palette.Solid(skin.BodyColor);
        Material accent = Palette.Solid(skin.AccentColor);
        Material trim = Palette.Solid(skin.TrimColor);

        GameObject root = new GameObject("Rig");
        root.transform.SetParent(parent, false);

        Transform hips = Bone(root.transform, "Hips", new Vector3(0f, HipHeight, 0f));
        Part(hips, "HipsMesh", PrimitiveType.Cube, new Vector3(0.40f, 0.22f, 0.24f) * bulk, Vector3.zero, body);

        Transform spine = Bone(hips, "Spine", new Vector3(0f, SpineLength, 0f));
        Transform chest = Bone(spine, "Chest", new Vector3(0f, ChestLength, 0f));
        Part(chest, "TorsoMesh", PrimitiveType.Cube, new Vector3(0.42f, 0.58f, 0.24f) * bulk, new Vector3(0f, -0.20f, 0f), body);

        Transform neck = Bone(chest, "Neck", new Vector3(0f, NeckLength, 0f));
        Transform head = Bone(neck, "Head", new Vector3(0f, HeadOffset, 0f));
        Part(head, "HeadMesh", PrimitiveType.Sphere, new Vector3(0.40f, 0.42f, 0.40f) * bulk, new Vector3(0f, 0.02f, 0f), body);
        Part(head, "CrestMesh", PrimitiveType.Cube, new Vector3(0.07f, 0.10f, 0.20f) * bulk, new Vector3(0f, 0.20f, -0.01f), accent);
        Part(head, "EyeL", PrimitiveType.Cube, new Vector3(0.05f, 0.07f, 0.04f) * bulk, new Vector3(-0.085f, 0.02f, 0.17f) * bulk, trim);
        Part(head, "EyeR", PrimitiveType.Cube, new Vector3(0.05f, 0.07f, 0.04f) * bulk, new Vector3(0.085f, 0.02f, 0.17f) * bulk, trim);

        BuildArm(chest, -1f, bulk, accent, trim);
        BuildArm(chest, 1f, bulk, accent, trim);
        BuildLeg(hips, -1f, bulk, accent, trim);
        BuildLeg(hips, 1f, bulk, accent, trim);

        return root;
    }

    private static void BuildArm(Transform chest, float side, float bulk, Material limb, Material trim)
    {
        string s = side < 0f ? "Left" : "Right";
        Transform shoulder = Bone(chest, s + "Shoulder", new Vector3(side * ShoulderX * bulk, 0.10f, 0f));
        Transform arm = Bone(shoulder, s + "Arm", new Vector3(side * ArmX * bulk, 0f, 0f));
        Part(arm, "UpperArmMesh", PrimitiveType.Cube, new Vector3(0.13f, ArmLength, 0.13f) * bulk, new Vector3(0f, -ArmLength * 0.5f, 0f), limb);

        Transform forearm = Bone(arm, s + "ForeArm", new Vector3(0f, -ArmLength, 0f));
        Part(forearm, "ForearmMesh", PrimitiveType.Cube, new Vector3(0.115f, ForearmLength, 0.115f) * bulk, new Vector3(0f, -ForearmLength * 0.5f, 0f), limb);

        Transform hand = Bone(forearm, s + "Hand", new Vector3(0f, -ForearmLength, 0f));
        Part(hand, "HandMesh", PrimitiveType.Cube, new Vector3(0.12f, 0.12f, 0.12f) * bulk, new Vector3(0f, -0.05f, 0f), trim);
    }

    private static void BuildLeg(Transform hips, float side, float bulk, Material limb, Material trim)
    {
        string s = side < 0f ? "Left" : "Right";
        Transform upLeg = Bone(hips, s + "UpLeg", new Vector3(side * LegX * bulk, 0f, 0f));
        Part(upLeg, "ThighMesh", PrimitiveType.Cube, new Vector3(0.17f, ThighLength, 0.17f) * bulk, new Vector3(0f, -ThighLength * 0.5f, 0f), limb);

        Transform leg = Bone(upLeg, s + "Leg", new Vector3(0f, -ThighLength, 0f));
        Part(leg, "ShinMesh", PrimitiveType.Cube, new Vector3(0.15f, ShinLength, 0.15f) * bulk, new Vector3(0f, -ShinLength * 0.5f, 0f), limb);

        Transform foot = Bone(leg, s + "Foot", new Vector3(0f, -ShinLength, 0f));
        Part(foot, "FootMesh", PrimitiveType.Cube, new Vector3(0.16f, 0.10f, 0.28f) * bulk, new Vector3(0f, -0.04f, 0.06f), trim);
    }

    private static Transform Bone(Transform parent, string name, Vector3 localPosition)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    private static Transform Part(Transform bone, string name, PrimitiveType type, Vector3 size, Vector3 localCenter, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(bone, false);
        go.transform.localPosition = localCenter;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = size;

        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            Object.Destroy(collider);

        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;

        return go.transform;
    }
}
