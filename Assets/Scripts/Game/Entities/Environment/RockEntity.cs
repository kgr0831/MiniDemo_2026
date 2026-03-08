using UnityEngine;

// 바위 (객체 엔티티)
public class RockEntity : MonoBehaviour, IGatherable
{
    public void Gather(ToolType toolType, PlayerController player)
    {
        if (toolType == ToolType.Pickaxe) 
        { 
            // 채광 로직 
        }
    }
}
