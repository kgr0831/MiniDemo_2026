using UnityEngine;

/// <summary>
/// Y좌표 기반 SpriteRenderer sortingOrder 자동 조절.
/// 탑다운 게임에서 Y좌표가 낮은(화면 아래) 오브젝트가 앞에 그려지도록 합니다.
/// 플레이어, 몬스터, 나무, 바위 등 모든 스프라이트 오브젝트에 부착합니다.
/// </summary>
public class YSortOrder : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    
    [Tooltip("정렬 정밀도 배수 (값이 클수록 정밀, 기본 100)")]
    public int sortingPrecision = 100;

    [Tooltip("정렬 기준 Y 오프셋 (피벗이 중앙이면 스프라이트 하단으로 맞추기 위해 사용)")]
    public float yOffset = 0f;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // 피벗이 중앙이면, 스프라이트 하단 기준으로 정렬하기 위해 자동 오프셋 계산
        if (spriteRenderer != null && yOffset == 0f)
        {
            yOffset = -(spriteRenderer.bounds.extents.y);
        }
    }

    void LateUpdate()
    {
        if (spriteRenderer == null) return;
        
        // Y가 낮을수록 (화면 아래) sortingOrder가 높아짐 → 앞에 그려짐
        float sortY = transform.position.y + yOffset;
        spriteRenderer.sortingOrder = -(int)(sortY * sortingPrecision);
    }
}
