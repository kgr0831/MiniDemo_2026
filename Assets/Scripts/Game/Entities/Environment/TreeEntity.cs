using UnityEngine;

// 나무 (객체 엔티티)
public class TreeEntity : MonoBehaviour, IGatherable, IDamageable
{
    public void Gather(ToolType toolType, PlayerController player)
    {
        if (toolType == ToolType.Axe) 
        { 
            // 벌목 로직 작동 
        }
    }

    public void TakeDamage(int damage)
    {
        // 일정 데미지 이상 누적 시 강제 철거/파괴
    }
}
