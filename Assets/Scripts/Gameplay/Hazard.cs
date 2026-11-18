using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Опасности на платформах. Любая из них убивает игрока касанием: смерть идёт
/// через общий <see cref="PlayerRespawn"/>, то есть игрок возвращается на последний
/// чекпоинт, ровно как при падении вниз. Отдельной системы здоровья в проекте нет,
/// и заводить её ради одного вида опасности незачем.
///
/// Меши собираются кодом: пирамидки для шипов делаются вручную из вершин, потому что
/// встроенного примитива-конуса в Unity нет, а квадратная пластина выглядит не тем.
/// </summary>
public class Hazard : MonoBehaviour
{
    public enum Kind
    {
        /// <summary>Статичные шипы. Обычное препятствие, заранее видно.</summary>
        Spikes,

        /// <summary>Вращающаяся пила. Ширше шипов, перепрыгивается только точно.</summary>
        Saw,

        /// <summary>Высокие столбы. Мешают приземлиться в центре платформы.</summary>
        Pillars
    }

    private const float SawSpin = 320f;
    private const int SpikeSides = 6;

    // Общая задержка после смерти: касание не должно срабатывать дважды за кадр.
    private static float _playerImmuneUntil;

    private static Mesh _spikeMesh;
    private static Mesh _sawBladeMesh;

    // Опасности исчезают вместе с платформами и через несколько шагов снова
    // появляются. Ключ — вид и размер с шагом 0,1 м: геометрия в тех же
    // пределах совпадает, а разные buckets не растят пул бесконечно.
    private static readonly Dictionary<int, Stack<Hazard>> _pool =
        new Dictionary<int, Stack<Hazard>>();

    private Transform _spinner;
    private bool _spent;
    private bool _pooled;
    private int _poolKey;

    /// <summary>Какой именно вид построен. Нужен наружу, чтобы тест и отладка
    /// могли проверить разнообразие, не залезая в имя объекта.</summary>
    public Kind Variety { get; private set; }

    /// <summary>
    /// Собирает опасность на поверхности платформы.
    /// </summary>
    /// <param name="footprint">Сторона занятого квадрата в метрах.</param>
    public static Hazard Create(Transform parent, Vector3 topCenter, Kind kind, float footprint)
    {
        int poolKey = PoolKey(kind, footprint);
        Hazard hazard = null;
        Stack<Hazard> stack;
        if (_pool.TryGetValue(poolKey, out stack))
        {
            while (stack.Count > 0)
            {
                hazard = stack.Pop();
                if (hazard != null) break;
                hazard = null;
            }
            if (stack.Count == 0) _pool.Remove(poolKey);
        }

        if (hazard != null)
        {
            hazard.transform.SetParent(parent, false);
            hazard.transform.position = topCenter;
            hazard.transform.rotation = Quaternion.identity;
            hazard.gameObject.SetActive(true);
            hazard._poolKey = poolKey;
            hazard._spent = false;
            hazard._pooled = false;
            hazard.Variety = kind;

            BoxCollider trigger = hazard.GetComponent<BoxCollider>();
            if (trigger != null)
            {
                trigger.size = new Vector3(footprint * 1.05f, 1.1f, footprint * 1.05f);
                trigger.center = new Vector3(0f, 0.5f, 0f);
            }
            return hazard;
        }

        GameObject go = new GameObject("Hazard " + kind);
        go.transform.SetParent(parent, false);
        go.transform.position = topCenter;

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        // Триггер чуть выше и шире видимых мешей: чтобы касание нельзя было
        // «проскочить» мимо острия из-за полупрозрачного края.
        box.size = new Vector3(footprint * 1.05f, 1.1f, footprint * 1.05f);
        box.center = new Vector3(0f, 0.5f, 0f);

        hazard = go.AddComponent<Hazard>();
        hazard._poolKey = poolKey;
        hazard.Variety = kind;
        switch (kind)
        {
            case Kind.Saw: hazard.BuildSaw(footprint); break;
            case Kind.Pillars: hazard.BuildPillars(footprint); break;
            default: hazard.BuildSpikes(footprint); break;
        }

        return hazard;
    }

    private static int PoolKey(Kind kind, float footprint)
    {
        int step = Mathf.RoundToInt(Mathf.Clamp(footprint, 0.1f, 8f) * 10f);
        return (int)kind * 1000 + step;
    }

