using UnityEngine;

// 나무 (벌목 가능, 타격 가능)
public class TreeEntity : MonoBehaviour, IGatherable, IDamageable
{ 
    public int health = 30; // 나무 체력
    public GameObject woodDropPrefab; // 부셔졌을 때 나올 아이템 드랍 프리팹
    public int dropAmount = 2; // 떨어질 나무 조각 수
    
    // 일반 공격으로 타격 받았을 때
    public void TakeDamage(int damage)
    {
        Debug.Log($"Tree took {damage} damage from an attack!");
        health -= damage;
        CheckDestroy();
    }

    // 벌목 툴(Axe)로 타격 받았을 때
    public void Gather(ToolType toolType, PlayerController player)
    {
        if (toolType == ToolType.Axe)
        {
            // 도끼는 일반 공격보다 더 많은 데미지를 주거나 즉시 벌목되도록 설계 가능
            Debug.Log("Chopping tree with an axe!");
            health -= 15; // 도끼 특효 데미지 
            CheckDestroy();
        }
        else
        {
            Debug.Log("This tool doesn't seem very effective on a tree.");
        }
    }

    private void CheckDestroy()
    {
        if (health <= 0)
        {
            Debug.Log("Tree chopped down!");
            // 아이템 드랍 로직
            if (woodDropPrefab != null)
            {
                for (int i = 0; i < dropAmount; i++)
                {
                    // 나무 주변에 랜덤 위치로 약간 흩뿌림
                    Vector2 randomOffset = Random.insideUnitCircle * 0.5f;
                    Instantiate(woodDropPrefab, (Vector2)transform.position + randomOffset, Quaternion.identity);
                }
            }
            
            // 나무 객체 제거 (또는 잘린 밑둥 이미지로 교체)
            Destroy(gameObject);
        }
    }
}
