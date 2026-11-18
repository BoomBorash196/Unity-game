using System.Collections.Generic;
using UnityEngine;

public class LevelGenerator : MonoBehaviour
{
    /// <summary>
    /// След платформы: центр и радиус описанной окружности. Хранится вместо одного
    /// Vector3, потому что платформы стали разной формы, и одной стороной больше
    /// нельзя описать, насколько далеко они разойдутся.
    /// </summary>
    private struct Footprint
    {
        public Vector3 center;
        public float radius;

        /// <summary>Половина толщины. Без неё не отличить площадки, стоящие друг
        /// над другом, от стоящих рядом, а проверка наложения без этого запрещала
        /// бы кучу совершенно безопасных мест и тратила на них попытки подбора.</summary>
        public float halfHeight;

        public Footprint(Vector3 c, float r, float h)
        {
            center = c;
            radius = r;
            halfHeight = h;
        }
    }

    /// <summary>Форма платформы. Влияет и на габариты, и на ориентацию, и на материал.</summary>
    private enum Shape
    {
        Square,
        Small,
        Wide,
        Long
    }

    public LevelData Data;

    // Ступень инициализируется сразу, а не в ResetPath: CheckpointPlatformIndex
    // спрашивает конфиг раньше, чем путь перестроен, — LevelManager сначала
    // считает стартовую платформу и только потом зовёт ResetPath.
    private StageDefinition _stage = StageCatalog.Stage(0);
    private int _stageIndex;

    /// <summary>Сид подъёма. Константой он был не из-за экономии, а потому что
    /// не придумали, чем его менять: пока сид фиксирован, трасса побайтово
    /// одинакова каждый забег и «другого уровня» просто не существует.</summary>
    public int Seed = SeedBase;

    /// <summary>Конфиг текущей ступени. Именно свойство, а не поле: сложность
    /// задаётся высотой, и если записать конфиг один раз при старте, верхние
    /// ступени получат земные габариты — и просто окажутся непроходимыми.</summary>
    public LevelConfig Cfg { get { return _stage.Cfg; } }

    public Biome Biome { get { return _stage.Biome; } }

    /// <summary>Ступень, к которой строится трасса прямо сейчас.</summary>
    public StageDefinition CurrentStage { get { return _stage; } }

    public int CurrentStageIndex { get { return _stageIndex; } }

    private const int ChunkSize = 24;
    private const int SeedBase = 13337;

    /// <summary>
    /// Шаг чекпоинтов и доля «плоских» отрезков. Общие для всего подъёма и
    /// намеренно не в LevelConfig: чекпоинты — это прогрессия и экономика,
    /// а не сложность ступени.
    ///
    /// Почему нельзя разрешить ступеням их менять. CheckpointPlatformIndex —
    /// прямая пересчёт «номер чекпоинта → номер платформы», и её результат
    /// кэшируется. Если CpGapBase зависит от высоты, то одна и та же функция
    /// даст разный ответ до и после подъёма, и ReleaseBelow начнёт уничтожать
    /// не тот кусок трассы, а SetKillY — ставить смерть по чужой высоте.
    /// </summary>
    private const int CpGapBase = 8;
    private const int FlatPercent = 40;

    /// <summary>
    /// Сколько последних площадок помним при проверке наложения.
    ///
    /// Значение заведомо с запасом, и это не лечение симптома. Курс ограничен
    /// плюс-минус 50 градусами от базового, поэтому трасса не может сложиться
    /// обратно: даже разверни её на эти сто градусов целиком, хорда между
    /// площадками в четыре шага назад выходит около 12 м, а для наложения хватает
    /// шести с половиной. Двенадцати старому значению хватало, то есть налезание
    /// через память случиться не могло, и я проверил это арифметикой, а не на глаз.
    ///
    /// Оставлено 48 намеренно: единственное, что держит ту границу, — константа
    /// MaxDeviation. Расширять её без правки этого числа нельзя: двенадцать тихо
    /// перестанет хватать, и наложение появится не сразу и не там, где его искали.
    /// Лишние записи почти ничего не стоят: площадки, разошедшиеся по высоте,
    /// отбрасываются одним сравнением, не считая расстояний.
    /// </summary>
    private const int RecentCount = 48;