    /// <summary>Убирает опасность в пул. Объект остаётся в выгружаемой сцене,
    /// но неактивным; ClearPool вызывается перед переходом.</summary>
    public void ReturnToPool()
    {
        if (_pooled || this == null) return;
        _pooled = true;
        StopAllCoroutines();
        _spent = false;
        gameObject.SetActive(false);

        Stack<Hazard> stack;
        if (!_pool.TryGetValue(_poolKey, out stack))
        {
            stack = new Stack<Hazard>();
            _pool[_poolKey] = stack;
        }
        stack.Push(this);
    }

    public static void ClearPool()
    {
        _pool.Clear();
    }

    // ---------------- виды ----------------

    private void BuildSpikes(float footprint)
    {
        Material mat = Surfaces.Hazard();

        // Подложка-бетонная плита, чтобы шипы читались как установленные, а не росли из воздуха.
        GameObject baseSlab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        baseSlab.name = "Base";
        baseSlab.transform.SetParent(transform, false);
        baseSlab.transform.localScale = new Vector3(footprint, 0.12f, footprint);
        baseSlab.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        StripCollider(baseSlab);
        Paint(baseSlab, mat);

        // Сетка 3x3 со сдвигом, чтобы ряд не выглядел заводским. Крайние убираем:
        // полная рамка из шипов перекрыла бы всю платформу.
        const int grid = 3;
        float step = footprint / grid;
        float spikeRadius = step * 0.3f;
        float spikeHeight = Mathf.Clamp(footprint * 0.42f, 0.42f, 0.85f);

        for (int ix = 0; ix < grid; ix++)
        {
            for (int iz = 0; iz < grid; iz++)
            {
                if (ix == 0 && iz == 0) continue;
                if (ix == grid - 1 && iz == grid - 1) continue;

                float ox = (ix - (grid - 1) * 0.5f) * step;
                float oz = (iz - (grid - 1) * 0.5f) * step;
                // Полупрозрачность шахматки: пропущенные клетки сразу видны.
                if (((ix + iz) & 1) == 0 && (ix == 1 || iz == 1)) continue;

                GameObject spike = new GameObject("Spike");
                spike.transform.SetParent(transform, false);
                spike.transform.localPosition = new Vector3(ox, 0.12f, oz);
                spike.transform.localScale = new Vector3(spikeRadius, spikeHeight, spikeRadius);
                spike.transform.localRotation = Quaternion.Euler(0f, (ix * 37 + iz * 53) % 90f, 0f);
                AttachMesh(spike, SpikeMesh(), mat);
            }
        }
    }

    private void BuildSaw(float footprint)
    {
        Material mat = Surfaces.Hazard();

        GameObject housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        housing.name = "Housing";
        housing.transform.SetParent(transform, false);
        housing.transform.localScale = new Vector3(footprint * 0.35f, 0.2f, footprint * 0.35f);
        housing.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        StripCollider(housing);
        Paint(housing, mat);

        GameObject pivot = new GameObject("Rotor");
        pivot.transform.SetParent(transform, false);
        pivot.transform.localPosition = new Vector3(0f, 0.34f, 0f);
        _spinner = pivot.transform;

        // Клиновидный диск: ровный цилиндр на прозрачном фоне читается как монета.
        GameObject blade = new GameObject("Blade");
        blade.transform.SetParent(pivot.transform, false);
        blade.transform.localScale = Vector3.one * footprint;
        AttachMesh(blade, SawBladeMesh(), mat);
    }

    private void BuildPillars(float footprint)
    {
        Material mat = Surfaces.Hazard();

        // Столбы не сплошной стеной: между ними остаётся зазор, иначе платформа
        // становится недоступной, а не опасной.
        int count = Mathf.Max(2, Mathf.RoundToInt(footprint / 0.55f));
        if (count > 4) count = 4;
        float radius = footprint * 0.09f;
        float height = Mathf.Clamp(footprint * 1.5f, 1.3f, 2.4f);
        float step = count > 1 ? footprint * 0.7f / (count - 1) : 0f;

        for (int i = 0; i < count; i++)
        {
            float oz = (i - (count - 1) * 0.5f) * step;
            GameObject pillar = new GameObject("Pillar " + i);
            pillar.transform.SetParent(transform, false);
            pillar.transform.localPosition = new Vector3(0f, height * 0.5f, oz);
            pillar.transform.localScale = new Vector3(radius, height, radius);
            AttachMesh(pillar, SpikeMesh(), mat);
        }

        GameObject baseSlab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        baseSlab.name = "Base";
        baseSlab.transform.SetParent(transform, false);
        baseSlab.transform.localScale = new Vector3(footprint, 0.1f, footprint);
        baseSlab.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        StripCollider(baseSlab);
        Paint(baseSlab, mat);
    }

    // ---------------- поведение ----------------

