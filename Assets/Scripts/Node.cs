using UnityEngine;

public class Node : MonoBehaviour
{
    public Vector2 Pos => transform.position;
    public Block OccupiedBlock;
}