    /// <summary>Дальность первой ступени от центра стартовой площадки. Считана так,
    /// чтобы следующая площадка не заезжала на стартовую ни углом, ни стороной;
    /// подробности у ветки idx == 1.</summary>
    private const float FirstStepDistance = 9.7f;
    private const float BaseHeading = 30f * Mathf.Deg2Rad;
    private const float MaxTurn = 30f * Mathf.Deg2Rad;
    private const float MaxDeviation = 50f * Mathf.Deg2Rad;
    private const int GrowK1 = 50;
    private const int GrowK2 = 100;
    private const float GrowR1 = 10f;
    private const float GrowR2 = 30f;
    private const float MaxGapPlatforms = 500000f;
    private const int CoinEvery = 7;

    /// <summary>
    /// Сколько мест пробуем поставить одну площадку.
    ///
    /// Четырёх не хватало: направление колеблется всего на ±30°, и когда сосед
    /// стоит точно по курсу, все четыре попытки попадали в него. Шесть попыток с
    /// растущей дальностью дают запас хода и по углу тоже, когда первый вариант
    /// не подошёл.
    /// </summary>
    private const int PlacementAttempts = 6;

    /// <summary>Насколько дальше пробуем с каждой попыткой. 15% от базовой
    /// дальности — примерно полметра: заметный сдвиг, но ещё не разрыв трассы.</summary>
    private const float DistanceGrow = 0.15f;

    private System.Random _rng;
    private readonly List<Footprint> _recent = new List<Footprint>();
    private readonly Dictionary<int, GameObject> _platforms = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, GameObject> _markers = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, GameObject> _coins = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, GameObject> _hazards = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, Vector3> _centers = new Dictionary<int, Vector3>();

    // Позади игрока площадки выключаются и ждут следующего шага. Компоненты и
    // коллайдер у них уже есть, поэтому повторное использование дешевле, чем
    // new GameObject + AddComponent на каждой платформе.
    private readonly Stack<GameObject> _platformPool = new Stack<GameObject>();

    private int _nextIndex;
    private int _aliveMin = -1;
    private int _aliveMax = -1;
    private Vector3 _lastCenter;
    private float _lastRadius;
    private float _lastYaw;
    private Shape _lastShape;
    private float _heading;
    private int _currentSegment;
    private int _nextCheckpointPlatform;
    private readonly List<int> _cpPrefix = new List<int>();

    public int AliveMin { get { return _aliveMin; } }
    public int AliveMax { get { return _aliveMax; } }

    /// <summary>
    /// Сколько раз подбор места не смог найти чистого варианта ни за одну из попыток
    /// и поставил наименее плохой. Ненулевое число означает, что где-то трасса
    /// оказалась теснее, чем позволяет прыжок, и площадка встала в наезд.
    ///
    /// Считается наружу специально: это единственный способ узнать извне, что
    /// подбор не справился, не разбирая вручную четыре тысячи площадок. Раньше
    /// такого счётчика не было, и провалы подбора были видны только как «выглядит
    /// не так» — то есть не видны совсем.
    /// </summary>
    public int PlacementFallbacks { get; private set; }

    public int CheckpointPlatformIndex(int checkpoint)
    {
        if (checkpoint <= 0) return 0;
        if (_cpPrefix.Count == 0) _cpPrefix.Add(0);
        while (_cpPrefix.Count <= checkpoint)
        {
            int k = _cpPrefix.Count;
            int gap = SegmentPlatforms(k);
            int prev = _cpPrefix[k - 1];
            _cpPrefix.Add(prev > int.MaxValue - gap ? int.MaxValue : prev + gap);
        }
        return _cpPrefix[checkpoint];
    }

