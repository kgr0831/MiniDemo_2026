using UnityEngine;

// 바위 (채광 가능, 타격 가능)
public class RockEntity : MonoBehaviour, IGatherable, IDamageable
{ 
    public int health = 40; // 바위 체력
    public GameObject stoneDropPrefab; // 부셔졌을 때 나올 아이템 드랍 프리팹
    public int dropAmount = 3; // 떨어질 돌 조각 수
    
    // 일반 공격으로 타격 받았을 때
    public void TakeDamage(int damage)
    {
        // 바위는 일반 공격(검 등)으로는 데미지를 매우 적게 받거나 안 받게 설계
        Debug.Log("Rock is too hard for normal attacks! Took 1 damage.");
        health -= 1;
        CheckDestroy();
    }

    // 채광 툴(Pickaxe)로 타격 받았을 때
    public void Gather(ToolType toolType, PlayerController player)
    {
        if (toolType == ToolType.Pickaxe || toolType == ToolType.Axe) // 데모를 위해 도끼도 일단 허용
        {
            Debug.Log("Mining rock!");
            health -= 20; // 도구 특효 데미지 
            CheckDestroy();
        }
        else
        {
            Debug.Log("This tool doesn't seem very effective on a rock.");
        }
    }

    private void CheckDestroy()
    {
        if (health <= 0)
        {
            Debug.Log("Rock destroyed!");
            // 아이템 드랍 로직
            if (stoneDropPrefab != null)
            {
                for (int i = 0; i < dropAmount; i++)
                {
                    // 바위 주변에 랜덤 위치로 약간 흩뿌림
                    Vector2 randomOffset = Random.insideUnitCircle * 0.5f;
                    Instantiate(stoneDropPrefab, (Vector2)transform.position + randomOffset, Quaternion.identity);
                }
            }
            
            Destroy(gameObject); // 파괴
        }
    }
}
