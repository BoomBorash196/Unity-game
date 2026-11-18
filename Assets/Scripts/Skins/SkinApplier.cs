using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ставит модель скина на префаб игрока: чистит старую геометрию, строит или
/// подгружает модель, подгоняет её под CharacterController и вешает SkinAnimator.
/// </summary>
public static class SkinApplier
{
    public const float TargetHeight = 1.9f;

    public static SkinAnimator Apply(GameObject playerRoot, SkinDefinition skin)
    {
        if (playerRoot == null) return null;
        if (skin == null) skin = SkinCatalog.Default;

        PlayerController player = playerRoot.GetComponent<PlayerController>();
        Transform body = ResolveBody(playerRoot);

        GameObject model = BuildModel(skin, body);
        if (model == null) return null;

        // Позы костей и скрытые дубли ставим ДО подгонки: у моделей Dota крюк
        // лежит в bind-pose у ног, и без этого он раздувал бы габариты, по которым
        // считается масштаб и посадка в капсулу.
        ApplyModelSetup(model, skin);

        FitToCharacter(model, body, skin);

        SkinAnimator animator = body.GetComponent<SkinAnimator>();
        if (animator == null)
            animator = body.gameObject.AddComponent<SkinAnimator>();
        animator.Bind(player, model.transform, skin);

        if (player != null)
            player.SetVisualRoot(body);

        return animator;
    }

    private static Transform ResolveBody(GameObject playerRoot)
    {
        Transform body = playerRoot.transform.Find("Body");
        if (body == null)
        {
            GameObject go = new GameObject("Body");
            go.transform.SetParent(playerRoot.transform, false);
            body = go.transform;
        }

        // В префабе на Body висит шар-заглушка — выкидываем всё старое содержимое.
        ClearChildren(body);
        Kill(body.GetComponent<MeshFilter>());
        KillComponents<Renderer>(body);
        KillComponents<Collider>(body);

        body.localPosition = Vector3.zero;
        body.localRotation = Quaternion.identity;
        body.localScale = Vector3.one;
        return body;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Kill(parent.GetChild(i).gameObject);
    }

    private static void KillComponents<T>(Component host) where T : Component
    {
        T[] list = host.GetComponents<T>();
        for (int i = 0; i < list.Length; i++)
            Kill(list[i]);
    }

    /// <summary>В редакторе Destroy отложенный, а нам нужно убрать объект прямо сейчас.</summary>
    private static void Kill(Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Object.Destroy(target);
        else Object.DestroyImmediate(target);
    }

    private static GameObject BuildModel(SkinDefinition skin, Transform body)
    {
        if (!skin.IsProcedural)
        {
            GameObject asset = LoadModel(skin);
            if (asset != null)
            {
                GameObject model = Object.Instantiate(asset, body, false);
                model.name = string.IsNullOrEmpty(skin.Title) ? "Model" : skin.Title;
                StripImported(model);
                DressModel(model, skin);
                return model;
            }
        }

        return ProceduralCharacter.Build(body, skin);
    }

    private static GameObject LoadModel(SkinDefinition skin)
    {
        GameObject asset = null;
        if (!string.IsNullOrEmpty(skin.ResourcePath))
            asset = Resources.Load<GameObject>(skin.ResourcePath);
        if (asset == null && !string.IsNullOrEmpty(skin.Id))
            asset = Resources.Load<GameObject>(SkinCatalog.ResourceFolder + "/" + skin.Id);
        return asset;
    }

    /// <summary>Убирает из импортированной модели всё, что не должно жить на игроке.</summary>
    private static void StripImported(GameObject model)
    {
        Collider[] colliders = model.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            Kill(colliders[i]);

        Kill(model.GetComponentInChildren<Camera>(true));
        Kill(model.GetComponentInChildren<Light>(true));
        Kill(model.GetComponentInChildren<AudioSource>(true));
        Kill(model.GetComponentInChildren<Animation>(true));
    }

    /// <summary>Ставит позы покоя костей и гасит дубли — до подгонки модели.</summary>
    private static void ApplyModelSetup(GameObject model, SkinDefinition skin)
    {
        HideParts(model, skin);
        ApplyBonePoses(model, skin);
    }

