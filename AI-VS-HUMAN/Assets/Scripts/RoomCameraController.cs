using System.Collections;
using UnityEngine;

public class RoomCameraController : MonoBehaviour
{
    [Header("참조")]
    public Transform player;

    [Header("전환 설정")]
    public float transitionDuration = 0.6f;
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("카메라 오프셋")]
    public Vector3 cameraOffset = new Vector3(0f, 0f, -10f);

    private Room currentRoom;
    private Room[] allRooms;
    private bool isTransitioning = false;
    private Camera cam;
    private CameraSizeController camSizeController;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        camSizeController = GetComponent<CameraSizeController>();
        
        allRooms = FindObjectsByType<Room>(FindObjectsSortMode.None);
        currentRoom = GetRoomContaining(player.position);

        if (currentRoom != null)
        {
            if (currentRoom.nX > camSizeController.n || currentRoom.nY > camSizeController.n)
            {
                transform.position = new Vector3(player.position.x, player.position.y, cameraOffset.z);
            }
            else
                transform.position = currentRoom.transform.position + cameraOffset;
        }
    }

    private void LateUpdate()
    {
        if (isTransitioning) return;

        if (currentRoom != null && !currentRoom.GetBounds().Contains(player.position))
        {
            Room nextRoom = GetRoomContaining(player.position);
            if (nextRoom != null && nextRoom != currentRoom)
            {
                StartCoroutine(TransitionToRoom(nextRoom));
                return;
            }
        }

        if (currentRoom != null && (currentRoom.nX > camSizeController.n || currentRoom.nY > camSizeController.n))
        {
            Vector3 target = GetClampedPosition(currentRoom);
            transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * 8f);
        }
    }

    private Room GetRoomContaining(Vector3 position)
    {
        foreach (Room room in allRooms)
        {
            if (room.GetBounds().Contains(position))
                return room;
        }
        return null;
    }

    private IEnumerator TransitionToRoom(Room targetRoom)
    {
        isTransitioning = true;

        Vector3 startPos = transform.position;

        // 클램프된 위치 계산 (방 경계 안쪽으로 제한)
        Vector3 endPos = GetClampedPosition(targetRoom);

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = transitionCurve.Evaluate(elapsed / transitionDuration);
            transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        transform.position = endPos;
        currentRoom = targetRoom;
        isTransitioning = false; // 전환 끝나면 LateUpdate가 자연스럽게 이어받음
    }

    // 클램프 계산을 별도 함수로 분리
    private Vector3 GetClampedPosition(Room room)
    {
        float halfW = room.roomSize.x / 2f - cam.orthographicSize * cam.aspect;
        float halfH = room.roomSize.y / 2f - cam.orthographicSize;

        float clampX = Mathf.Clamp(player.position.x, room.transform.position.x - halfW, room.transform.position.x + halfW);
        float clampY = Mathf.Clamp(player.position.y, room.transform.position.y - halfH, room.transform.position.y + halfH);

        return new Vector3(clampX, clampY, cameraOffset.z);
    }
}