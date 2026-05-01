using UnityEngine;

[ExecuteAlways] // 에디터에서도 실시간 반영
[RequireComponent(typeof(Camera))]
public class CameraSizeController : MonoBehaviour
{
    [Header("카메라 크기 배수 (4.5 × n)")]
    [Min(1)]
    public int n = 1; // 1 → 4.5, 2 → 9.0, 3 → 13.5

    private Camera cam;
    private int prevN;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void OnValidate() // 인스펙터 값 바꿀 때마다 호출
    {
        if (cam == null) cam = GetComponent<Camera>();
        cam.orthographicSize = 4.5f * n;
    }
}