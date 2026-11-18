using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Процедурная анимация без клипов: кости ищутся по именам (Hips/Spine/LeftArm/...),
/// фаза бега идёт от пройденного расстояния, чтобы ноги не скользили.
/// ПонимаетMixamo (mixamorig:), Blender (DEF-upper_arm.L) и 3ds Max (Bip01) префиксы.
/// Если костей нет — модель просто покачивается, как будто парит.
/// Если в модели есть Animator с контроллером — анимации отдаются ему.
/// </summary>
public class SkinAnimator : MonoBehaviour
{
    public float StrideLength = 3.2f;
    public float ArmSwing = 46f;
    public float LegSwing = 34f;
    public float BlendSpeed = 7f;
    public float SquashAmount = 0.26f;
    public float SquashTime = 0.18f;

    private PlayerController _player;
    private Transform _model;
    private SkinDefinition _skin;

    private Transform _hips, _spine, _chest, _head;
    private Transform _armL, _foreL, _armR, _foreR;
    private Transform _legL, _shinL, _legR, _shinR;

    // Модель без скелета: анимируем целые меши-части относительно друг друга.
    private Transform _partBody, _partHead, _partWing;
    private readonly Dictionary<Transform, Quaternion> _partRestRotation = new Dictionary<Transform, Quaternion>(4);
    private readonly Dictionary<Transform, Vector3> _partRestPosition = new Dictionary<Transform, Vector3>(4);
    private bool _partsReady;

    private readonly Dictionary<string, Transform> _bones = new Dictionary<string, Transform>(64);
    private readonly List<Transform> _posed = new List<Transform>(16);
    private readonly Dictionary<Transform, Quaternion> _restRotation = new Dictionary<Transform, Quaternion>(16);
    private readonly Dictionary<Transform, Vector3> _restPosition = new Dictionary<Transform, Vector3>(16);

    private Vector3 _modelScale = Vector3.one;
    private Vector3 _modelPosition;
    private Quaternion _modelRotation = Quaternion.identity;

    /// <summary>Сколько метров в одной единице модели — нужно, чтобы смещения костей не зависели от масштаба FBX.</summary>
    private float _unit = 1f;

    /// <summary>Крутить кости в системе модели (см. SkinDefinition.ModelSpaceAnimation).</summary>
    private bool _modelSpace;

    /// <summary>Поза родителя каждой кости в системе модели на момент покоя. Нужна для model-space вращений.</summary>
    private readonly Dictionary<Transform, Quaternion> _parentModelRotation = new Dictionary<Transform, Quaternion>(16);

    private float _cycle;
    private float _runBlend;
    private float _airBlend;
    private float _wallBlend;
    private float _jumpTimer;
    private float _landTimer;
    private float _landImpact;
    private float _time;
    private bool _ready;

    // Счётчик кадров анимации и максимальный угол отклонения от позы покоя.
    // Это не «ожидание работы», а измерение: показывает, что реально происходит с костями.
    private int _frames;
    private float _maxAngle;

    public bool DrivenByAnimator { get; private set; }
    public bool HasBones { get { return _hips != null || _head != null; } }
    public SkinDefinition Skin { get { return _skin; } }

    public void Bind(PlayerController player, Transform model, SkinDefinition skin)
    {
        if (_player != null)
        {
            _player.Jumped -= OnJumped;
            _player.Landed -= OnLanded;
        }

        _player = player;
        _model = model;
        _skin = skin;
        _modelSpace = skin != null && skin.ModelSpaceAnimation;

        if (_model != null)
        {
            _modelScale = _model.localScale;
            _modelPosition = _model.localPosition;
            _modelRotation = _model.localRotation;
            _unit = 1f / Mathf.Max(0.0001f, Mathf.Abs(_modelScale.x));
        }

        CacheBones();
        DetectAnimator();
        CacheParts();
        if (_player != null)
        {
            _player.Jumped += OnJumped;
            _player.Landed += OnLanded;
        }
    }

    private void OnDestroy()
    {
        if (_player == null) return;
        _player.Jumped -= OnJumped;
        _player.Landed -= OnLanded;
    }

    private void OnJumped()
    {
        _jumpTimer = SquashTime;
    }

