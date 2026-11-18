using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Небо и туман. Небо одно на игру, но картинок в нём шесть: подъём на километр
/// обязан читаться как подъём, иначе высота перестаёт что-то значить.
///
/// Картинки взяты из пакета «8K Skybox Pack Free» — это кубы, а не панорамы:
/// в одном PNG лежат шесть граней. Шейдер пака Skybox/Cubemap умеет только
/// одну картинку, поэтому переход между поясами сделан своим шейдером
/// Parkour/SkyBlend: два куба в одном материале, доля второго идёт в _Blend.
///
/// Откатов три, и это не перестраховка, а три разные поломки, которые выглядят
/// одинаково — пустое небо. Шейдер не собрался, кубы не загрузились, или
/// встроенного шейдера нет в сборке. Порядок: плавно, потом подмена без
/// перехода, потом старое процедурное небо, и только потом однотонный фон.
///
/// Процедурных облачных ярусов больше нет: на присланных кубах облака уже
/// нарисованы, и плоскости поверх них давали бы двойное небо и серую муть.
/// </summary>
public static class Sky
{
    /// <summary>Один пояс неба. Между соседними поясами всё плавно интерполируется.</summary>
    private struct Band
    {
        public float altitude;
        public Color tint;
        public Color ground;
        public Color fog;
        public float fogDensity;
        public float atmosphere;
        public float exposure;
        public float sunSize;
    }

    /// <summary>Доля пояса, в конце которой картинка уходит на следующую.
    ///
    /// Не на границе: смена неба ровно на стыке ступеней читалась бы как
    /// подмена картинки, а не как перемена высоты. Последние 28 процентов
    /// пояса дают небу переехать, пока игрок ещё на старой ступени и ничего
    /// не ждёт.</summary>
    private const float FadeFraction = 0.28f;

    /// <summary>Минимальный шаг высоты, после которого небо и туман пересчитываются.
    /// Переход идёт на десятки метров, поэтому 5 см на глаз не отличаются,
    /// зато стоя на месте кадр не перекрашивает материалы зря. Проверка кривой
    /// берёт этот же шаг, иначе она измеряет не поведение игры, а догадку.</summary>
    public const float FollowStep = 0.05f;

    private const string BlendShader = "Parkour/SkyBlend";
    private const string SwapShader = "Skybox/Cubemap";
    private const string ProceduralShader = "Skybox/Procedural";

    private static readonly string[] BandTexProps = { "_TexA", "_TexB", "_Blend", "_Exposure", "_Tint" };
    private const string SwapTexProp = "_Tex";

    /// <summary>Какой скайбокс пака ставить поясу. Снизу вверх.
    ///
    /// Выбрано не на глаз, а по замеру: у каждого куба снят средний цвет зенита,
    /// горизонта и надира, и по ним выставлен порядок от местности с видимой
    /// землёй до тёмного неба с чёрным низом. Подробности в SkyboxPackCheck.</summary>
    private static readonly string[] PackSky =
    {
        "sky-1",   // зенит 53,73,90  горизонт 149,153,140  надир 221,219,191 — местность
        "sky-3",   // зенит 50,62,78   горизонт 143,153,166  надир 173,183,202 — день в дымке
        "sky-4",   // зенит 71,81,98   горизонт 130,147,163  надир 150,165,180 — чистый день
        "sky-9",   // зенит 36,64,138  горизонт 174,192,188  надир 193,185,161 — день над облаками
        "sky-11",  // зенит 119,124,128 горизонт 206,198,185 надир 197,195,189 — светлая дымка
        "sky-10"   // зенит 22,83,230  горизонт 19,41,167   надир 18,18,19   — ночь
    };

