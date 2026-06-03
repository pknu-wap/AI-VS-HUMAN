using UnityEngine;

[ExecuteAlways]
public class Room : MonoBehaviour
{
    [Header("방 크기")]
    [Min(1)] public int nX = 1;
    [Min(1)] public int nY = 1;

    [Header("리스폰")]
    public bool useRespawnPosition;
    public Vector3 respawnPosition;
    public Transform respawnPoint;

    public Vector2 roomSize => new Vector2(16f * nX, 9f * nY);

    public Bounds GetBounds()
    {
        return new Bounds(transform.position, roomSize);
    }

    public Vector3 GetRespawnPosition(Vector3 fallbackPosition)
    {
        if (useRespawnPosition)
            return respawnPosition;

        return respawnPoint != null ? respawnPoint.position : fallbackPosition;
    }

    public bool HasRespawnPosition()
    {
        return useRespawnPosition || respawnPoint != null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
        Gizmos.DrawCube(transform.position, roomSize);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, roomSize);

        if (HasRespawnPosition())
        {
            Vector3 markerPosition = GetRespawnPosition(transform.position);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(markerPosition, 0.35f);
            Gizmos.DrawLine(markerPosition + Vector3.left * 0.5f, markerPosition + Vector3.right * 0.5f);
            Gizmos.DrawLine(markerPosition + Vector3.down * 0.5f, markerPosition + Vector3.up * 0.5f);
        }
    }
}