    private void OnLanded(float impact)
    {
        _landTimer = SquashTime;
        _landImpact = Mathf.Clamp01(impact);
    }

    private void LateUpdate()
    {
        float delta = Time.deltaTime;
        if (delta <= 0f || _model == null) return;

        _time += delta;
        _frames++;

        float speed01 = 0f;
        float vertical = 0f;
        bool grounded = true;
        bool rising = false;
        bool wall = false;
        Vector3 wallNormal = Vector3.zero;

        if (_player != null)
        {
            speed01 = _player.PlanarSpeed01;
            vertical = _player.VerticalVelocity;
            grounded = _player.IsGrounded;
            rising = vertical > 0.5f;
            wall = _player.IsWallSliding;
            wallNormal = _player.WallNormal;
        }

        _cycle += (speed01 * PlayerPhysics.RunSpeed * delta) / Mathf.Max(0.2f, StrideLength);
        if (_cycle > 1f)
            _cycle -= Mathf.Floor(_cycle);

        _jumpTimer = Mathf.Max(0f, _jumpTimer - delta);
        _landTimer = Mathf.Max(0f, _landTimer - delta);

        _runBlend = Mathf.MoveTowards(_runBlend, speed01, delta * BlendSpeed);
        _airBlend = Mathf.MoveTowards(_airBlend, grounded ? 0f : 1f, delta * BlendSpeed * 1.4f);
        _wallBlend = Mathf.MoveTowards(_wallBlend, wall ? 1f : 0f, delta * 6f);

        float phase = _cycle * Mathf.PI * 2f;
        float swing = Mathf.Sin(phase);
        float bob = Mathf.Cos(phase * 2f);

        AnimateRoot(speed01, grounded, rising, wall, wallNormal, bob);

        if (DrivenByAnimator) return;

        if (_ready)
            AnimateBones(swing, bob, speed01, rising);
        else
            AnimateParts(swing, bob, rising);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        MeasureDeviation();
#endif
    }

    /// <summary>
    /// Замеряет, насколько части или кости ушли от позы покоя. Это честная
    /// телеметрия: если анимация не крутится, здесь будет 0, а не «вроде двигается».
    /// </summary>
    private void MeasureDeviation()
    {
        float max = 0f;

        for (int i = 0; i < _posed.Count; i++)
        {
            Transform bone = _posed[i];
            if (bone == null) continue;
            Quaternion rest;
            if (!_restRotation.TryGetValue(bone, out rest)) continue;
            float angle = Quaternion.Angle(rest, bone.localRotation);
            if (angle > max) max = angle;
        }

        foreach (KeyValuePair<Transform, Quaternion> pair in _partRestRotation)
        {
            if (pair.Key == null) continue;
            float angle = Quaternion.Angle(pair.Value, pair.Key.localRotation);
            if (angle > max) max = angle;
        }

        _maxAngle = max;
    }

    /// <summary>
    /// Строка состояния для отладочного оверлея. Показывает, что аниматор жив,
    /// что он нашёл (кости или части) и на какой угол реально уехала поза.
    /// </summary>
    public string AnimationStatus(PlayerController player)
    {
        string who = _skin != null ? _skin.Id : "?";
        string mode = DrivenByAnimator ? "свой Animator" : (_ready ? "кости" : "части мешей");
        int driven = _posed.Count;
        if (!DrivenByAnimator && !_ready) driven = _partRestRotation.Count;

        string move = player != null
            ? Mathf.RoundToInt(player.PlanarSpeed01 * 100f) + "%"
            : "?";

        return "АНИМАЦИЯ " + who + " · " + mode + " (" + driven + ") · кадров " + _frames +
               " · бег " + move +
               " · воздух " + Mathf.RoundToInt(_airBlend * 100f) + "%" +
               " · отклонение " + _maxAngle.ToString("0.0") + "°";
    }