    private int SegmentPlatforms(int cp)
    {
        if (cp <= 0) return 0;
        float gap;
        if (cp <= GrowK1)
        {
            gap = CpGapBase * Mathf.Pow(GrowR1, (cp - 1f) / (GrowK1 - 1f));
        }
        else
        {
            gap = CpGapBase * GrowR1 * Mathf.Pow(GrowR2 / GrowR1, (cp - GrowK1) / (float)(GrowK2 - GrowK1));
        }
        gap = Mathf.Min(gap, MaxGapPlatforms);
        return Mathf.Max(1, Mathf.RoundToInt(gap));
    }

    private bool IsFlatSegment(int cp)
    {
        if (cp <= 0) return false;
        return ((uint)cp * 2654435761u) % 100u < (uint)FlatPercent;
    }

    public Vector3 PositionOf(int index)
    {
        return _centers[index];
    }

    public void BuildData()
    {
        Data = new LevelData();
        Data.Root = new GameObject("Endless Level");
        Data.Root.transform.SetParent(transform, false);
    }

    public void ResetPath(int startPlatform)
    {
        _platforms.Clear();
        _markers.Clear();
        _coins.Clear();
        _centers.Clear();
        _recent.Clear();
        _aliveMin = -1;
        _aliveMax = -1;
        PlacementFallbacks = 0;
        _nextIndex = 0;
        _lastCenter = new Vector3(0f, 0.5f, 0f);
        _lastRadius = PlatformFit.CircumRadius(new Vector3(12f, 1f, 12f));
        _lastYaw = 0f;
        _lastShape = Shape.Square;
        _heading = BaseHeading;
        _currentSegment = 0;
        _nextCheckpointPlatform = 0;
        _cpPrefix.Clear();
        InitializeRng(0);
        UpdateStage(_lastCenter.y);

        while (_nextIndex < Mathf.Max(0, startPlatform))
            Step(false);
    }

    /// <summary>
    /// Пересчитывает ступень по высоте площадки. Вызывается на каждом шаге:
    /// дешевле, чем следить за сменой ступени снаружи.
    ///
    /// Считаем по высоте предыдущей площадки, а не той, что строится сейчас.
    /// Разница между ними — один шаг вверх, максимум 2,4 м, то есть на
    /// границе ступеней ошибка возможна ровно одна платформа: та, что уже
    /// взяла конфиг нижней ступени. Это ровно тот случай, когда на границе
    /// хочется «ещё чуть-чуть старой» местности, а не новой.
    /// </summary>
    private void UpdateStage(float worldY)
    {
        int index = StageCatalog.IndexAt(worldY - StageCatalog.WorldOriginY);
        if (index == _stageIndex) return;
        _stageIndex = index;
        _stage = StageCatalog.Stage(index);
    }

    public void GenerateUpTo(int untilIndex)
    {
        while (_nextIndex <= untilIndex)
            Step(true);
    }

    public void ReleaseBelow(int minIndex)
    {
        if (_aliveMin < 0) return;

        for (int i = _aliveMin; i < minIndex; i++)
        {
            if (i > _aliveMax) break;
            if (_platforms.TryGetValue(i, out GameObject go))
            {
                _platforms.Remove(i);
                if (go != null)
                {
                    go.SetActive(false);
                    _platformPool.Push(go);
                }
            }
            if (_markers.TryGetValue(i, out GameObject m))
            {
                Destroy(m);
                _markers.Remove(i);
            }
            if (_coins.TryGetValue(i, out GameObject c))
            {
                _coins.Remove(i);
                if (c != null)
                {
                    CoinPickup coin = c.GetComponent<CoinPickup>();
                    if (coin != null) coin.ReturnToPool();
                    else Destroy(c);
                }
            }
            if (_hazards.TryGetValue(i, out GameObject h))
            {
                _hazards.Remove(i);
                if (h != null)
                {
                    Hazard hazard = h.GetComponent<Hazard>();
                    if (hazard != null) hazard.ReturnToPool();
                    else Destroy(h);
                }
            }
            _centers.Remove(i);
        }
        _aliveMin = Mathf.Max(_aliveMin, minIndex);
    }

