using UnityEngine;

[RequireComponent(typeof(GiantDrone))]
public class GiantDronePhase2 : MonoBehaviour
{
    private GiantDrone boss;
    private bool isRunning;

    private void Awake()
    {
        boss = GetComponent<GiantDrone>();
    }

    public void StartPhase(GiantDrone owner)
    {
        boss = owner;

        if (isRunning)
            return;

        isRunning = true;

        if (boss.healDronePattern != null)
            boss.healDronePattern.StartPattern(boss);
    }

    public void StopPhase()
    {
        isRunning = false;

        if (boss != null && boss.healDronePattern != null)
            boss.healDronePattern.StopPattern();
    }
}