    private void AnimateRoot(float speed01, bool grounded, bool rising, bool wall, Vector3 wallNormal, float bob)
    {
        float stretch = _jumpTimer > 0f ? SquashAmount * 0.6f * (_jumpTimer / SquashTime) : 0f;
        float squash = _landTimer > 0f ? _landImpact * SquashAmount * (_landTimer / SquashTime) : 0f;
        float amount = stretch - squash;

        Vector3 scale = _modelScale;
        scale.x = _modelScale.x * (1f + amount * 0.45f);
        scale.z = _modelScale.z * (1f + amount * 0.45f);
        scale.y = _modelScale.y * (1f - amount);
        _model.localScale = scale;

        float lean = speed01 * 7f + (grounded ? 0f : (rising ? -5f : 6f));
        float roll = 0f;
        Vector3 offset = _modelPosition;

        if (wall && wallNormal.sqrMagnitude > 0.0001f)
        {
            Vector3 local = transform.InverseTransformDirection(wallNormal);
            roll = -local.x * 20f * _wallBlend;
            lean += 8f * _wallBlend;
            offset += local * 0.05f * _wallBlend;
        }

        if (DrivenByAnimator || !_ready)
        {
            float hover = Mathf.Sin(_time * 1.9f) * (1f - _airBlend * 0.6f);
            offset.y += Mathf.Sin(_time * 1.6f) * 0.006f * (1f - _airBlend);
            offset.y += hover * 0.03f;
            roll += hover * 2.5f;

            if (DrivenByAnimator)
            {
                // Свой Animator уже рисует всё сам — только лёгкий отскок на бегу.
                offset.y += Mathf.Abs(bob) * 0.035f * _runBlend;
            }
            else
            {
                // Без скелета: корень только парит, а бег и прыжок разбирают AnimateParts.
                // Отскок на бегу делает часть тела, иначе было бы два прыжка сразу.
                lean += Mathf.Sin(_time * 2.4f) * 1.5f;
            }

            _model.localPosition = offset;
        }

        _model.localRotation = _modelRotation * Quaternion.Euler(lean, 0f, roll);
    }

    private void AnimateBones(float swing, float bob, float speed01, bool rising)
    {
        float run = _runBlend;
        float air = _airBlend;
        float wall = _wallBlend;

        float breath = Mathf.Sin(_time * 1.7f) * 1.5f;
        float sway = Mathf.Sin(_time * 0.9f) * 2.5f;

        float runLegLx = swing * LegSwing;
        float runLegRx = -swing * LegSwing;
        float runShinLx = Mathf.Max(0f, -swing) * 55f;
        float runShinRx = Mathf.Max(0f, swing) * 55f;
        float runArmLx = -swing * ArmSwing;
        float runArmRx = swing * ArmSwing;
        float runForeLx = 22f + swing * 14f;
        float runForeRx = 22f - swing * 14f;
        float runHipsRoll = -swing * 7f;
        float runChestTwist = swing * 8f;
        float runSpine = 6f;

        float airLegLx = rising ? -34f : 16f;
        float airLegRx = rising ? 8f : -10f;
        float airShinLx = rising ? 64f : 30f;
        float airShinRx = rising ? 20f : 14f;
        float airArmLx = rising ? -132f : -48f;
        float airArmRx = rising ? -100f : -32f;
        float airForeLx = rising ? 34f : 20f;
        float airForeRx = rising ? 28f : 18f;
        float airSpine = rising ? -7f : 9f;
        float airHead = rising ? -10f : 16f;

        float wallArmX = -145f;
        float wallArmZ = 20f;
        float wallLegX = 26f;
        float wallShinX = 55f;
        float wallSpine = 10f;

        float legLx = Mathf.Lerp(Blend(0f, runLegLx, airLegLx, run, air), wallLegX, wall);
        float legRx = Mathf.Lerp(Blend(0f, runLegRx, airLegRx, run, air), wallLegX * 0.4f, wall);
        float shinLx = Mathf.Lerp(Blend(2f, runShinLx, airShinLx, run, air), wallShinX, wall);
        float shinRx = Mathf.Lerp(Blend(2f, runShinRx, airShinRx, run, air), wallShinX * 0.5f, wall);

        float armLx = Mathf.Lerp(Blend(0f, runArmLx, airArmLx, run, air), wallArmX, wall);
        float armRx = Mathf.Lerp(Blend(0f, runArmRx, airArmRx, run, air), wallArmX * 0.75f, wall);
        float armLz = Mathf.Lerp(Blend(6f, 4f, 14f, run, air), wallArmZ, wall);
        float armRz = Mathf.Lerp(Blend(-6f, -4f, -14f, run, air), -wallArmZ, wall);

        float hipsX = Mathf.Lerp(Blend(0f, runSpine * 0.35f, airSpine * 0.4f, run, air), wallSpine * 0.5f, wall);
        float hipsY = Blend(sway * 0.4f, runChestTwist * 0.5f, 0f, run, air);
        float hipsZ = Mathf.Lerp(Blend(0f, runHipsRoll, 0f, run, air), -12f, wall);
        float hipsYOffset = Blend(breath * 0.004f, Mathf.Abs(bob) * 0.05f - 0.03f, 0.02f, run, air);

        SetRot(_hips, hipsX, hipsY, hipsZ);
        SetPos(_hips, hipsYOffset);
        SetRot(_spine, Blend(breath * 0.4f, runSpine * 0.5f, airSpine * 0.6f, run, air), 0f, 0f);
        SetRot(_chest, Blend(breath * 0.6f, runSpine * 0.3f, airSpine * 0.4f, run, air), Blend(0f, runChestTwist, 0f, run, air), 0f);
        SetRot(_head, Blend(breath * 0.4f, -runSpine * 0.5f, airHead, run, air), Blend(sway, -runChestTwist * 0.6f, 0f, run, air), 0f);

        SetRot(_armL, armLx, 0f, armLz);
        SetRot(_armR, armRx, 0f, armRz);
        SetRot(_foreL, Blend(8f, runForeLx, airForeLx, run, air), 0f, 0f);
        SetRot(_foreR, Blend(8f, runForeRx, airForeRx, run, air), 0f, 0f);

        SetRot(_legL, legLx, 0f, 0f);
        SetRot(_legR, legRx, 0f, 0f);
        SetRot(_shinL, shinLx, 0f, 0f);
        SetRot(_shinR, shinRx, 0f, 0f);
    }

