// 플레이어가 어느 방에 있는지 추적하고 카메라를 해당 방에 맞춰 이동시키는 일반 룸 카메라 컨트롤러
// 큰 방에서는 플레이어를 따라가되, 카메라가 방 경계 밖으로 나가지 않도록 위치를 제한한다.
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(CameraSizeController))]
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

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        if (cam == null || camSizeController == null || player == null)
        {
            Debug.LogError("RoomCameraController needs Camera, CameraSizeController, and Player reference.", this);
            enabled = false;
            return;
        }

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
        if (player == null) return;
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

    public void ResetToPlayerRoom()
    {
        // 리스폰처럼 플레이어 위치가 크게 바뀐 경우 현재 방과 카메라 위치를 즉시 다시 맞춥니다.
        if (player == null)
            return;

        StopAllCoroutines();
        isTransitioning = false;
        allRooms = FindObjectsByType<Room>(FindObjectsSortMode.None);
        currentRoom = GetRoomContaining(player.position);

        if (currentRoom == null)
            return;

        transform.position = currentRoom.nX > camSizeController.n || currentRoom.nY > camSizeController.n
            ? GetClampedPosition(currentRoom)
            : currentRoom.transform.position + cameraOffset;
    }

    private Room GetRoomContaining(Vector3 position)
    {
        if (allRooms == null) return null;

        foreach (Room room in allRooms)
        {
            if (room.GetBounds().Contains(position))
                return room;
        }
        return null;
    }

    private IEnumerator TransitionToRoom(Room targetRoom)
    {
        // 방 사이 이동은 바로 순간이동하지 않고 지정된 곡선에 따라 부드럽게 전환한다.
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

    // 카메라 중심이 방 경계 밖으로 나가지 않게 플레이어 위치를 제한한다.
    private Vector3 GetClampedPosition(Room room)
    {
        float halfW = Mathf.Max(0f, room.roomSize.x / 2f - cam.orthographicSize * cam.aspect);
        float halfH = Mathf.Max(0f, room.roomSize.y / 2f - cam.orthographicSize);

        float clampX = Mathf.Clamp(player.position.x, room.transform.position.x - halfW, room.transform.position.x + halfW);
        float clampY = Mathf.Clamp(player.position.y, room.transform.position.y - halfH, room.transform.position.y + halfH);

        return new Vector3(clampX, clampY, cameraOffset.z);
    }
}
