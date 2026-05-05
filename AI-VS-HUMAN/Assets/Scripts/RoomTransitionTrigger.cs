using UnityEngine;

// Room 오브젝트에 BoxCollider2D(IsTrigger=true)와 함께 부착
[RequireComponent(typeof(BoxCollider2D))]
public class RoomTransitionTrigger : MonoBehaviour
{
    private RoomCameraController cameraController;

    private void Start()
    {
        cameraController = FindFirstObjectByType<RoomCameraController>();

        // BoxCollider2D 크기를 Room 크기에 자동 맞춤
        var room = GetComponent<Room>();
        var col = GetComponent<BoxCollider2D>();
        if (room != null && col != null)
            col.size = room.roomSize;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            // 카메라 컨트롤러의 LateUpdate가 자동으로 처리하므로
            // 여기서 이펙트, 오디오 등 추가 연출 가능
            Debug.Log($"[Room] {gameObject.name} 진입!");
        }
    }
}