    private static float Blend(float idle, float run, float air, float runBlend, float airBlend)
    {
        return Mathf.Lerp(Mathf.Lerp(idle, run, runBlend), air, airBlend);
    }

    // ---------------- модель без скелета ----------------

    /// <summary>
    /// Ищет в модели без скелета целые меши, которые можно двигать: крылья, голову, тело.
    /// Опирается на форму габаритов, а не на имена — в импортированных FBX имена
    /// обычно «Cube», «Cube.001» и ничего не говорят.
    /// </summary>
    private void CacheParts()
    {
        _partRestRotation.Clear();
        _partRestPosition.Clear();
        _partBody = null;
        _partHead = null;
        _partWing = null;
        _partsReady = false;

        // Со скелетом части не нужны: там всё уже есть кости.
        if (_model == null || _ready || DrivenByAnimator) return;

        Transform bestWing = null;
        float widest = 0f;
        float secondWidest = 0f;
        Transform widestPart = null;

        // Собираем кандидатов и ищем самую широкую часть.
        for (int i = 0; i < _model.childCount; i++)
        {
            Transform child = _model.GetChild(i);
            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer == null) continue;

            Vector3 size, center;
            if (!TryGetLocalBox(renderer, out size, out center)) continue;

            float width = size.x;
            if (width > widest)
            {
                secondWidest = widest;
                widest = width;
                widestPart = child;
            }
            else if (width > secondWidest)
            {
                secondWidest = width;
            }
        }

        // Крылья — это часть, которая заметно шире всех остальных. Сравниваем ширины
        // между собой, а не с собственной высотой: у импортированных мешей масштаб
        // произвольный, и проверка «шире, чем выше» принимала корпус за крыло.
        if (widestPart != null && (secondWidest <= 0.0001f || widest > secondWidest * 1.5f))
            bestWing = widestPart;

        Transform bestHead = null;
        float bestHeadTop = float.NegativeInfinity;
        Transform bestBody = null;
        float bestBodyHeight = 0f;

        for (int i = 0; i < _model.childCount; i++)
        {
            Transform child = _model.GetChild(i);
            if (child == bestWing) continue;

            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer == null) continue;

