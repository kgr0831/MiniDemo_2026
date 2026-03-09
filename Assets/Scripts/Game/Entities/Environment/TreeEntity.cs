using UnityEngine;

// 나무 (벌목 가능, 공격으로 채집)
public class TreeEntity : ResourceEntity
{ 
    protected override void Awake()
    {
        maxHealth = 30;
        dropAmount = 2;
        base.Awake();
    }
}
