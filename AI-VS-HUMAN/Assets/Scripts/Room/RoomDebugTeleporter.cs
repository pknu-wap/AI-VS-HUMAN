using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class RoomDebugTeleporter : MonoBehaviour
{
    [Header("참조")]
    public Transform player;
    public RoomCameraController roomCameraController;

    [Header("방 순서")]
    public List<Room> rooms = new List<Room>();
    public Vector2 roomTeleportPositionFromBottomLeft = new Vector2(4f, 3.5f);
    public bool resetRoomsOnTeleport = true;

    [Header("단축키")]
    public bool enableHotkeys = true;
    public bool requireCtrl = true;

    private int currentRoomIndex = -1;

    private void Awake()
    {
        ResolveReferences();
        UpdateCurrentRoomIndexFromPlayer();
    }

    private void Update()
    {
        if (!enableHotkeys)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (requireCtrl && !IsCtrlPressed(keyboard))
            return;

        if (keyboard[Key.LeftBracket].wasPressedThisFrame)
            TeleportByOffset(-1);
        else if (keyboard[Key.RightBracket].wasPressedThisFrame)
            TeleportByOffset(1);
    }

    public void TeleportToPreviousRoom()
    {
        TeleportByOffset(-1);
    }

    public void TeleportToNextRoom()
    {
        TeleportByOffset(1);
    }

    public void TeleportToRoomIndex(int index)
    {
        ResolveReferences();

        if (player == null || rooms == null || rooms.Count == 0)
            return;

        if (index < 0 || index >= rooms.Count || rooms[index] == null)
            return;

        Room sourceRoom = GetRoomContainingPlayer();
        Room targetRoom = rooms[index];

        if (resetRoomsOnTeleport)
        {
            ResetRoomForTeleport(sourceRoom);

            if (targetRoom != sourceRoom)
                ResetRoomForTeleport(targetRoom);
        }

        currentRoomIndex = index;
        TeleportPlayerToRoom(targetRoom);
    }

    private void TeleportByOffset(int offset)
    {
        ResolveReferences();

        if (player == null || rooms == null || rooms.Count == 0)
            return;

        if (currentRoomIndex < 0 || currentRoomIndex >= rooms.Count)
            UpdateCurrentRoomIndexFromPlayer();

        if (currentRoomIndex < 0)
            currentRoomIndex = 0;

        int targetIndex = Mathf.Clamp(currentRoomIndex + offset, 0, rooms.Count - 1);
        TeleportToRoomIndex(targetIndex);
    }

    private void TeleportPlayerToRoom(Room targetRoom)
    {
        Vector3 targetPosition = GetTeleportPosition(targetRoom);
        player.position = targetPosition;

        Rigidbody2D playerRigidbody = player.GetComponent<Rigidbody2D>();
        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector2.zero;
            playerRigidbody.angularVelocity = 0f;
        }

        Physics2D.SyncTransforms();

        if (roomCameraController != null)
        {
            roomCameraController.enabled = true;
            roomCameraController.ResetToPlayerRoom();
        }
    }

    private Vector3 GetTeleportPosition(Room targetRoom)
    {
        Bounds bounds = targetRoom.GetBounds();
        return new Vector3(
            bounds.min.x + roomTeleportPositionFromBottomLeft.x,
            bounds.min.y + roomTeleportPositionFromBottomLeft.y,
            player != null ? player.position.z : targetRoom.transform.position.z);
    }

    private void UpdateCurrentRoomIndexFromPlayer()
    {
        if (player == null || rooms == null)
            return;

        for (int i = 0; i < rooms.Count; i++)
        {
            Room room = rooms[i];
            if (room != null && room.GetBounds().Contains(player.position))
            {
                currentRoomIndex = i;
                return;
            }
        }
    }

    private Room GetRoomContainingPlayer()
    {
        if (player == null || rooms == null)
            return null;

        foreach (Room room in rooms)
        {
            if (room != null && room.GetBounds().Contains(player.position))
                return room;
        }

        return null;
    }

    private void ResetRoomForTeleport(Room room)
    {
        if (room == null)
            return;

        foreach (RoomEnemyEncounter encounter in room.GetComponents<RoomEnemyEncounter>())
            encounter.ResetForDebugTeleport();

        foreach (Stage1BossRoomController bossRoom in room.GetComponents<Stage1BossRoomController>())
            bossRoom.ResetForDebugTeleport();

        foreach (Stage2BossRoomController bossRoom in room.GetComponents<Stage2BossRoomController>())
            bossRoom.ResetForDebugTeleport();
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        if (roomCameraController == null && Camera.main != null)
            roomCameraController = Camera.main.GetComponent<RoomCameraController>();
    }

    private bool IsCtrlPressed(Keyboard keyboard)
    {
        return keyboard[Key.LeftCtrl].isPressed || keyboard[Key.RightCtrl].isPressed;
    }
}