            Vector3 size, center;
            if (!TryGetLocalBox(renderer, out size, out center)) continue;

            if (size.y > bestBodyHeight)
            {
                bestBody = child;
                bestBodyHeight = size.y;
            }

            // Габариты уже в системе модели, вместе со смещением части.
            float top = center.y + size.y * 0.5f;
            if (top > bestHeadTop)
            {
                bestHead = child;
                bestHeadTop = top;
            }
        }

        // Голова должна быть заметно меньше тела, иначе это просто торс.
        if (bestHead != null && bestHead != bestBody && bestBodyHeight > 0f &&
            LocalBox(bestHead).y < bestBodyHeight * 0.75f)
        {
            _partHead = bestHead;
        }

        _partBody = bestBody;
        _partWing = bestWing;

        AddPart(_partBody);
        AddPart(_partHead);
        AddPart(_partWing);

        _partsReady = _partBody != null || _partHead != null || _partWing != null;
    }

    private Vector3 LocalBox(Transform part)
    {
        if (part == null) return Vector3.zero;
        Renderer renderer = part.GetComponent<Renderer>();
        Vector3 size, center;
        return TryGetLocalBox(renderer, out size, out center) ? size : Vector3.zero;
    }

    private void AddPart(Transform part)
    {
        if (part == null) return;
        _partRestRotation[part] = part.localRotation;
        _partRestPosition[part] = part.localPosition;
    }

    /// <summary>
    /// Габариты меша в координатах модели. Именно в её системе, а не в собственной:
    /// у импортированных частей произвольный локальный масштаб, и по их локальным
    /// размерам корпус выглядит меньше головы. Перевод в систему модели снимает
    /// и масштаб, и поворот, и смещение части.
    /// </summary>
    private bool TryGetLocalBox(Renderer renderer, out Vector3 size, out Vector3 center)
    {
        size = Vector3.zero;
        center = Vector3.zero;
        if (renderer == null || _model == null) return false;

        Transform t = renderer.transform;
        Mesh mesh = null;

        SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
        if (skinned != null) mesh = skinned.sharedMesh;
        else
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter != null) mesh = filter.sharedMesh;
        }

        if (mesh == null) return false;

        Matrix4x4 toModel = _model.worldToLocalMatrix * t.localToWorldMatrix;
        Bounds meshBounds = mesh.bounds;

        Vector3 extents = toModel.MultiplyVector(meshBounds.extents);
        extents = new Vector3(Mathf.Abs(extents.x), Mathf.Abs(extents.y), Mathf.Abs(extents.z));

        center = toModel.MultiplyPoint3x4(meshBounds.center);
        size = extents * 2f;
        return true;
    }

    private void SetPartRot(Transform part, float x, float y, float z)
    {
        if (part == null) return;
        Quaternion rest;
        if (!_partRestRotation.TryGetValue(part, out rest)) return;
        part.localRotation = rest * Quaternion.Euler(x, y, z);
    }

    private void SetPartPos(Transform part, float meters)
    {
        if (part == null) return;
        Vector3 rest;
        if (!_partRestPosition.TryGetValue(part, out rest)) return;
        rest.y += meters * _unit;
        part.localPosition = rest;
    }

    /// <summary>
    /// Бег и прыжок для модели без костей: крылья взмахиваются тем чаще, чем быстрее бег,
    /// тело отскакивает на каждом шаге, в прыжке всё поджимается и уходит назад.
    /// </summary>
    private void AnimateParts(float swing, float bob, bool rising)
    {
        if (!_partsReady) return;

        float run = _runBlend;
        float air = _airBlend;

        if (_partWing != null)
        {
            float rate = 2.2f + run * 4.5f + air * 3.5f;
            float flap = Mathf.Sin(_time * rate * Mathf.PI * 2f);

            // В прыжке бег прекращается, и выигрыш от run исчезает вместе с ним.
            // Поэтому вклад воздуха заведомо больше бега: иначе в прыжке крылья
            // бьют слабее, чем на бегу, и прыжок читается как просадка.
            float amp = 9f + run * 13f + air * 28f;
            float sweep = 6f + run * 16f + air * 38f;
            SetPartRot(_partWing, -sweep, 0f, flap * amp);
        }

        if (_partBody != null)
        {
            // Отскок на каждом шаге плюс поджатие в воздухе.
            SetPartPos(_partBody, Mathf.Abs(bob) * 0.045f * run - air * 0.09f);
            SetPartRot(_partBody, air * (rising ? 11f : -6f), 0f, swing * 3f * run);
        }

        if (_partHead != null)
        {
            // Голова отстаёт от тела и чуть кивает в такт шагам.
            SetPartPos(_partHead, -Mathf.Abs(bob) * 0.03f * run - air * 0.05f);
            SetPartRot(_partHead,
                Blend(0f, -swing * 4f, rising ? -12f : 8f, run, air),
                Blend(0f, swing * 3f, 0f, run, air),
                Blend(0f, -swing * 5f, 0f, run, air));
        }
    }

    private void SetRot(Transform bone, float x, float y, float z)
    {
        if (bone == null) return;
        Quaternion rest;
        if (!_restRotation.TryGetValue(bone, out rest))
            rest = Quaternion.identity;

        Quaternion euler = Quaternion.Euler(x, y, z);

        Quaternion parent;
        if (_modelSpace && _parentModelRotation.TryGetValue(bone, out parent))
        {
            // Поворот в системе модели: оси модели, а не собственные оси кости.
            // У Source-рига «взмах вперёд-назад» идёт вокруг Z, и при вращении в
            // собственных осях рука качалась бы вбок.
            bone.localRotation = Quaternion.Inverse(parent) * euler * parent * rest;
            return;
        }

        bone.localRotation = rest * euler;
    }

    /// <param name="meters">Смещение в метрах игрового мира.</param>
    private void SetPos(Transform bone, float meters)
    {
        if (bone == null) return;
        Vector3 rest;
        if (!_restPosition.TryGetValue(bone, out rest))
            return;

        if (_modelSpace && bone.parent != null)
        {
            Quaternion parent;
            if (_parentModelRotation.TryGetValue(bone, out parent))
            {
                // Смещение вверх в системе модели; делим на масштаб родителя, чтобы
                // метры остались метрами, даже если внутри модели есть свой масштаб.
                float unit = 1f / Mathf.Max(0.0001f, Mathf.Abs(bone.parent.lossyScale.y));
                rest += Quaternion.Inverse(parent) * (Vector3.up * (meters * unit));
                bone.localPosition = rest;
                return;
            }
        }

        rest.y += meters * _unit;
        bone.localPosition = rest;
    }

    public void ResetPose()
    {
        _cycle = 0f;
        _runBlend = 0f;
        _airBlend = 0f;
        _wallBlend = 0f;
        _jumpTimer = 0f;
        _landTimer = 0f;
        _landImpact = 0f;

        if (_model != null)
        {
            _model.localScale = _modelScale;
            _model.localPosition = _modelPosition;
            _model.localRotation = _modelRotation;
        }

        for (int i = 0; i < _posed.Count; i++)
        {
            Transform bone = _posed[i];
            if (bone == null) continue;
            bone.localRotation = _restRotation[bone];
            bone.localPosition = _restPosition[bone];
        }

        foreach (KeyValuePair<Transform, Quaternion> pair in _partRestRotation)
        {
            if (pair.Key != null) pair.Key.localRotation = pair.Value;
        }

        foreach (KeyValuePair<Transform, Vector3> pair in _partRestPosition)
        {
            if (pair.Key != null) pair.Key.localPosition = pair.Value;
        }
    }

    private void DetectAnimator()
    {
        DrivenByAnimator = false;
        if (_model == null) return;

        Animator animator = _model.GetComponentInChildren<Animator>();
        if (animator == null) return;

        if (animator.runtimeAnimatorController != null)
        {
            animator.applyRootMotion = false;
            DrivenByAnimator = true;
        }
        else
        {
            animator.enabled = false;
        }
    }

    private void CacheBones()
    {
        _bones.Clear();
        _posed.Clear();
        _restRotation.Clear();
        _restPosition.Clear();
        _ready = false;

        _hips = null;
        _spine = null;
        _chest = null;
        _head = null;
        _armL = _foreL = _armR = _foreR = null;
        _legL = _shinL = _legR = _shinR = null;

        if (_model == null) return;

        Transform[] all = _model.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            string key = NormalizeName(all[i].name);
            if (key.Length == 0 || _bones.ContainsKey(key))
                continue;
            _bones.Add(key, all[i]);
        }

        _head = Role("head", "Head", "HeadTop");
        _armL = Role("armL", "LeftArm", "LeftUpperArm", "LeftUpArm", "UpperArmL", "UpArmL", "ArmL", "LUpArm", "LUpperArm", "LArm", "LeftShoulder");
        _foreL = Role("foreL", "LeftForeArm", "LeftForearm", "LeftLowerArm", "LeftElbow", "ForeArmL", "ForearmL", "LowerArmL", "ElbowL", "LLowerArm", "LForeArm");
        _armR = Role("armR", "RightArm", "RightUpperArm", "RightUpArm", "UpperArmR", "UpArmR", "ArmR", "RUpArm", "RUpperArm", "RArm", "RightShoulder");
        _foreR = Role("foreR", "RightForeArm", "RightForearm", "RightLowerArm", "RightElbow", "ForeArmR", "ForearmR", "LowerArmR", "ElbowR", "RLowerArm", "RForeArm");
        _legL = Role("legL", "LeftUpLeg", "LeftUpperLeg", "LeftThigh", "UpLegL", "UpperLegL", "ThighL", "LUpLeg", "LUpperLeg", "LThigh");
        _shinL = Role("shinL", "LeftLeg", "LeftLowerLeg", "LeftShin", "LeftCalf", "LeftKnee", "LegL", "LowerLegL", "ShinL", "CalfL", "LLowerLeg", "LKnee");
        _legR = Role("legR", "RightUpLeg", "RightUpperLeg", "RightThigh", "UpLegR", "UpperLegR", "ThighR", "RUpLeg", "RUpperLeg", "RThigh");
        _shinR = Role("shinR", "RightLeg", "RightLowerLeg", "RightShin", "RightCalf", "RightKnee", "LegR", "LowerLegR", "ShinR", "CalfR", "RLowerLeg", "RKnee");

        _hips = Role("hips", "Hips", "Hip", "Root", "Pelvis", "Armature", "Bip01", "Bip001", "Hips.L");
        _spine = Role("spine", "Spine", "Chest1");
        _chest = Role("chest", "Chest", "UpperChest", "Ribcage", "Torso");

        FillGaps();

        Add(_hips);
        Add(_spine);
        Add(_chest);
        Add(_head);
        Add(_armL);
        Add(_foreL);
        Add(_armR);
        Add(_foreR);
        Add(_legL);
        Add(_shinL);
        Add(_legR);
        Add(_shinR);

        CacheParentModelRotation();

        _ready = _hips != null || _head != null;
    }

    /// <summary>
    /// Запоминает позу родителя каждой кости в системе модели на момент покоя.
    /// По ней model-space вращение переводится в локальную систему кости.
    /// </summary>
    private void CacheParentModelRotation()
    {
        _parentModelRotation.Clear();
        if (!_modelSpace || _model == null) return;

        for (int i = 0; i < _posed.Count; i++)
        {
            Transform bone = _posed[i];
            if (bone == null || bone.parent == null) continue;
            _parentModelRotation[bone] = Quaternion.Inverse(_model.rotation) * bone.parent.rotation;
        }
    }

    /// <summary>Достраивает таз, грудь и позвоночник по иерархии, если по именам не нашлось.</summary>
    private void FillGaps()
    {
        if (_hips == null)
            _hips = CommonAncestor(_legL, _legR);

        // У многих ригов корень называется «Spine» — это таз, а не позвоночник.
        if (_spine == _hips)
            _spine = null;

        if (_chest == null && _armL != null)
        {
            Transform shoulder = _armL.parent;
            if (shoulder != null && shoulder != _hips)
            {
                Transform chest = shoulder.parent != null ? shoulder.parent : shoulder;
                if (chest != _hips)
                    _chest = chest;
            }
        }

        if (_chest == null && _head != null && _head.parent != null && _head.parent != _hips)
            _chest = _head.parent;

        if (_spine == null && _chest != null)
        {
            Transform between = _chest.parent;
            _spine = between != null ? between : _hips;
        }

        if (_spine == _hips)
            _spine = null;
    }

    private static Transform CommonAncestor(Transform a, Transform b)
    {
        if (a == null || b == null) return null;

        var up = new HashSet<Transform>();
        for (Transform t = a; t != null; t = t.parent) up.Add(t);
        for (Transform t = b; t != null; t = t.parent)
        {
            if (up.Contains(t)) return t;
        }
        return null;
    }

    private void Add(Transform bone)
    {
        if (bone == null || _posed.Contains(bone)) return;
        _posed.Add(bone);
        _restRotation[bone] = bone.localRotation;
        _restPosition[bone] = bone.localPosition;
    }

    private Transform Bone(params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform found;
            if (_bones.TryGetValue(NormalizeName(names[i]), out found) && found != null)
                return found;
        }
        return null;
    }

    /// <summary>
    /// Кость по роли: сперва точное имя из SkinDefinition.BoneNames (для ригов с
    /// чужими именами), иначе — обычный поиск по синонимам.
    /// </summary>
    private Transform Role(string role, params string[] names)
    {
        if (_skin != null)
        {
            string custom = _skin.BoneName(role);
            if (!string.IsNullOrEmpty(custom))
            {
                Transform found;
                if (_bones.TryGetValue(NormalizeName(custom), out found) && found != null)
                    return found;
            }
        }
        return Bone(names);
    }

    /// <summary>Человекочитаемое имя кости: без префиксов экспорта, без разделителей, без ведущих нулей.</summary>
    private static string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;

        // mixamorig:Hips, Armature|Hips, DEF-upper_arm.L, B-forearm
        int colon = name.IndexOf(':');
        if (colon >= 0 && colon < name.Length - 1)
            name = name.Substring(colon + 1);

        name = StripPrefix(name, "def-");
        name = StripPrefix(name, "b-");
        name = StripPrefix(name, "bip01");
        name = StripPrefix(name, "bip001");

        if (name.Length == 0) return string.Empty;

        char[] buffer = new char[name.Length];
        int count = 0;
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (c == '_' || c == ' ' || c == '-' || c == '.' || c == '|') continue;
            buffer[count++] = char.ToLowerInvariant(c);
        }
        return TrimZeros(new string(buffer, 0, count));
    }

    private static string StripPrefix(string name, string prefix)
    {
        if (name.Length > prefix.Length && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return name.Substring(prefix.Length);
        return name;
    }

    /// <summary>«spine.001» и «spine1» должны стать одним ключом — иначе бонусные кости не найдутся.</summary>
    private static string TrimZeros(string name)
    {
        int firstDigit = -1;
        for (int i = 0; i < name.Length; i++)
        {
            if (name[i] >= '0' && name[i] <= '9') { firstDigit = i; break; }
        }
        if (firstDigit < 0) return name;

        var writer = new System.Text.StringBuilder(name.Length);
        writer.Append(name, 0, firstDigit);

        int at = firstDigit;
        while (at < name.Length)
        {
            if (name[at] < '0' || name[at] > '9')
            {
                writer.Append(name[at]);
                at++;
                continue;
            }

            int start = at;
            while (at < name.Length && name[at] >= '0' && name[at] <= '9') at++;
            writer.Append(int.Parse(name.Substring(start, at - start)).ToString());
        }

        return writer.ToString();
    }

    /// <summary>Что нашлось при разборе скелета — для отладки и дымового теста.</summary>
    public string BoneReport()
    {
        return string.Format(
            "hips={0} spine={1} chest={2} head={3} armL={4} foreL={5} armR={6} foreR={7} legL={8} shinL={9} legR={10} shinR={11}",
            Name(_hips), Name(_spine), Name(_chest), Name(_head),
            Name(_armL), Name(_foreL), Name(_armR), Name(_foreR),
            Name(_legL), Name(_shinL), Name(_legR), Name(_shinR));
    }

    private static string Name(Transform t)
    {
        return t != null ? t.name : "-";
    }
}