    /// <summary>Гасит меши, которые в FBX продублированы (иначе они мерцают друг в друге).</summary>
    private static void HideParts(GameObject model, SkinDefinition skin)
    {
        if (skin.HiddenParts.Count == 0) return;

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            for (int j = 0; j < skin.HiddenParts.Count; j++)
            {
                if (renderers[i].name == skin.HiddenParts[j])
                {
                    renderers[i].enabled = false;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Возвращает костям рабочую позу: у моделей Dota оружие держит анимация, а
    /// без неё оно остаётся в bind-pose у ног. Позу задаёт скин (SkinBonePose).
    /// </summary>
    private static void ApplyBonePoses(GameObject model, SkinDefinition skin)
    {
        if (skin.BonePoses.Count == 0) return;

        Transform[] all = model.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < skin.BonePoses.Count; i++)
        {
            SkinBonePose pose = skin.BonePoses[i];
            if (pose == null || string.IsNullOrEmpty(pose.Bone)) continue;

            for (int j = 0; j < all.Length; j++)
            {
                if (all[j].name != pose.Bone) continue;
                all[j].localPosition = pose.Position;
                all[j].localRotation = Quaternion.Euler(pose.Euler);
                break;
            }
        }
    }

    /// <summary>Раздаёт импортированной модели материалы: текстуры, иначе — плоский цвет.</summary>
    private static void DressModel(GameObject model, SkinDefinition skin)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        bool textured = skin.HasTextures;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;

            Material[] current = renderer.sharedMaterials;
            Material[] result = new Material[current.Length];

            for (int j = 0; j < current.Length; j++)
            {
                Material source = current[j];
                string sourceName = source != null ? source.name : string.Empty;
                SkinTexture wanted = textured ? skin.FindTexture(sourceName) : null;

                if (wanted != null)
                {
                    result[j] = TexturedMaterial(wanted);
                }
                else if (textured)
                {
                    // Часть без своей текстуры: оставляем как есть, лишь заменяя заглушку.
                    result[j] = IsPlaceholder(source) ? Palette.Solid(skin.TrimColor) : UsableMaterial(source);
                }
                else if (skin.TintImported || IsPlaceholder(source))
                {
                    // Своих текстур нет — красим в цвет скина.
                    result[j] = Palette.Solid(skin.BodyColor);
                }
                else
                {
                    // Модель пришла со своими цветами (LowPolyMage) — не трогаем их.
                    result[j] = UsableMaterial(source);
                }
            }

            renderer.sharedMaterials = result;
        }
    }

    /// <summary>Материал-заглушка Unity: своих цветов у модели нет, красить нужно.</summary>
    private static bool IsPlaceholder(Material material)
    {
        return material == null || material.name == "Default-Material";
    }

    private static readonly Dictionary<Material, Material> _sourceCache = new Dictionary<Material, Material>();

    /// <summary>
    /// Материалы из FBX часто сидят на устаревших шейдерах (Diffuse/Lambert), которых
    /// в Unity 6 уже нет — такой рендерится розовым. Переносим цвет на Standard.
    /// </summary>
    private static Material UsableMaterial(Material source)
    {
        if (source == null) return null;
        if (source.shader != null && source.shader.isSupported) return source;

        Material cached;
        if (_sourceCache.TryGetValue(source, out cached) && cached != null)
            return cached;

        Color tint = Color.white;
        if (source.HasProperty("_Color")) tint = source.color;
        else if (source.HasProperty("_DiffuseColor")) tint = source.GetColor("_DiffuseColor");

        Material replacement = Palette.Solid(tint);
        _sourceCache[source] = replacement;
        return replacement;
    }

    private static readonly Dictionary<SkinTexture, Material> _texturedCache = new Dictionary<SkinTexture, Material>();

    private static Material TexturedMaterial(SkinTexture wanted)
    {
        Material cached;
        if (_texturedCache.TryGetValue(wanted, out cached) && cached != null)
            return cached;

        Texture2D diffuse = null;
        Texture2D normal = null;
        if (!string.IsNullOrEmpty(wanted.Diffuse)) diffuse = Resources.Load<Texture2D>(wanted.Diffuse);
        if (!string.IsNullOrEmpty(wanted.Normal)) normal = Resources.Load<Texture2D>(wanted.Normal);

        Material mat = Palette.Textured(diffuse, normal);
        _texturedCache[wanted] = mat;
        return mat;
    }

    /// <summary>Масштабирует модель под рост скина, ставит ступни на ноль и центрирует по оси капсулы.</summary>
    private static void FitToCharacter(GameObject model, Transform body, SkinDefinition skin)
    {
        if (model == null) return;

        Transform root = model.transform;
        root.localPosition = Vector3.zero;
        // Границы меряем ДО поворота: иначе worldToLocal уже включает ModelYaw
        // и центрирование по Z уезжает в другую сторону.
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;

        Vector3 min;
        Vector3 max;
        if (!LocalBounds(model, root, out min, out max))
        {
            root.localRotation = Quaternion.Euler(0f, skin.ModelYaw, 0f);
            return;
        }

        float target = TargetHeight * Mathf.Max(0.2f, skin.ModelScale);
        float scale = target / Mathf.Max(0.0001f, max.y - min.y);

        // Центр считаем в неповёрнутых координатах, потом поворачиваем — так поправка
        // по X и Z попадает в ту же систему, в которой живёт localPosition.
        Quaternion yaw = Quaternion.Euler(0f, skin.ModelYaw, 0f);
        Vector3 center = yaw * ((min + max) * 0.5f);

        root.localRotation = yaw;
        root.localScale = new Vector3(scale, scale, scale);
        root.localPosition = new Vector3(-center.x * scale, -min.y * scale, -center.z * scale);
    }

    private static bool LocalBounds(GameObject model, Transform root, out Vector3 min, out Vector3 max)
    {
        min = Vector3.zero;
        max = Vector3.zero;
        bool any = false;

        Matrix4x4 toLocal = root.worldToLocalMatrix;
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Vector3 rmin;
            Vector3 rmax;
            if (!RendererWorldBounds(renderers[i], out rmin, out rmax)) continue;
            Accumulate(toLocal, rmin, rmax, ref min, ref max, ref any);
        }

