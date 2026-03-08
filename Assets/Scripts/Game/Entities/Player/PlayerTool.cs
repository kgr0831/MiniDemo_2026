using UnityEngine;

// 벌목, 채집 등의 플레이어 파밍 장비 사용
public class PlayerTool : MonoBehaviour 
{ 
    public float toolRange = 1f;
    public float toolRadius = 0.5f;
    
    private PlayerController controller;
    private float toolTimer = 0f;
    public float toolDuration = 0.4f;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
    }

    public void StartTool()
    {
        toolTimer = 0f;
        PerformToolAction();
    }

    public void HandleTool()
    {
        toolTimer += Time.deltaTime;
        
        if (toolTimer >= toolDuration)
        {
            controller.ChangeState(PlayerState.Idle);
        }
    }

    private void PerformToolAction()
    {
        Vector2 toolPos = (Vector2)transform.position + (controller.moveModule.lastMoveDir * toolRange);
        
        Collider2D[] colliders = Physics2D.OverlapCircleAll(toolPos, toolRadius);
        
        // 현재 장착된 툴의 종류를 판단 (임시: Axe 작동 가정)
        ToolType currentEquippedTool = ToolType.Axe; 

        foreach (Collider2D hit in colliders)
        {
            if (hit.gameObject == gameObject) continue;

            IGatherable gatherable = hit.GetComponent<IGatherable>();
            if (gatherable != null)
            {
                gatherable.Gather(currentEquippedTool, controller);
                Debug.Log($"Used tool on {hit.name}");
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        PlayerMove move = GetComponent<PlayerMove>();
        Vector2 dir = move != null ? move.lastMoveDir : Vector2.down;
        Vector2 toolPos = (Vector2)transform.position + (dir * toolRange);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(toolPos, toolRadius);
    }
}
