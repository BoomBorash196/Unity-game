using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform Target;
    public float Distance = 8f;
    public float HeightOffset = 2.2f;
    public float Sensitivity = 1f;
    public float SmoothTime = 8f;
    /// <summary>Нижняя граница наклона. Отрицательная — это взгляд вверх.</summary>
    ///
    /// Раньше стояло 15, и вверх посмотреть было нечем вовсе: положительный
    /// наклон опускает камеру над игроком и смотрит вниз, а вверх требовало
    /// отрицательного, который зажим срезал. Половина диапазона была
    /// недостижима, и мышь упиралась в верх экрана без всякой реакции.
    ///
    /// Минус 30 выбран по геометрии, а не на глаз: при угле обзора 65
    /// игрок оказывается в 29 градусах от центра кадра, то есть у самой нижней
    /// кромки, и остаётся в кадре на самом верхом наклоне вверх. Верх кадра
    /// при этом уходит на 47 градусов над горизонтом — видно и платформы над
    /// головой, и небо. Если брать меньше, игрок начнёт выпадать из кадра;
    /// если больше, камера уйдёт под платформу, на которой стоит игрок.
    public float PitchMin = -30f;
    public float PitchMax = 75f;
    public float BaseYaw = 0f;

    private float _yaw;
    private float _pitch;
    private Vector3 _velocity;

    public void Snap()
    {
        if (Target == null) return;
        Vector3 desired = DesiredPosition();
        transform.position = desired;
        _velocity = Vector3.zero;
        LookAtTarget();
    }

    public void ResetTo(float yaw)
    {
        _yaw = yaw;
        _pitch = 25f;
        if (Target != null)
        {
            Vector3 desired = DesiredPosition();
            transform.position = desired;
            _velocity = Vector3.zero;
            LookAtTarget();
        }
    }

    private void LateUpdate()
    {
        if (Target == null) return;

        if (GameManager.Instance == null || GameManager.Instance.State == GameState.Playing)
        {
            if (InputService.Instance != null)
            {
                Vector2 look = InputService.Instance.LookDelta();
                float frameSens = Sensitivity * 120f / Mathf.Max(1, Screen.height);
                _yaw += look.x * frameSens;
                _pitch -= look.y * frameSens;
            }
            _pitch = Mathf.Clamp(_pitch, PitchMin, PitchMax);
        }

        Vector3 desired = DesiredPosition();
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, 1f / SmoothTime);
        LookAtTarget();
    }

    private Vector3 DesiredPosition()
    {
        Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 backDir = rot * Vector3.back;
        Vector3 anchor = Target.position + Vector3.up * HeightOffset;
        return anchor + backDir * Distance;
    }

    private void LookAtTarget()
    {
        Vector3 look = Target.position + Vector3.up * 0.9f;
        transform.LookAt(look);
    }
}