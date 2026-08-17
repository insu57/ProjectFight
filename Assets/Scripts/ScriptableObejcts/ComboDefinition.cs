using System;
using UnityEngine;

[Serializable]
public struct ComboStep
{
    [field: SerializeField] public AttackDefinition AttackDefinition;
    [field: SerializeField] public AttackInputType AttackInputType;
}

[CreateAssetMenu(fileName = "ComboDefinition", menuName = "Scriptable Objects/ComboDefinition")]
public class ComboDefinition : ScriptableObject
{
    [field:SerializeField] public int ComboID { get; private set; }
    [field:SerializeField] public string ComboName { get; private set; }
    [field: SerializeField] public ComboStep[] ComboSteps { get; private set; }
    //기본 데미지(각 공격별)
    //가드 브레이크 수치?
}
