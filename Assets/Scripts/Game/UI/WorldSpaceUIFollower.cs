using UnityEngine;

/// <summary>
/// World Space Canvas가 항상 카메라를 따라다니면서 화면 고정 위치에 보이도록 하는 스크립트.
/// 이 스크립트를 World Space Canvas 오브젝트에 붙여주세요.
/// </summary>
public class WorldSpaceUIFollower : MonoBehaviour
{
    [SerializeField] Camera targetCamera; // 따라갈 카메라 (Main Camera)

    [Header("화면 내 위치 설정 (Viewport 좌표: 0~1)")]
    [SerializeField] float viewportX = 0.5f;  // 0 = 왼쪽 끝, 1 = 오른쪽 끝
    [SerializeField] float viewportY = 0.9f;  // 0 = 아래 끝, 1 = 위쪽 끝

    [Header("카메라로부터의 거리")]
    [SerializeField] float distanceFromCamera = 5f; // 카메라 앞 몇 유닛에 배치할 것인지

    void LateUpdate()
    {
        if (targetCamera == null) return;

        // Viewport 좌표(0~1)를 월드 좌표로 변환
        Vector3 worldPos = targetCamera.ViewportToWorldPoint(
            new Vector3(viewportX, viewportY, distanceFromCamera)
        );

        transform.position = worldPos;
        
        // 카메라를 항상 똑바로 바라보도록 회전 (2D 게임이라 보통 정면)
        transform.rotation = targetCamera.transform.rotation;
    }
}
