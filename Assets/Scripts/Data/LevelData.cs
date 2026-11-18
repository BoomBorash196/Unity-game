using System.Collections.Generic;
using UnityEngine;

public class LevelData
{
    public GameObject Root;
    public int ActiveCheckpointIndex;
    public Dictionary<int, Checkpoint> Checkpoints = new Dictionary<int, Checkpoint>();
}