    private void Step(bool instantiate)
    {
        int idx = _nextIndex;
        _nextIndex++;

        if (idx % ChunkSize == 0)
            InitializeRng(idx / ChunkSize);

        // Ступень определяет конфиг и биом этого шага, поэтому она обязана
        // обновиться до всех обращений к Cfg — включая SegmentPlatforms ниже.
        UpdateStage(_lastCenter.y);

        bool flat = IsFlatSegment(_currentSegment);
        bool checkpointHere = idx == _nextCheckpointPlatform;

        Vector3 center;
        Vector3 size;
        float yaw = 0f;
        Shape shape = Shape.Square;

        if (idx == 0)
        {
            center = _lastCenter;
            size = new Vector3(12f, 1f, 12f);
        }
        else if (idx == 1)
        {
            // Первая ступень после стартовой площадки. Дальность считана не по
            // центру стартовой, а по её краю: стартовая квадратная, 12 на 12,
            // и по диагонали её описанная окружность заметно больше самой
            // площадки. Раньше площадка вставала в 7,6 м от центра, то есть
            // почти вплотную к углу, и её край целиком оказывался внутри
            // стартовой на полтора с лишним метра. По кромке прыжок от неё
            // короткий, игрок по стартовой идёт пешком.
            //
            // Требуется 0,866 * d >= 6 + s/2 + зазор, откуда при s = 3,6
            // получается 9,47 м. С запасом берём 9,7.
            float dy = flat ? 0f : Mathf.Lerp(1.0f, 1.9f, NextFloat());
            _heading = BaseHeading;
            center = new Vector3(
                _lastCenter.x + Mathf.Cos(BaseHeading) * FirstStepDistance,
                _lastCenter.y + dy,
                _lastCenter.z + Mathf.Sin(BaseHeading) * FirstStepDistance);
            float s = Mathf.Lerp(Cfg.MinSize, Cfg.MaxSize, NextFloat());
            size = new Vector3(s, Cfg.Thickness, s);
        }
        else
        {
            // Форму выбираем до подбора позиции: от неё зависит радиус, а значит
            // и минимальная дистанция до соседей. Форма влияет на достижимость,
            // поэтому её нельзя менять в последний момент, когда позиция уже готова.
            shape = PickShape();
            float radius = RadiusOf(shape, out size, out yaw);

            float halfHeight = size.y * 0.5f;
            float dist = Mathf.Lerp(Cfg.MinDx, Cfg.MaxDx, NextFloat());
            float dy = flat ? 0f : Mathf.Lerp(Cfg.MinDy, Cfg.MaxDy, NextFloat());

            _heading = BaseHeading + Mathf.Lerp(Unwind(_heading - BaseHeading), 0f, 0.12f);

            // Дальность, на которой новая площадка гарантированно разойдётся с
            // предыдущей, и предел, дальше которого прыгнуть не получится.
            // Второй важнее первого: он зависит от dy, и при большой высоте шага
            // он меньше простой суммы радиусов. Ставить дальше нельзя — получится
            // стена, на которую не допрыгнуть.
            float needPrev = PlatformFit.RequiredSeparation(radius, _lastRadius, Cfg.ShapeGap);
            float ceiling = PlatformFit.SafeReachAt(dy);

            Vector3 best = _lastCenter;
            float bestClearance = float.NegativeInfinity;
            float bestHeading = _heading;

            // Присваиваем сразу, а не только внутри цикла. Пока ноль попыток,
            // переменная осталась бы неприсвоенной, и это не педантизм
            // компилятора: площадка обязана где-то стоять даже в случае,
            // которого сегодня не бывает. Отдельный флаг постановки честнее
            // проверки на bestClearance — тот верен лишь потому, что мы знаем,
            // что цикл прерывается ровно на первом чистом кандидате.
            bool placed = false;
            center = _lastCenter;

            for (int attempt = 0; attempt < PlacementAttempts; attempt++)
            {
                // Дальность растёт от родной для ступени к гарантированно
                // достаточной. Раньше двигалось только направление, и на прямом
                // участке, где сосед стоит точно по курсу, все попытки
                // проваливались, а провалившийся вариант всё равно ставился.
                float tryDist = Mathf.Max(dist, needPrev) * (1f + attempt * DistanceGrow);
                if (tryDist > ceiling)
                    tryDist = ceiling;

                _heading += (NextFloat() * 2f - 1f) * MaxTurn;
                _heading = BaseHeading + Mathf.Clamp(Unwind(_heading - BaseHeading), -MaxDeviation, MaxDeviation);

                Vector3 dir = new Vector3(Mathf.Cos(_heading), 0f, Mathf.Sin(_heading));
                Vector3 cand = _lastCenter + dir * tryDist;
                cand.y = _lastCenter.y + dy;

                float clearance = Clearance(cand, radius, halfHeight);
                if (clearance > bestClearance)
                {
                    bestClearance = clearance;
                    best = cand;
                    bestHeading = _heading;
                }

                if (clearance >= 0f)
                {
                    center = cand;
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                // Ни одна попытка не вышла чистой. Ставим наименее плохую из
                // найденных и возвращаем её курс. Раньше здесь стоял последний
                // кандидат, то есть заведомо пересекающийся, и наложение было
                // не редким случаем, а штатным исходом подбора.
                center = best;
                _heading = bestHeading;
                PlacementFallbacks++;
            }
        }

        // Доску разворачиваем вдоль курса, который сложился при подборе места,
        // а не того, что был до него. Подбор курс уточняет, и доска, поставленная
        // под старый угол, лежала бы боком к собственному направлению движения.
        if (shape == Shape.Long)
            yaw = _heading * Mathf.Rad2Deg;

        int checkpointIndex = checkpointHere ? _currentSegment : -1;
        if (checkpointHere)
        {
            _currentSegment++;
            _nextCheckpointPlatform = idx + SegmentPlatforms(_currentSegment);
        }

        _lastCenter = center;
        _lastRadius = PlatformFit.CircumRadius(size);
        _lastYaw = yaw;
        _lastShape = shape;
        if (_recent.Count >= RecentCount)
            _recent.RemoveAt(0);
        _recent.Add(new Footprint(center, _lastRadius, size.y * 0.5f));

        if (instantiate)
            Materialize(idx, center, size, yaw, shape, checkpointIndex);
    }

    /// <summary>
    /// Форма платформы по весам из конфига. Формы чередуются: после двух одинаковых
    /// вес следующей урезаем вдвое, иначе трасса читается как полоса.
    /// </summary>
    private Shape PickShape()
    {
        int wSquare = Mathf.Max(0, Cfg.SquareWeight);
        int wSmall = Mathf.Max(0, Cfg.SmallWeight);
        int wWide = Mathf.Max(0, Cfg.WideWeight);
        int wLong = Mathf.Max(0, Cfg.LongWeight);
        if (wSquare + wSmall + wWide + wLong <= 0) return Shape.Square;

        if (_lastShape == Shape.Square) wSquare /= 2;
        if (_lastShape == Shape.Small) wSmall /= 2;
        if (_lastShape == Shape.Wide) wWide /= 2;
        if (_lastShape == Shape.Long) wLong /= 2;

        // Сумму считаем заново, уже после деления. Иначе розыгрыш шёл бы по
        // старому числу, а вычитались бы урезанные веса, и всё «лишнее» уезжало
        // в последнюю по счёту форму: длинных досок выходило 40% вместо 14%.
        int total = wSquare + wSmall + wWide + wLong;
        if (total <= 0) return Shape.Square;

        int roll = _rng.Next(total);
        if ((roll -= wSquare) < 0) return Shape.Square;
        if ((roll -= wSmall) < 0) return Shape.Small;
        if ((roll -= wWide) < 0) return Shape.Wide;
        return Shape.Long;
    }

    /// <summary>
    /// Габариты, поворот и описанный радиус для выбранной формы.
    /// Длинная доска всегда разворачивается вдоль текущего направления движения:
    /// поперёк она превратилась бы в почти непроходимую нитку.
    /// </summary>
    private float RadiusOf(Shape shape, out Vector3 size, out float yaw)
    {
        yaw = 0f;
        float thickness = Cfg.Thickness;

        switch (shape)
        {
            case Shape.Small:
                // Разброс внутри диапазона, иначе все маленькие платформы
                // были бы одного размера и читались как клоны.
                size = new Vector3(Cfg.SmallSize, thickness, Cfg.SmallSize * Mathf.Lerp(0.9f, 1.25f, NextFloat()));
                break;

            case Shape.Wide:
            {
                float w = Mathf.Lerp(Cfg.WideSize * 0.8f, Cfg.WideSize, NextFloat());
                size = new Vector3(w, thickness, Mathf.Lerp(Cfg.MinSize, Cfg.MaxSize + 0.4f, NextFloat()));
                yaw = Mathf.Round(NextFloat()) * 90f;
                break;
            }

            case Shape.Long:
                size = new Vector3(Cfg.LongLength, thickness, Cfg.LongWidth);
                // Угол не задаём: доска разворачивается вдоль курса, который
                // сложится при подборе места, и считается уже после подбора.
                break;

            default:
            {
                float s = Mathf.Lerp(Cfg.MinSize, Cfg.MaxSize, NextFloat());
                size = new Vector3(s, thickness, s);
                // Квадрат неоднородно доворачиваем: так путь выглядит живым,
                // и площадка не всегда ориентирована ровно по осям.
                yaw = Mathf.Round(NextFloat() * 4f) * 45f;
                break;
            }
        }

        return PlatformFit.CircumRadius(size);
    }

    /// <summary>
    /// Площадка из процедурного меша, а не примитива-куба. Коллайдер — тот же
    /// куб единичного размера: его масштабирует localScale, как делал и куб
    /// примитива, так что габариты, подбор места и проверки наложений не
    /// меняются. Меш вписан в коробку коллайдера и нигде её не превышает.
    ///
    /// Силуэт сбоку берётся от вида текстуры: у травы и снега — обрыв плавучего
    /// острова, у камня — шпиль или терраса, у досок и листов — ровная плита.
    /// </summary>
    private GameObject AcquirePlatform(Shape shape, Textures.Kind kind, int idx)
    {
        GameObject go = null;
        while (_platformPool.Count > 0)
        {
            go = _platformPool.Pop();
            if (go != null) break;
            go = null;
        }

        if (go == null)
        {
            go = new GameObject("Platform");
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            go.AddComponent<BoxCollider>();
            go.transform.SetParent(Data.Root.transform, false);
        }
        else
        {
            go.SetActive(true);
        }

        int profile = PlatformMesh.Pick((int)shape, idx, Seed);
        int style = PlatformMesh.PickStyle(kind, idx, Seed);
        go.GetComponent<MeshFilter>().sharedMesh = PlatformMesh.Get(profile, style);
        return go;
    }

    private void Materialize(int idx, Vector3 center, Vector3 size, float yaw, Shape shape, int checkpointIndex)
    {
        // Вид текстуры разыгрывается до постройки площадки: от него зависит и
        // силуэт. Обращений к _rng при этом ровно столько же и в том же порядке,
        // что и раньше, — один вид на площадку, — поэтому трасса не сдвигается.
        Textures.Kind kind = PlatformKind(shape);

        GameObject go = AcquirePlatform(shape, kind, idx);
        go.name = "Platform " + idx;
        go.transform.SetParent(Data.Root.transform, false);

        go.transform.position = center;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = size;
        Collider col = go.GetComponent<Collider>();
        if (col != null)
            col.enabled = true;
        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            // Два материала в порядке подмешей PlatformMesh: крышка и борт с
            // низом. Оба берутся из общего кэша Surfaces, иначе на длинной
            // трассе накапливаются сотни копий.
            r.sharedMaterials = Surfaces.PlatformPair(kind, PlatformTiling(size));
        }

        _platforms[idx] = go;
        _centers[idx] = center;
        if (_aliveMin < 0)
            _aliveMin = idx;
        _aliveMax = idx;

        float thickness = idx == 0 ? 1f : Cfg.Thickness;
        if (checkpointIndex >= 0)
            CreateCheckpoint(checkpointIndex, center, thickness, idx);
        else
        {
            if (idx > 0 && idx % CoinEvery == 0)
                CreateCoin(idx, center, thickness);
            MaybeCreateHazard(idx, center, size, thickness, shape);
        }
    }