    private void Update()
    {
        if (_spinner == null) return;
        _spinner.Rotate(Vector3.up, SawSpin * Time.deltaTime, Space.Self);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_spent || other == null || Time.time < _playerImmuneUntil) return;

        PlayerRespawn respawn = other.GetComponentInParent<PlayerRespawn>();
        if (respawn == null) return;

        _spent = true;
        _playerImmuneUntil = Time.time + 0.35f;

        HazardBurst.Spawn(transform.position + Vector3.up * 0.35f);
        respawn.Respawn();

        // Опасность не должна срабатывать снова, пока игрок на чекпоинте.
        StartCoroutine(Rearm());
    }

    private System.Collections.IEnumerator Rearm()
    {
        yield return new WaitForSeconds(0.5f);
        _spent = false;
    }

    // ---------------- геометрия ----------------

    /// <summary>
    /// Низкополигональная пирамида. Собственный премитив, а не Quad/Cube:
    /// вершины и треугольники считаются напрямую, меш общий на весь уровень.
    /// </summary>
    private static Mesh SpikeMesh()
    {
        if (_spikeMesh != null) return _spikeMesh;

        int sides = SpikeSides;
        int count = 1 + sides + 1; // вершина, кольцо основания, центр дна
        Vector3[] verts = new Vector3[count];
        int[] tris = new int[sides * 3 * 2];

        verts[0] = new Vector3(0f, 1f, 0f);
        for (int i = 0; i < sides; i++)
        {
            float a = i / (float)sides * Mathf.PI * 2f;
            verts[1 + i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }
        verts[count - 1] = Vector3.zero;

        for (int i = 0; i < sides; i++)
        {
            int a = 1 + i;
            int b = 1 + (i + 1) % sides;

            // Порядок (вершина, b, a) даёт нормаль наружу, а не внутрь пирамиды.
            int t = i * 6;
            tris[t] = 0; tris[t + 1] = b; tris[t + 2] = a;

            // Дно нужно, иначе снизу виден просвет. Смотрит вниз, поэтому порядок обратный.
            tris[t + 3] = count - 1; tris[t + 4] = a; tris[t + 5] = b;
        }

        _spikeMesh = BuildMesh("Spike", verts, tris);
        return _spikeMesh;
    }

    /// <summary>Зубчатый диск пилы. Радиус 1 по XZ, высота 0.16 — масштабом превращается в нужный размер.</summary>
    private static Mesh SawBladeMesh()
    {
        if (_sawBladeMesh != null) return _sawBladeMesh;

        const int teeth = 12;
        int count = teeth * 4 + 2;
        Vector3[] verts = new Vector3[count];
        // На каждый зуб: 2 квада низа и верха (4 треугольника) плюс 8 треугольников
        // на боковые стенки зуба и крышку. Итого 12 треугольников, то есть 36 индексов.
        int[] tris = new int[teeth * 36];

        const float outer = 1f;
        const float inner = 0.74f;
        const float half = 0.08f;

        int v = 0;
        for (int i = 0; i < teeth; i++)
        {
            float a0 = i / (float)teeth * Mathf.PI * 2f;
            float a1 = (i + 0.42f) / (float)teeth * Mathf.PI * 2f;
            float a2 = (i + 0.58f) / (float)teeth * Mathf.PI * 2f;
            float a3 = (i + 1f) / (float)teeth * Mathf.PI * 2f;

            verts[v++] = new Vector3(Mathf.Cos(a0) * outer, -half, Mathf.Sin(a0) * outer);
            verts[v++] = new Vector3(Mathf.Cos(a1) * inner, -half, Mathf.Sin(a1) * inner);
            verts[v++] = new Vector3(Mathf.Cos(a2) * inner, -half, Mathf.Sin(a2) * inner);
            verts[v++] = new Vector3(Mathf.Cos(a3) * outer, -half, Mathf.Sin(a3) * outer);
        }
        int topCenter = v;
        verts[v++] = new Vector3(0f, half, 0f);
        int bottomCenter = v;
        verts[v++] = new Vector3(0f, -half, 0f);

        int t = 0;
        for (int i = 0; i < teeth; i++)
        {
            int a = i * 4;
            int b = ((i + 1) % teeth) * 4;
            AddQuad(tris, ref t, a + 0, a + 1, b + 1, b + 0); // низ
            AddQuad(tris, ref t, a + 3, a + 2, b + 2, b + 3); // верх
        }
        for (int i = 0; i < teeth; i++)
        {
            int a = i * 4;
            int b = ((i + 1) % teeth) * 4;
            AddTri(tris, ref t, topCenter, a + 3, a + 0);
            AddTri(tris, ref t, topCenter, a + 2, a + 3);
            AddTri(tris, ref t, topCenter, b + 0, b + 3);
            AddTri(tris, ref t, topCenter, b + 3, b + 2);
            AddTri(tris, ref t, bottomCenter, a + 0, a + 3);
            AddTri(tris, ref t, bottomCenter, a + 3, a + 2);
            AddTri(tris, ref t, bottomCenter, b + 3, b + 0);
            AddTri(tris, ref t, bottomCenter, b + 2, b + 3);
        }

        _sawBladeMesh = BuildMesh("SawBlade", verts, tris);
        return _sawBladeMesh;
    }

    private static void AddQuad(int[] tris, ref int t, int v0, int v1, int v2, int v3)
    {
        tris[t++] = v0; tris[t++] = v2; tris[t++] = v1;
        tris[t++] = v1; tris[t++] = v2; tris[t++] = v3;
    }

    private static void AddTri(int[] tris, ref int t, int v0, int v1, int v2)
    {
        tris[t++] = v0; tris[t++] = v1; tris[t++] = v2;
    }

    private static Mesh BuildMesh(string name, Vector3[] verts, int[] tris)
    {
        Mesh mesh = new Mesh();
        mesh.name = name;
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AttachMesh(GameObject go, Mesh mesh, Material mat)
    {
        MeshFilter filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        // Шипы и пилы не должны попадать в карту теней: они мелкие, а
        // отрисовка каждого кастера стоит прохода по геометрии.
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static void StripCollider(GameObject go)
    {
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
    }

    private static void Paint(GameObject go, Material mat)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}

/// <summary>
/// Короткая вспышка частиц в месте смерти. Не ParticleSystem, а несколько кубиков:
/// система частиц на смертях дольше компилируется в первый кадр, а кубики
/// создаются за один вызов и убираются через полсекунды.
/// </summary>
public class HazardBurst : MonoBehaviour
{
    private const int Count = 10;
    private const float Life = 0.45f;
    private const float Speed = 5.5f;

    private static readonly Stack<HazardBurst> _pool = new Stack<HazardBurst>();

    private Transform[] _bits;
    private Vector3[] _velocities;
    private float _age;
    private bool _pooled;

    public static void Spawn(Vector3 position)
    {
        HazardBurst burst = null;
        while (_pool.Count > 0)
        {
            burst = _pool.Pop();
            if (burst != null) break;
            burst = null;
        }

        if (burst == null)
        {
            GameObject go = new GameObject("HazardBurst");
            burst = go.AddComponent<HazardBurst>();
            burst.Populate();
        }

        burst._pooled = false;
        burst.transform.position = position;
        burst.transform.rotation = Quaternion.identity;
        burst.gameObject.SetActive(true);
        burst.ResetBits();
    }

    private void Populate()
    {
        Material mat = Surfaces.Hazard();
        _bits = new Transform[Count];
        _velocities = new Vector3[Count];

        for (int i = 0; i < Count; i++)
        {
            GameObject bit = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bit.name = "Bit " + i;
            bit.transform.SetParent(transform, false);
            Collider collider = bit.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Renderer renderer = bit.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            _bits[i] = bit.transform;
        }
    }

    private void ResetBits()
    {
        _age = 0f;
        for (int i = 0; i < _bits.Length; i++)
        {
            if (_bits[i] == null) continue;
            _bits[i].gameObject.SetActive(true);
            _bits[i].localPosition = Vector3.zero;
            _bits[i].localRotation = Quaternion.identity;
            _bits[i].localScale = Vector3.one * 0.14f;

            // Разброс по сфере, но с уклоном вверх — снег из-под ног.
            Vector3 dir = Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y) * 1.4f + 0.35f;
            _velocities[i] = dir.normalized * (Speed * Random.Range(0.6f, 1.2f));
        }
    }

    public static void ClearPool()
    {
        _pool.Clear();
    }

    private void Update()
    {
        _age += Time.deltaTime;
        float t = _age / Life;

        for (int i = 0; i < _bits.Length; i++)
        {
            if (_bits[i] == null) continue;
            _bits[i].localPosition += _velocities[i] * Time.deltaTime;
            _velocities[i] += Vector3.down * 16f * Time.deltaTime;
            float s = Mathf.Max(0f, 1f - t);
            _bits[i].localScale = Vector3.one * (0.14f * s);
        }

        if (_age >= Life)
            ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (_pooled) return;
        _pooled = true;
        gameObject.SetActive(false);
        _pool.Push(this);
    }
}
