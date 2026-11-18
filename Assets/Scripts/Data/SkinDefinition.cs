using System.Collections.Generic;
using UnityEngine;

public enum SkinUnlock
{
    Free,
    Height,
    Checkpoint,
    Coins,
    Points,

    /// <summary>Выдаётся только за 7-й день ежедневной награды. Купить нельзя,
    /// обменять нельзя — единственный способ получить такой скин.</summary>
    Daily,

    /// <summary>Награда батл-пасса (бесплатная или премиум-дорожка). Купить
    /// нельзя — только получить из пасса.</summary>
    Pass
}

/// <summary>Текстуры одной части импортированной модели: ключ — кусок имени материала.</summary>
public class SkinTexture
{
    public string Diffuse;
    public string Normal;
}

/// <summary>
/// Поза покоя одной кости. Нужна моделям, у которых рабочее положение кости
/// отличается от bind-pose: у моделей Dota оружие в bind-pose лежит у ног, а в
/// руку его ставит только анимация, которой при процедурной анимации нет.
/// </summary>
public class SkinBonePose
{
    public string Bone;
    public Vector3 Position;
    public Vector3 Euler;
}

public class SkinDefinition
{
    public string Id = "skin";
    public string Title = "Скин";
    public string Description = "";

    /// <summary>Путь в Resources для импортированной FBX/префаба. Пусто — модель строится процедурно.</summary>
    public string ResourcePath;

    public Color BodyColor = new Color(0.2f, 0.85f, 1f, 1f);
    public Color AccentColor = new Color(0.12f, 0.44f, 0.66f, 1f);
    public Color TrimColor = new Color(0.06f, 0.13f, 0.2f, 1f);

    /// <summary>Толщина процедурной модели (1 — стандартная).</summary>
    public float Bulk = 1f;

    /// <summary>Высота модели в CharacterController-метрах (1 — около 1.9 м).</summary>
    public float ModelScale = 1f;

    /// <summary>Поворот импортированной модели, если в FBX она смотрит не в +Z.</summary>
    public float ModelYaw;

    /// <summary>Красить импортированную модель цветом BodyColor. Нужно, только если у модели нет текстур.</summary>
    public bool TintImported;

    /// <summary>Текстуры по частям модели. Ключ — часть имени материала/сабмеша («armor», «wings»).</summary>
    public readonly Dictionary<string, SkinTexture> Textures = new Dictionary<string, SkinTexture>();

    /// <summary>
    /// Имена костей модели, если они не совпадают со стандартными (Unity/Mixamo/
    /// Blender). Ключ — роль (`hips`, `spine`, `chest`, `head`, `armL`, `foreL`,
    /// `armR`, `foreR`, `legL`, `shinL`, `legR`, `shinR`), значение — точное имя
    /// кости в модели. Пусто — кости ищутся обычными синонимами.
    /// </summary>
    public readonly Dictionary<string, string> BoneNames = new Dictionary<string, string>();

    /// <summary>
    /// Крутить кости в системе модели, а не в собственных осях кости. Нужно ригам,
    /// у которых «взмах вперёд-назад» идёт не вокруг локального X (Source/Dota):
    /// в системе модели X всегда означает наклон вперёд-назад, независимо от того,
    /// как повёрнуты кости внутри модели.
    /// </summary>
    public bool ModelSpaceAnimation;

    /// <summary>Позы покоя отдельных костей, выставляемые после сборки модели.</summary>
    public readonly List<SkinBonePose> BonePoses = new List<SkinBonePose>();

    /// <summary>Меши, которые надо погасить (например, дубли в FBX). Сравнение — по имени узла-владельца.</summary>
    public readonly List<string> HiddenParts = new List<string>();

    public SkinUnlock Unlock = SkinUnlock.Free;
    public int UnlockValue;

    public bool IsProcedural { get { return string.IsNullOrEmpty(ResourcePath); } }

    /// <summary>Имя кости для роли из BoneNames, если оно задано. Иначе — пусто.</summary>
    public string BoneName(string role)
    {
        string name;
        if (BoneNames.TryGetValue(role, out name)) return name;
        return null;
    }

    /// <summary>Есть ли чему красить модель: текстуры из Resources.</summary>
    public bool HasTextures { get { return Textures.Count > 0; } }

    /// <summary>Найти текстуру по имени материала: сначала точное вхождение, потом самый длинный ключ.</summary>
    public SkinTexture FindTexture(string materialName)
    {
        if (Textures.Count == 0 || string.IsNullOrEmpty(materialName)) return null;

        string lower = materialName.ToLowerInvariant();

        SkinTexture best = null;
        int bestLength = 0;
        foreach (KeyValuePair<string, SkinTexture> pair in Textures)
        {
            if (lower.Contains(pair.Key) && pair.Key.Length > bestLength)
            {
                best = pair.Value;
                bestLength = pair.Key.Length;
            }
        }
        return best;
    }

    public string RequirementText()
    {
        switch (Unlock)
        {
            case SkinUnlock.Free:
                return "Доступен сразу";
            case SkinUnlock.Height:
                return "Рекорд " + UnlockValue + " м";
            case SkinUnlock.Checkpoint:
                return "Чекпоинт " + UnlockValue;
            case SkinUnlock.Coins:
                return UnlockValue + " монет";
            case SkinUnlock.Points:
                return UnlockValue + " очков";
            case SkinUnlock.Daily:
                return "Награда за 7-й день";
            case SkinUnlock.Pass:
                return "Награда батл-пасса";
        }
        return "";
    }
}