    /// <summary>
    /// Ставит опасность на площадку, если она туда помещается.
    /// Опасность всегда в середине платформы и занимает не больше 42% меньшей
    /// стороны: по краям остаются реальные места для приземления, иначе
    /// платформа перестаёт быть сложной и становится непроходимой.
    /// </summary>
    private void MaybeCreateHazard(int idx, Vector3 center, Vector3 size, float thickness, Shape shape)
    {
        if (idx == 0) return;

        float minSide = Mathf.Min(size.x, size.z);
        if (minSide < Cfg.MinHazardPlatform) return;

        float levelHeight = center.y - StageCatalog.WorldOriginY;
        if (!ShouldAddHazard(levelHeight)) return;

        float footprint = Mathf.Min(1.05f, minSide * 0.42f);
        if (footprint < 0.4f) return;

        Vector3 top = center + Vector3.up * (thickness * 0.5f + 0.02f);
        Hazard hazard = Hazard.Create(Data.Root.transform, top, PickHazardKind(), footprint);
        hazard.name = "Hazard " + idx;
        _hazards[idx] = hazard.gameObject;
    }

    private void CreateCoin(int idx, Vector3 center, float thickness)
    {
        CoinPickup coin = CoinPickup.Create(Data.Root.transform, center + Vector3.up * (1.25f + thickness * 0.5f));
        coin.name = "Coin " + idx;
        _coins[idx] = coin.gameObject;
    }