    private static readonly Band[] Bands =
    {
        new Band
        {
            altitude = -50f,
            tint = new Color(0.50f, 0.66f, 0.92f, 1f),
            ground = new Color(0.42f, 0.48f, 0.38f, 1f),
            fog = new Color(0.72f, 0.82f, 0.92f, 1f),
            fogDensity = 0.0115f,
            atmosphere = 1.05f,
            exposure = 1.30f,
            sunSize = 0.035f
        },
        new Band
        {
            altitude = 130f,
            tint = new Color(0.45f, 0.60f, 0.90f, 1f),
            ground = new Color(0.66f, 0.72f, 0.76f, 1f),
            fog = new Color(0.80f, 0.87f, 0.95f, 1f),
            fogDensity = 0.0095f,
            atmosphere = 0.92f,
            exposure = 1.25f,
            sunSize = 0.032f
        },
        new Band
        {
            altitude = 300f,
            tint = new Color(0.33f, 0.47f, 0.86f, 1f),
            ground = new Color(0.88f, 0.91f, 0.96f, 1f),
            fog = new Color(0.86f, 0.90f, 0.97f, 1f),
            fogDensity = 0.0070f,
            atmosphere = 0.78f,
            exposure = 1.18f,
            sunSize = 0.030f
        },
        new Band
        {
            altitude = 460f,
            tint = new Color(0.26f, 0.35f, 0.78f, 1f),
            ground = new Color(0.84f, 0.86f, 0.94f, 1f),
            fog = new Color(0.76f, 0.81f, 0.93f, 1f),
            fogDensity = 0.0055f,
            atmosphere = 0.68f,
            exposure = 1.14f,
            sunSize = 0.028f
        },
        new Band
        {
            altitude = 660f,
            tint = new Color(0.55f, 0.34f, 0.56f, 1f),
            ground = new Color(0.52f, 0.33f, 0.44f, 1f),
            fog = new Color(0.55f, 0.42f, 0.56f, 1f),
            fogDensity = 0.0045f,
            atmosphere = 0.72f,
            exposure = 1.10f,
            sunSize = 0.045f
        },
        new Band
        {
            altitude = 950f,
            tint = new Color(0.09f, 0.12f, 0.30f, 1f),
            ground = new Color(0.07f, 0.09f, 0.19f, 1f),
            fog = new Color(0.12f, 0.16f, 0.32f, 1f),
            fogDensity = 0.0035f,
            atmosphere = 0.55f,
            exposure = 1.05f,
            sunSize = 0.050f
        }
    };

    /// <summary>Высоты полос неба, снизу вверх.
    ///
    /// Ступени подъёма обязаны начинаться на этих же высотах. Иначе игрок
    /// увидит, как небо посветлело на 305 м, а под ногами ещё лес до 300 м,
    /// и подъём начнёт читаться неправильно. Проверка совпадения живёт в
    /// StageCheck — отсюда и нужен доступ наружу.</summary>
    public static float[] BandAltitudes
    {
        get
        {
            var heights = new float[Bands.Length];
            for (int i = 0; i < Bands.Length; i++)
                heights[i] = Bands[i].altitude;
            return heights;
        }
    }

    private const int ModeNone = 0;
    private const int ModeBlend = 1;
    private const int ModeSwap = 2;
    private const int ModeProcedural = 3;

    /// <summary>Имя скайбокса пака, поставленный поясу. Нужно наружу, чтобы
    /// проверка ассетов читала ту же карту, что и игра, а не свою копию.</summary>
    public static string PackSkyFor(int band)
    {
        if (band < 0 || band >= PackSky.Length) return null;
        return PackSky[band];
    }

    /// <summary>Сколько кубов реально удалось загрузить.</summary>
    public static int LoadedCubemaps { get; private set; }

    /// <summary>Режим неба: 1 — плавно, 2 — подмена, 3 — процедурное, 0 — однотонный фон.</summary>
    public static int Mode { get; private set; }

    /// <summary>Доля второго куба в текущем материале. Для проверки перехода.</summary>
    public static float CurrentBlend { get; private set; }

    /// <summary>Куб, которого сейчас видно больше всего.
    ///
    /// Отдельное свойство, а не вычисление снаружи, потому что из него
    /// проверяется главное требование перехода: по обе стороны от границы
    /// поясов виден один и тот же куб. Раньше был виден верхний (переход
    /// дошёл до конца), теперь нижний (переход начинается заново) — если
    /// сравнить эти два числа и они разойдутся, значит на стыке будет щелчок.</summary>
    public static int DominantCube { get; private set; }

    private static Material _skybox;
    private static Texture[] _cubes;
    private static int _pairA = -1;
    private static int _pairB = -1;
    private static float _lastFollowY = float.NaN;
    private static bool _installed;

    /// <summary>Ставит небо камере и настраивает туман. Вызывается один раз на сцену.</summary>
    public static void Install(Camera cam, Color fallbackBackground)
    {
        _cubes = LoadCubes();
        LoadedCubemaps = CountCubes();
        BuildSkybox();

        if (cam != null)
        {
            if (_skybox != null)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.backgroundColor = fallbackBackground;
            }
            else
            {
                // Ни одного шейдера неба (например, сборка без встроенных
                // шейдеров) — остаётся однотонный фон, лучше, чем чёрный кадр.
                cam.clearFlags = CameraClearFlags.SolidColor;
            }
        }

