using UnityEngine;

public static class PlayerPhysics
{
    public const float Gravity = 30f;
    public const float JumpHeight = 3.2f;
    public const float RunSpeed = 8f;

    public static readonly float JumpSpeed = Mathf.Sqrt(2f * Gravity * JumpHeight);
    public static readonly float AirTime = 2f * JumpSpeed / Gravity;
    public static readonly float MaxHorizontalReach = RunSpeed * AirTime * 0.85f;
    public static readonly float MaxVerticalJump = JumpHeight * 0.93f;
}