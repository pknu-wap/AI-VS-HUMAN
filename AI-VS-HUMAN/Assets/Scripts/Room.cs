using UnityEngine;

public class Room : MonoBehaviour
{
    [Header("방 크기 (카메라 기준)")]
    public Vector2 roomSize = new Vector2(20f, 12f); // 카메라가 보여줄 영역

    public Bounds GetBounds()
    {
        return new Bounds(transform.position, roomSize);
    }

    // 씬 뷰에서 방 영역 시각화
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
        Gizmos.DrawCube(transform.position, roomSize);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, roomSize);
    }
}