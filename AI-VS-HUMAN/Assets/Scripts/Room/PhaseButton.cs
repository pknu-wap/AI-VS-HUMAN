using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PhaseButton : MonoBehaviour
{
    public BossRoomController bossRoomController;

    private bool isTriggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isTriggered) return;

        if (other.CompareTag("Player"))
        {
            isTriggered = true; // 한 번만 실행
            bossRoomController.StartPhase2();
        }
    }
}