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

    private void Start()
    {
        allRooms = FindObjectsByType<Room>(FindObjectsSortMode.None);
        
        // 시작 시 플레이어가 있는 방 찾기
        currentRoom = GetRoomContaining(player.position);
        
        if (currentRoom != null)
            transform.position = currentRoom.transform.position + cameraOffset;
    }

    private void LateUpdate()
    {
        if (isTransitioning) return;

        // 플레이어가 현재 방을 벗어났는지 체크
        if (currentRoom != null && !currentRoom.GetBounds().Contains(player.position))
        {
            Room nextRoom = GetRoomContaining(player.position);
            if (nextRoom != null && nextRoom != currentRoom)
            {
                StartCoroutine(TransitionToRoom(nextRoom));
            }
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
        Vector3 endPos = targetRoom.transform.position + cameraOffset;

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
        isTransitioning = false;
    }
}