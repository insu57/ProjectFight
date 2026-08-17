using UnityEngine;
using UnityEngine.Serialization;

public enum AttackInputType
{
    Light,
    Heavy,
    Grab
}

[DisallowMultipleComponent]
public sealed class PlayerAttack : MonoBehaviour
{
    [SerializeField] private ComboDefinition[] _comboDefinitions;

    private ComboGraph _comboGraph;
    private ComboNode _currentNode;
    private ComboNode _queuedNode;
    private AttackInputType _queuedInputType;

    public AttackInputType CurrentAttackType { get; private set; }
    public bool IsAttacking { get; private set; }

    private void Awake()
    {
        // 인스펙터에 등록된 콤보 데이터를 런타임 탐색용 그래프로 변환한다.
        _comboGraph = ComboGraph.Create(_comboDefinitions);
    }

    public bool TryBeginAttack(AttackInputType inputType, out AttackDefinition attackDefinition)
    {
        attackDefinition = null;

        if (IsAttacking)
        {
            return false;
        }

        if (!_comboGraph.TryGetFirstNode(inputType, out ComboNode firstNode))
        {
            return false;
        }

        _currentNode = firstNode;
        _queuedNode = null;
        CurrentAttackType = inputType;
        IsAttacking = true;
        attackDefinition = firstNode.AttackDefinition;
        return true;
    }

    public bool TryQueueNextAttack(AttackInputType inputType, float normalizedTime)
    {
        if (!IsAttacking || _currentNode == null || _queuedNode != null)
        {
            return false;
        }

        AttackDefinition currentAttack = _currentNode.AttackDefinition;

        if (normalizedTime < currentAttack.ComboInputStartNormalizedTime || normalizedTime > currentAttack.ComboInputEndNormalizedTime)
        {
            return false;
        }

        if (!_currentNode.TryGetNextNode(inputType, out ComboNode nextNode))
        {
            return false;
        }

        // 유효한 연계 입력은 실행 시점까지 다음 노드로 예약한다.
        _queuedNode = nextNode;
        _queuedInputType = inputType;
        return true;
    }

    public bool TryConsumeQueuedAttack(float normalizedTime, out AttackDefinition attackDefinition)
    {
        attackDefinition = null;

        if (_currentNode == null || _queuedNode == null)
        {
            return false;
        }

        if (normalizedTime < _currentNode.AttackDefinition.ComboTransitionNormalizedTime)
        {
            return false;
        }

        _currentNode = _queuedNode;
        _queuedNode = null;
        CurrentAttackType = _queuedInputType;
        attackDefinition = _currentNode.AttackDefinition;
        return true;
    }

    public void EndAttack()
    {
        _currentNode = null;
        _queuedNode = null;
        IsAttacking = false;
    }
}
