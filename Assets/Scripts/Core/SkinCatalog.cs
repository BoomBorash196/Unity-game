using System.Collections.Generic;
using UnityEngine;

public static class SkinCatalog
{
    public const string ResourceFolder = "Skins";

    private static List<SkinDefinition> _skins;

    public static List<SkinDefinition> All
    {
        get
        {
            if (_skins == null)
                _skins = Build();
            return _skins;
        }
    }

    public static SkinDefinition Default
    {
        get
        {
            SkinDefinition skin = Get("classic");
            if (skin != null) return skin;
            return All.Count > 0 ? All[0] : null;
        }
    }

    public static SkinDefinition Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        List<SkinDefinition> all = All;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].Id == id)
                return all[i];
        }
        return null;
    }

    private static List<SkinDefinition> Build()
    {
        var list = new List<SkinDefinition>
        {
            new SkinDefinition
            {
                Id = "classic",
                Title = "Классик",
                Description = "Стартовый бегун",
                BodyColor = new Color(0.20f, 0.85f, 1.00f, 1f),
                AccentColor = new Color(0.12f, 0.44f, 0.66f, 1f),
                TrimColor = new Color(0.05f, 0.13f, 0.20f, 1f),
                Unlock = SkinUnlock.Free
            },
            new SkinDefinition
            {
                Id = "lime",
                Title = "Лайм",
                Description = "Свежий старт",
                BodyColor = new Color(0.42f, 0.91f, 0.42f, 1f),
                AccentColor = new Color(0.16f, 0.55f, 0.33f, 1f),
                TrimColor = new Color(0.08f, 0.19f, 0.12f, 1f),
                Unlock = SkinUnlock.Height,
                UnlockValue = 50
            },
            new SkinDefinition
            {
                Id = "sunset",
                Title = "Закат",
                Description = "Вечерний разбег",
                BodyColor = new Color(1.00f, 0.54f, 0.30f, 1f),
                AccentColor = new Color(0.77f, 0.25f, 0.45f, 1f),
                TrimColor = new Color(0.23f, 0.11f, 0.18f, 1f),
                Unlock = SkinUnlock.Height,
                UnlockValue = 150
            },
            new SkinDefinition
            {
                Id = "chrome",
                Title = "Хром",
                Description = "За рекорды",
                BodyColor = new Color(0.85f, 0.87f, 0.90f, 1f),
                AccentColor = new Color(0.54f, 0.58f, 0.64f, 1f),
                TrimColor = new Color(0.16f, 0.18f, 0.22f, 1f),
                Bulk = 1.08f,
                Unlock = SkinUnlock.Points,
                UnlockValue = 8
            },
            new SkinDefinition
            {
                Id = "neon",
                Title = "Неон",
                Description = "За монеты",
                BodyColor = new Color(1.00f, 0.24f, 0.78f, 1f),
                AccentColor = new Color(0.13f, 0.88f, 0.88f, 1f),
                TrimColor = new Color(0.07f, 0.04f, 0.12f, 1f),
                Unlock = SkinUnlock.Coins,
                UnlockValue = 200
            },
            new SkinDefinition
            {
                Id = "gold",
                Title = "Золото",
                Description = "Дорогой вкус",
                BodyColor = new Color(1.00f, 0.79f, 0.24f, 1f),
                AccentColor = new Color(0.79f, 0.54f, 0.09f, 1f),
                TrimColor = new Color(0.18f, 0.13f, 0.03f, 1f),
                Bulk = 1.12f,
                Unlock = SkinUnlock.Coins,
                UnlockValue = 700
            },
            new SkinDefinition
            {
                Id = "void",
                Title = "Пустота",
                Description = "Глубокий подъём",
                BodyColor = new Color(0.42f, 0.36f, 1.00f, 1f),
                AccentColor = new Color(0.14f, 0.11f, 0.30f, 1f),
                TrimColor = new Color(0.04f, 0.03f, 0.09f, 1f),
                Bulk = 0.92f,
                Unlock = SkinUnlock.Checkpoint,
                UnlockValue = 30
            }
        };

        list.Add(Willow());
        list.Add(Wingsage());
        list.AddRange(DailySkins());
        list.AddRange(PassSkins());

        var used = new HashSet<string>();
        for (int i = 0; i < list.Count; i++)
            used.Add(list[i].Id);

        List<SkinDefinition> imported = DiscoverImported(used);
        list.AddRange(imported);
        return list;
    }

    /// <summary>Папка с текстурами импортированных скинов внутри Resources.</summary>
    public const string TextureFolder = ResourceFolder + "/Textures/";

    /// <summary>dark_willow — FBX со скелетом, процедурно анимируется целиком.</summary>
    private static SkinDefinition Willow()
    {
        var skin = new SkinDefinition
        {
            Id = "dark_willow",
            Title = "Тёмная Ива",
            Description = "Со скелетом: бег, прыжок и стена считаются с ног",
            ResourcePath = ResourceFolder + "/dark_willow",
            BodyColor = new Color(0.32f, 0.26f, 0.40f, 1f),
            // В FBX персонаж повёрнут лицом в -Z — определено по костям лица.
            ModelYaw = 180f,
            Unlock = SkinUnlock.Coins,
            UnlockValue = 900
        };

        skin.Textures["armor"] = WillowPart("armor");
        skin.Textures["base"] = WillowPart("base");
        skin.Textures["head"] = WillowPart("head");
        skin.Textures["wings"] = WillowPart("wings");
        // Диск-чакрам в FBX назван chakram, а текстуры пришли как offhand.
        skin.Textures["chakram"] = new SkinTexture
        {
            Diffuse = TextureFolder + "dark_willow_offhand_color",
            Normal = TextureFolder + "dark_willow_offhand_normal"
        };
        return skin;
    }

    private static SkinTexture WillowPart(string part)
    {
        return new SkinTexture
        {
            Diffuse = TextureFolder + "dark_willow_" + part + "_color",
            Normal = TextureFolder + "dark_willow_" + part + "_normal"
        };
    }

    /// <summary>lowpolymage — FBX без скелета: только покачивание и взмахи крыльями.</summary>
    private static SkinDefinition Wingsage()
    {
        return new SkinDefinition
        {
            Id = "lowpolymage",
            Title = "Маг крыльев",
            Description = "Без скелета — парит и машет крыльями",
            ResourcePath = ResourceFolder + "/lowpolymage",
            BodyColor = new Color(0.45f, 0.40f, 0.85f, 1f),
            // Размах крыльев в FBX — 2,85 м при росте 1,9. Ужимаем, иначе крылья
            // проходят сквозь платформы. Правится одним числом.
            ModelScale = 0.8f,
            TintImported = false,
            Unlock = SkinUnlock.Free
        };
    }

    /// <summary>
    /// Набор скинов за 7-й день ежедневной награды. Отдельный набор нужен
    /// потому, что выдавать скин из магазина бессмысленно: он и так покупается
    /// за ту же валюту, ради которой игрок и заходит по ежедневке.
    ///
    /// Все семь процедурные — палитра и габариты разные, скелет и ритм бега те
    /// же, что у классика. Это честнее, чем семь почти одинаковых моделей: игрок
    /// видит различие, и оно читается на бегу.
    /// </summary>
    private static IEnumerable<SkinDefinition> DailySkins()
    {
        yield return Daily("daily_ruby", "Рубин", "День 7 · неделя 1",
            new Color(0.85f, 0.20f, 0.30f, 1f), new Color(0.44f, 0.07f, 0.13f, 1f), new Color(0.13f, 0.02f, 0.05f, 1f), 1.00f);
        yield return Daily("daily_cobalt", "Кобальт", "День 7 · неделя 2",
            new Color(0.20f, 0.38f, 0.86f, 1f), new Color(0.09f, 0.18f, 0.44f, 1f), new Color(0.03f, 0.06f, 0.15f, 1f), 0.94f);
        yield return Daily("daily_amber", "Янтарь", "День 7 · неделя 3",
            new Color(0.92f, 0.60f, 0.14f, 1f), new Color(0.52f, 0.26f, 0.05f, 1f), new Color(0.16f, 0.08f, 0.02f, 1f), 1.10f);
        yield return Daily("daily_teal", "Бирюза", "День 7 · неделя 4",
            new Color(0.16f, 0.74f, 0.70f, 1f), new Color(0.06f, 0.38f, 0.36f, 1f), new Color(0.02f, 0.11f, 0.11f, 1f), 0.90f);
        yield return Daily("daily_amethyst", "Аметист", "День 7 · неделя 5",
            new Color(0.62f, 0.30f, 0.86f, 1f), new Color(0.30f, 0.12f, 0.48f, 1f), new Color(0.09f, 0.04f, 0.16f, 1f), 1.04f);
        yield return Daily("daily_bone", "Кость", "День 7 · неделя 6",
            new Color(0.88f, 0.86f, 0.80f, 1f), new Color(0.44f, 0.42f, 0.38f, 1f), new Color(0.14f, 0.13f, 0.12f, 1f), 0.96f);
        yield return Daily("daily_ember", "Уголь", "День 7 · неделя 7",
            new Color(0.20f, 0.19f, 0.21f, 1f), new Color(0.58f, 0.22f, 0.06f, 1f), new Color(0.06f, 0.05f, 0.06f, 1f), 1.16f);
    }

    private static SkinDefinition Daily(string id, string title, string description,
        Color body, Color accent, Color trim, float bulk)
    {
        return new SkinDefinition
        {
            Id = id,
            Title = title,
            Description = description,
            BodyColor = body,
            AccentColor = accent,
            TrimColor = trim,
            Bulk = bulk,
            Unlock = SkinUnlock.Daily
        };
    }

    /// <summary>
    /// Скины батл-пасса: два в бесплатной дорожке, три в премиум. Выдаются
    /// только из пасса через SetSkinOwned — купить нельзя (не Coins/Points),
    /// поэтому в магазине они стоят «Закрыто» с подсказкой про пасс.
    ///
    /// Премиум-скины дороже по замыслу: серебро, аврора и властелин — то, ради
    /// чего покупается премиум. Обычные посвободнее — «Зелень» и «Лучезарный».
    /// </summary>
    private static IEnumerable<SkinDefinition> PassSkins()
    {
        yield return Pass("pass_verdant", "Зелень", "Свободная дорожка · ур. 10",
            new Color(0.30f, 0.86f, 0.42f, 1f), new Color(0.13f, 0.50f, 0.30f, 1f), new Color(0.05f, 0.18f, 0.10f, 1f));
        yield return Pass("pass_sunray", "Лучезарный", "Свободная дорожка · ур. 25",
            new Color(0.95f, 0.72f, 0.30f, 1f), new Color(0.80f, 0.36f, 0.10f, 1f), new Color(0.22f, 0.10f, 0.03f, 1f));
        yield return Pass("pass_argenteus", "Серебро", "Премиум · ур. 5",
            new Color(0.82f, 0.85f, 0.90f, 1f), new Color(0.48f, 0.52f, 0.60f, 1f), new Color(0.12f, 0.14f, 0.17f, 1f));
        yield return Pass("pass_aurora", "Аврора", "Премиум · ур. 17",
            new Color(0.22f, 0.78f, 0.72f, 1f), new Color(0.44f, 0.30f, 0.86f, 1f), new Color(0.05f, 0.09f, 0.16f, 1f));
        yield return Pass("pass_regal", "Властелин", "Премиум · ур. 30",
            new Color(0.42f, 0.24f, 0.60f, 1f), new Color(0.94f, 0.70f, 0.20f, 1f), new Color(0.06f, 0.03f, 0.12f, 1f));
    }

    private static SkinDefinition Pass(string id, string title, string description,
        Color body, Color accent, Color trim)
    {
        return new SkinDefinition
        {
            Id = id,
            Title = title,
            Description = description,
            BodyColor = body,
            AccentColor = accent,
            TrimColor = trim,
            Unlock = SkinUnlock.Pass
        };
    }

    /// <summary>Все модели, брошенные в Assets/Resources/Skins, сами становятся скинами.</summary>
    private static List<SkinDefinition> DiscoverImported(HashSet<string> used)
    {
        var result = new List<SkinDefinition>();
        GameObject[] assets;
        try
        {
            assets = Resources.LoadAll<GameObject>(ResourceFolder);
        }
        catch
        {
            return result;
        }

        if (assets == null) return result;

        for (int i = 0; i < assets.Length; i++)
        {
            GameObject asset = assets[i];
            if (asset == null) continue;

            SkinMarker marker = asset.GetComponentInChildren<SkinMarker>(true);
            string id = marker != null && !string.IsNullOrEmpty(marker.SkinId)
                ? marker.SkinId
                : asset.name.ToLowerInvariant();
            if (string.IsNullOrEmpty(id) || used.Contains(id)) continue;
            used.Add(id);

            SkinDefinition skin = new SkinDefinition
            {
                Id = id,
                ResourcePath = ResourceFolder + "/" + asset.name
            };

            if (marker != null)
            {
                skin.Title = string.IsNullOrEmpty(marker.Title) ? asset.name : marker.Title;
                skin.Description = marker.Description;
                skin.BodyColor = marker.BodyColor;
                skin.AccentColor = marker.AccentColor;
                skin.TrimColor = marker.TrimColor;
                skin.TintImported = marker.Tint;
                skin.ModelYaw = marker.ModelYaw;
                skin.ModelScale = Mathf.Max(0.2f, marker.ModelScale);
                skin.Unlock = marker.Unlock;
                skin.UnlockValue = marker.Unlock == SkinUnlock.Coins
                    ? Mathf.Max(0, marker.CoinCost)
                    : (marker.Unlock == SkinUnlock.Points ? Mathf.Max(0, marker.PointCost) : Mathf.Max(0, marker.UnlockValue));
            }
            else
            {
                skin.Title = asset.name;
                skin.Description = "Импортированная модель";
                // Покрасит только если на модели нет текстур — это решает SkinApplier.
                skin.TintImported = false;
                skin.Unlock = SkinUnlock.Height;
                skin.UnlockValue = 100;
            }

            result.Add(skin);
        }

        result.Sort(delegate (SkinDefinition a, SkinDefinition b) { return string.Compare(a.Title, b.Title); });
        return result;
    }
}
