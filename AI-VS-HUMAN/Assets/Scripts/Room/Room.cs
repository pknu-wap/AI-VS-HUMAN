// 방의 크기와 경계를 정의하는 스크립트
// RoomCameraController와 BossRoomController는 이 Bounds를 기준으로 카메라 위치와 입장 여부를 판단한다.
using UnityEngine;

[ExecuteAlways]
public class Room : MonoBehaviour
{
    [Header("방 크기 배수")]
    [Min(1)] public int nX = 1; // 가로 (16 × nX)
    [Min(1)] public int nY = 1; // 세로 (9 × nY)

    public Vector2 roomSize => new Vector2(16f * nX, 9f * nY);

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