    private void CreateCheckpoint(int checkpointIndex, Vector3 center, float thickness, int platformIndex)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "Checkpoint " + checkpointIndex;
        marker.transform.SetParent(Data.Root.transform, true);
        marker.transform.position = center + Vector3.up * (0.75f + thickness * 0.4f);
        marker.transform.localScale = Vector3.one * 0.9f;
        Collider markerCollider = marker.GetComponent<Collider>();
        if (markerCollider != null)
            markerCollider.isTrigger = true;
        Renderer mr = marker.GetComponent<Renderer>();
        if (mr != null)
            mr.material = Palette.Checkpoint();

        GameObject probe = new GameObject("Probe");
        SphereCollider sc = probe.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 1.1f;
        probe.transform.SetParent(marker.transform, false);
        probe.transform.localPosition = Vector3.zero;

        Checkpoint cp = probe.AddComponent<Checkpoint>();
        cp.Init(checkpointIndex, center.y + 1f);
        cp.SetCollectable(false);

        Data.Checkpoints[checkpointIndex] = cp;
        _markers[platformIndex] = marker;
    }

    private void InitializeRng(int chunk)
    {
        _rng = new System.Random(Seed + chunk * 7919);
    }

    private float NextFloat()
    {
        return (float)_rng.NextDouble();
    }

    private static float Unwind(float angle)
    {
        while (angle > Mathf.PI)
            angle -= Mathf.PI * 2f;
        while (angle < -Mathf.PI)
            angle += Mathf.PI * 2f;
        return angle;
    }

    /// <summary>
    /// Зазор новой площадки до всех запомненных соседей, в метрах.
    ///
    /// Положительное число — место есть, и это же число и есть запас.
    /// Отрицательное — насколько глубоко площадка залезла в чужую.
    /// <see cref="float.PositiveInfinity"/> — рядом никого нет.
    ///
    /// Проверяется трёхмерное расстояние, а не только XZ. У всех площадок одна
    /// толщина, и если центры разошлись по высоте сильнее, чем толщины вместе
    /// с зазором, они физически не могут соприкоснуться ни при какой близости
    /// проекций. Такие пары отбрасываются одним сравнением — и, что важнее,
    /// не съедают попытки подбора места, которые на обычном шаге вверх тратятся
    /// впустую: при dy не меньше 0,9 м половина толщины с зазором уже пройдена.
    ///
    /// Формула требования симметрична относительно обеих площадок. Прежняя
    /// брала половину суммы радиусов и прибавляла радиус соседа, то есть
    /// требовала radius*0.5 + other*1.5 вместо radius + other.
    /// </summary>
    private float Clearance(Vector3 center, float radius, float halfHeight)
    {
        float worst = float.PositiveInfinity;

        for (int i = 0; i < _recent.Count; i++)
        {
            Footprint other = _recent[i];
            float gap = PlatformFit.PairClearance(
                center, radius, halfHeight,
                other.center, other.radius, other.halfHeight,
                Cfg.ShapeGap);

            if (gap < worst) worst = gap;
        }

        return worst;
    }

    /// <summary>
    /// Насколько площадка может не повезти. Порог и разгон считаются по высоте
    /// над стартом, а не по номеру платформы: номер на разных ступенях значит
    /// разное, и «34-я платформа» на Раю давно не разгон.
    /// </summary>
    private bool ShouldAddHazard(float levelHeight)
    {
        if (levelHeight < Cfg.HazardStartHeight) return false;

        float from = Cfg.HazardStartHeight;
        float t = Mathf.InverseLerp(from, from + Cfg.HazardRampHeight, levelHeight);
        float chance = Mathf.Lerp(Cfg.HazardChance, Cfg.HazardChanceMax, t);
        return NextFloat() < chance;
    }

    /// <summary>Вид опасности — тоже по весам, чтобы не выпадали только шипы.</summary>
    private Hazard.Kind PickHazardKind()
    {
        int roll = _rng.Next(100);
        if (roll < 45) return Hazard.Kind.Spikes;
        if (roll < 80) return Hazard.Kind.Saw;
        return Hazard.Kind.Pillars;
    }

    /// <summary>
    /// Вид текстуры площадки. Доску оставляем деревом в любом биоме: её узнают
    /// по силуэту — длинная и узкая, — и если на гребне покрасить её снегом,
    /// паркурный элемент перестанет читаться: игрок не поймёт, что это доска,
    /// а не площадка.
    /// </summary>
    private Textures.Kind PlatformKind(Shape shape)
    {
        return shape == Shape.Long ? Textures.Kind.Wood : KindByWeights();
    }

    /// <summary>Тайлинг по большей стороне: примерно одна текстура на полтора метра.</summary>
    private static float PlatformTiling(Vector3 size)
    {
        float longest = Mathf.Max(size.x, size.z);
        return Mathf.Clamp(longest / 3.6f, 1f, 4f);
    }

    /// <summary>Вид площадки по весам биома ступени.</summary>
    private Textures.Kind KindByWeights()
    {
        Biome b = Biome;
        int total = b.Total;
        if (total <= 0) return Textures.Kind.Rock;

        // Порядок вычитания совпадает с порядком полей в Biome. Сумма берётся
        // до вычитаний, а не после: иначе весь «лишний» вес уехал бы в последний
        // по счёту вид — ровно та ошибка, что была с весами форм платформ.
        int roll = _rng.Next(total);
        if ((roll -= b.Grass) < 0) return Textures.Kind.Grass;
        if ((roll -= b.Wood) < 0) return Textures.Kind.Wood;
        if ((roll -= b.Rock) < 0) return Textures.Kind.Rock;
        if ((roll -= b.Metal) < 0) return Textures.Kind.Metal;
        if ((roll -= b.Snow) < 0) return Textures.Kind.Snow;
        return Textures.Kind.Crate;
    }
}