        return any;
    }

    /// <summary>
    /// Габариты одного рендерера в мировых координатах. У SkinnedMeshRenderer нельзя
    /// взять `sharedMesh.bounds`: это bind-pose, и крюк, пересаженный костью в руку,
    /// всё ещё мерился бы у ног и раздувал масштаб модели. Поэтому вершины скинятся
    /// вручную по костям.
    /// </summary>
    public static bool RendererWorldBounds(Renderer renderer, out Vector3 min, out Vector3 max)
    {
        min = Vector3.zero;
        max = Vector3.zero;
        if (renderer == null || !renderer.enabled) return false;

        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null)
        {
            Bounds box = filter.sharedMesh.bounds;
            Matrix4x4 matrix = renderer.transform.localToWorldMatrix;
            Vector3 center = matrix.MultiplyPoint3x4(box.center);
            Vector3 extents = Abs(matrix.MultiplyVector(box.extents));
            min = center - extents;
            max = center + extents;
            return true;
        }

        SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
        Mesh mesh = skinned != null ? skinned.sharedMesh : null;
        Transform[] bones = skinned != null ? skinned.bones : null;
        if (mesh == null || bones == null || bones.Length == 0) return false;

        // В редакторе вершины читаются даже при isReadable = false, а в сборке —
        // нет. Проверяем флаг сами, чтобы в билде тихо уйти на запасной путь,
        // а не сыпать ошибками «Not allowed to access vertices».
        if (!mesh.isReadable) return FallbackWorldBounds(renderer, out min, out max);

        Vector3[] vertices;
        BoneWeight[] weights;
        try
        {
            vertices = mesh.vertices;
            weights = mesh.boneWeights;
        }
        catch (UnityException)
        {
            return FallbackWorldBounds(renderer, out min, out max);
        }
        if (vertices == null || vertices.Length == 0) return FallbackWorldBounds(renderer, out min, out max);

        Matrix4x4[] skin = new Matrix4x4[bones.Length];
        for (int b = 0; b < bones.Length; b++)
        {
            Transform bone = bones[b];
            Matrix4x4 bind = b < mesh.bindposes.Length ? mesh.bindposes[b] : Matrix4x4.identity;
            skin[b] = (bone != null ? bone.localToWorldMatrix : Matrix4x4.identity) * bind;
        }

        bool any = false;
        for (int v = 0; v < vertices.Length; v++)
        {
            BoneWeight weight;
            if (weights != null && v < weights.Length)
            {
                weight = weights[v];
            }
            else
            {
                weight = new BoneWeight();
                weight.boneIndex0 = 0;
                weight.weight0 = 1f;
            }

            Vector3 point = vertices[v];
            Vector3 world = Vector3.zero;
            float total = 0f;
            SkinnedPoint(skin, weight.boneIndex0, weight.weight0, point, ref world, ref total);
            SkinnedPoint(skin, weight.boneIndex1, weight.weight1, point, ref world, ref total);
            SkinnedPoint(skin, weight.boneIndex2, weight.weight2, point, ref world, ref total);
            SkinnedPoint(skin, weight.boneIndex3, weight.weight3, point, ref world, ref total);

            world = total > 0f ? world / total : skin[0].MultiplyPoint3x4(point);

            if (!any)
            {
                min = world;
                max = world;
                any = true;
            }
            else
            {
                min = Vector3.Min(min, world);
                max = Vector3.Max(max, world);
            }
        }

        return any;
    }

    private static void SkinnedPoint(Matrix4x4[] skin, int index, float weight, Vector3 point, ref Vector3 world, ref float total)
    {
        if (weight <= 0f || index < 0 || index >= skin.Length) return;
        world += skin[index].MultiplyPoint3x4(point) * weight;
        total += weight;
    }

    /// <summary>
    /// Запасной путь, когда вершины недоступны (например, в сборке при
    /// `isReadable = false`): берём габариты, которые Unity сама посчитала по
    /// текущей позе скелета. Менее точные, но модель всё равно встанет под рост,
    /// а не останется неотмасштабированной.
    /// </summary>
    private static bool FallbackWorldBounds(Renderer renderer, out Vector3 min, out Vector3 max)
    {
        Bounds box = renderer.bounds;
        min = box.min;
        max = box.max;
        return true;
    }

    private static Vector3 Abs(Vector3 v)
    {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }

    private static void Accumulate(Matrix4x4 matrix, Vector3 sourceMin, Vector3 sourceMax, ref Vector3 min, ref Vector3 max, ref bool any)
    {
        Vector3 center = matrix.MultiplyPoint3x4((sourceMin + sourceMax) * 0.5f);
        Vector3 extents = Abs(matrix.MultiplyVector((sourceMax - sourceMin) * 0.5f));
        Vector3 low = center - extents;
        Vector3 high = center + extents;

        if (!any)
        {
            min = low;
            max = high;
            any = true;
            return;
        }

        min = Vector3.Min(min, low);
        max = Vector3.Max(max, high);
    }
}
