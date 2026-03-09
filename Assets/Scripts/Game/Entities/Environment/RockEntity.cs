using UnityEngine;

// 바위 (채광 가능, 공격으로 채집)
public class RockEntity : ResourceEntity
{ 
    protected override void Awake()
    {
        maxHealth = 40;
        dropAmount = 3;
        base.Awake();
    }
}
