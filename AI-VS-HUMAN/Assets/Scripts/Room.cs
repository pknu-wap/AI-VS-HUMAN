using UnityEngine;

[ExecuteAlways]
public class Room : MonoBehaviour
{
    [Header("방 크기 배수 (16 × n, 9 × n)")]
    [Min(1)]
    public int n = 1;

    public Vector2 roomSize => new Vector2(16f * n, 9f * n); // 자동 계산으로 변경

    public Bounds GetBounds()
    {
        return new Bounds(transform.position, roomSize);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
        Gizmos.DrawCube(transform.position, roomSize);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, roomSize);
    }
}