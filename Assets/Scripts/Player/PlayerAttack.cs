using System.Collections.Generic;
using UnityEngine;

public enum AttackInputType
{
    Light,
    Heavy,
    Grab
}

[DisallowMultipleComponent]
public sealed class PlayerAttack : MonoBehaviour
{
    private const string ComboDefinitionResourcePath = "ComboDefinition";

    private ComboGraph _comboGraph;
    private ComboNode _currentNode;
    private ComboNode _queuedNode;
    // 필살기는 콤보 노드를 만들지 않고 전달받은 공격 정의를 단발 실행한다.
    private AttackDefinition _standaloneAttack;
    private AttackInputType _queuedInputType;
    private float _previousAttackMoveProgress;

    public AttackInputType CurrentAttackType { get; private set; }
    public bool IsAttacking { get; private set; }
    public bool IsLethalArtActive => IsAttacking && _standaloneAttack;

    private void Awake()
    {
        InitializeComboGraph();
    }

    public void SetUnlockedComboIds(IReadOnlyList<int> unlockedIds)
    {
        InitializeComboGraph();
        _comboGraph.SetUnlockedComboIds(unlockedIds);

        // 재동기화로 예약한 경로가 닫혀도 현재 재생 중인 공격은 유지한다.
        if (_queuedNode != null && !_queuedNode.IsAvailable) _queuedNode = null;
    }

    public bool TryGetComboDefinition(int comboId, out ComboDefinition definition)
    {
        InitializeComboGraph();
        return _comboGraph.TryGetDefinition(comboId, out definition);
    }

    private void InitializeComboGraph()
    {
        // Manager.Awake에서 먼저 호출해도 이 컴포넌트의 Awake가 그래프를 다시 만들지 않는다.
        if (_comboGraph != null) return;

        // Resources/ComboDefinition 및 하위 폴더의 콤보 데이터를 모두 불러온다.
        ComboDefinition[] comboDefinitions = Resources.LoadAll<ComboDefinition>(ComboDefinitionResourcePath);
        _comboGraph = ComboGraph.Create(comboDefinitions);
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
        _standaloneAttack = null;
        _previousAttackMoveProgress = 0f;
        CurrentAttackType = inputType;
        IsAttacking = true;
        attackDefinition = firstNode.AttackDefinition;
        return true;
    }

    public bool TryBeginLethalArt(AttackDefinition attackDefinition)
    {
        if (IsLethalArtActive || !attackDefinition) return false;

        // 일반 공격은 어느 구간에서든 교체하고, 이전 콤보 예약과 이동 진행도는 이어받지 않는다.
        _currentNode = null;
        _queuedNode = null;
        _standaloneAttack = attackDefinition;
        _previousAttackMoveProgress = 0f;
        CurrentAttackType = AttackInputType.Heavy;
        IsAttacking = true;
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
        _previousAttackMoveProgress = 0f;
        CurrentAttackType = _queuedInputType;
        attackDefinition = _currentNode.AttackDefinition;
        return true;
    }

    public bool TryGetAttackMoveDisplacement(float normalizedTime, out Vector2 localDisplacement)
    {
        localDisplacement = Vector2.zero;

        if (!IsAttacking)
        {
            return false;
        }

        // 단발 필살기도 일반 공격과 같은 이동 구간과 커브를 사용한다.
        AttackDefinition attackDefinition = _currentNode != null ? _currentNode.AttackDefinition : _standaloneAttack;
        if (!attackDefinition) return false;

        float moveProgress = EvaluateAttackMoveProgress(attackDefinition, normalizedTime);
        float progressDelta = moveProgress - _previousAttackMoveProgress;

        _previousAttackMoveProgress = moveProgress;
        localDisplacement = attackDefinition.AttackMoveDistance * progressDelta;
        return true;
    }

    public void EndAttack()
    {
        _currentNode = null;
        _queuedNode = null;
        _standaloneAttack = null;
        _previousAttackMoveProgress = 0f;
        IsAttacking = false;
    }

    private static float EvaluateAttackMoveProgress(AttackDefinition attackDefinition, float normalizedTime)
    {
        float moveStartTime = attackDefinition.MoveStartNormalizedTime;
        float moveEndTime = attackDefinition.MoveEndNormalizedTime;

        if (normalizedTime <= moveStartTime)
        {
            return 0f;
        }

        if (normalizedTime >= moveEndTime || moveEndTime <= moveStartTime)
        {
            return 1f;
        }

        float moveNormalizedTime = Mathf.InverseLerp(moveStartTime, moveEndTime, normalizedTime);
        AnimationCurve moveCurve = attackDefinition.MoveCurve;

        // 커브가 비어 있으면 이동 구간 동안 선형으로 이동한다.
        return moveCurve != null ? moveCurve.Evaluate(moveNormalizedTime) : moveNormalizedTime;
    }
}