        if (_skybox != null) RenderSettings.skybox = _skybox;
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;

        Light sun = Object.FindFirstObjectByType<Light>();
        if (sun != null && sun.type == LightType.Directional) RenderSettings.sun = sun;

        ApplyBand(-50f);
        _lastFollowY = -50f;
        _installed = true;
    }

    /// <summary>
    /// Сбрасывает состояние при перезапуске игровой сцены. Небо остаётся тем же
    /// материалом, поэтому здесь только сброс пары кубов: иначе новый заход
    /// начнёт показывать прошлую ступень, пока не сменится высота.
    /// </summary>
    public static void Reset()
    {
        _pairA = -1;
        _pairB = -1;
        _lastFollowY = float.NaN;
        _installed = false;
    }

    /// <summary>
    /// Держит небо и туман в актуальном состоянии. Вызывается каждый кадр
    /// из уровня: позиция игрока задаёт и картинку неба, и плотность тумана.
    /// </summary>
    public static void Follow(Vector3 playerPosition)
    {
        if (!_installed) return;

        float y = playerPosition.y;
        if (Mathf.Abs(y - _lastFollowY) < FollowStep) return;

        _lastFollowY = y;
        ApplyBand(y);
    }

    private static void BuildSkybox()
    {
        Shader blend = Shader.Find(BlendShader);
        Shader swap = Shader.Find(SwapShader);
        Shader procedural = Shader.Find(ProceduralShader);

        // Плавный переход требует всех кубов сразу: два неполных набора дали бы
        // смену картинки посреди неба вместо перехода.
        if (LoadedCubemaps == Bands.Length && blend != null)
        {
            _skybox = new Material(blend);
            _skybox.name = "SkyBlend";
            Mode = ModeBlend;
            return;
        }

        if (LoadedCubemaps > 0 && swap != null)
        {
            _skybox = new Material(swap);
            _skybox.name = "SkySwap";
            Mode = ModeSwap;
            return;
        }

        if (procedural != null)
        {
            _skybox = new Material(procedural);
            _skybox.name = "GenSkybox";
            Mode = ModeProcedural;
            return;
        }

        _skybox = null;
        Mode = ModeNone;
    }

    /// <summary>
    /// Тянет кубы из материалов пака, а не из текстур: материал уже указывает на
    /// свой куб, и копировать в Resources триста мегабайт картинок незачем.
    /// </summary>
    private static Texture[] LoadCubes()
    {
        var cubes = new Texture[Bands.Length];

        for (int i = 0; i < Bands.Length; i++)
        {
            Material src = Resources.Load<Material>("Skyboxes/" + PackSky[i]);
            if (src == null) continue;

            Texture cube = src.GetTexture(SwapTexProp);
            if (cube is Cubemap) cubes[i] = cube;
        }

        return cubes;
    }

    private static int CountCubes()
    {
        if (_cubes == null) return 0;

        int n = 0;
        for (int i = 0; i < _cubes.Length; i++)
            if (_cubes[i] != null) n++;
        return n;
    }

    private static void ApplyBand(float altitude)
    {
        int lower = BandAt(altitude);
        int upper = Mathf.Min(lower + 1, Bands.Length - 1);

        // Доля второго куба: ноль, пока не началось окно перехода, и единица на
        // границе поясов. Считается из высоты, а не из индекса, потому что один
        // и тот же индекс даёт и ноль, и единицу — в зависимости, где игрок.
        float blend = 0f;
        if (upper != lower)
        {
            float a = Bands[lower].altitude;
            float b = Bands[upper].altitude;
            float fadeStart = Mathf.Lerp(a, b, 1f - FadeFraction);
            if (altitude > fadeStart) blend = Mathf.InverseLerp(fadeStart, b, altitude);
        }

        CurrentBlend = blend;
        DominantCube = blend >= 0.5f ? upper : lower;

        // Оттенок и экспозиция едут вместе с картинкой, по той же доле: иначе
        // небо меняло бы цвет, а картинка ещё нет.
        Color tint = Color.Lerp(Bands[lower].tint, Bands[upper].tint, blend);
        float exposure = Mathf.Lerp(Bands[lower].exposure, Bands[upper].exposure, blend);

        if (_skybox != null)
        {
            switch (Mode)
            {
                case ModeBlend:
                    // Текстуры переставляем только когда сменилась пара: дёргать
                    // SetTexture каждый кадр незачем, а лишняя перестановка на
                    // той же высоте означала бы рывок картинки в середине неба.
                    if (lower != _pairA || upper != _pairB)
                    {
                        _pairA = lower;
                        _pairB = upper;
                        _skybox.SetTexture(BandTexProps[0], _cubes[lower]);
                        _skybox.SetTexture(BandTexProps[1], _cubes[upper]);
                    }

                    _skybox.SetFloat(BandTexProps[2], blend);
                    _skybox.SetFloat(BandTexProps[3], exposure);
                    _skybox.SetColor(BandTexProps[4], tint);
                    break;

                case ModeSwap:
                    // Неполный набор кубов: перехода нет, картинка меняется на
                    // границе поясов. Принято только потому, что лучше щелчок,
                    // чем пустое небо.
                    if (lower != _pairA)
                    {
                        _pairA = lower;
                        _skybox.SetTexture(SwapTexProp, FirstCube(lower));
                    }

                    _skybox.SetFloat("_Exposure", exposure);
                    _skybox.SetColor("_Tint", tint);
                    break;

                case ModeProcedural:
                    _skybox.SetColor("_SkyTint", SampleColor(altitude, 0));
                    _skybox.SetColor("_GroundColor", SampleColor(altitude, 1));
                    _skybox.SetFloat("_AtmosphereThickness", SampleScalar(altitude, 2));
                    _skybox.SetFloat("_Exposure", SampleScalar(altitude, 3));
                    _skybox.SetFloat("_SunSize", SampleScalar(altitude, 4));
                    break;
            }
        }

        RenderSettings.fogColor = SampleColor(altitude, 2);
        RenderSettings.fogDensity = SampleScalar(altitude, 1);
    }

    /// <summary>Первый попавшийся куб не выше пояса. Нужен, когда часть кубов не загрузилась.</summary>
    private static Texture FirstCube(int from)
    {
        if (_cubes == null) return null;

        for (int i = from; i < _cubes.Length; i++)
            if (_cubes[i] != null) return _cubes[i];

        for (int i = from - 1; i >= 0; i--)
            if (_cubes[i] != null) return _cubes[i];

        return null;
    }

    /// <summary>Индекс пояса по высоте: последний, у которого высота не выше заданной.</summary>
    private static int BandAt(float altitude)
    {
        int index = 0;
        for (int i = 0; i < Bands.Length; i++)
        {
            if (altitude < Bands[i].altitude) break;
            index = i;
        }
        return index;
    }

    /// <summary>Цвет пояса: 0 — небо, 1 — низ, 2 — туман.</summary>
    private static Color SampleColor(float altitude, int which)
    {
        int count = Bands.Length;
        if (altitude <= Bands[0].altitude) return BandColor(Bands[0], which);
        if (altitude >= Bands[count - 1].altitude) return BandColor(Bands[count - 1], which);

        for (int i = 0; i < count - 1; i++)
        {
            Band a = Bands[i];
            Band b = Bands[i + 1];
            if (altitude > a.altitude && altitude <= b.altitude)
            {
                float t = (altitude - a.altitude) / (b.altitude - a.altitude);
                t = t * t * (3f - 2f * t);
                return Color.Lerp(BandColor(a, which), BandColor(b, which), t);
            }
        }

        return BandColor(Bands[0], which);
    }

    private static Color BandColor(Band band, int which)
    {
        switch (which)
        {
            case 1: return band.ground;
            case 2: return band.fog;
            default: return band.tint;
        }
    }

    /// <summary>Число пояса: 1 — плотность тумана, 2 — толщина атмосферы, 3 — экспозиция, 4 — размер солнца.</summary>
    private static float SampleScalar(float altitude, int which)
    {
        int count = Bands.Length;
        if (altitude <= Bands[0].altitude) return BandScalar(Bands[0], which);
        if (altitude >= Bands[count - 1].altitude) return BandScalar(Bands[count - 1], which);

        for (int i = 0; i < count - 1; i++)
        {
            Band a = Bands[i];
            Band b = Bands[i + 1];
            if (altitude > a.altitude && altitude <= b.altitude)
            {
                float t = (altitude - a.altitude) / (b.altitude - a.altitude);
                t = t * t * (3f - 2f * t);
                return Mathf.Lerp(BandScalar(a, which), BandScalar(b, which), t);
            }
        }

        return BandScalar(Bands[0], which);
    }

    private static float BandScalar(Band band, int which)
    {
        switch (which)
        {
            case 1: return band.fogDensity;
            case 2: return band.atmosphere;
            case 3: return band.exposure;
            default: return band.sunSize;
        }
    }
}
