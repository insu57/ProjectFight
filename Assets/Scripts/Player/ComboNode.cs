using System;

public sealed class ComboNode
{
    private const int InputTypeCount = 3;

    private readonly ComboNode[] _nextNodes = new ComboNode[InputTypeCount];

    public AttackDefinition AttackDefinition { get; }
    public ComboDefinition CompletedCombo { get; private set; }
    public bool IsComboEnd => CompletedCombo != null;
    // 해금한 콤보가 하나라도 지나는 입력 경로만 사용할 수 있다.
    public bool IsAvailable { get; internal set; }

    public ComboNode(AttackDefinition attackDefinition)
    {
        AttackDefinition = attackDefinition;
    }

    public bool TryGetNextNode(AttackInputType inputType, out ComboNode nextNode)
    {
        if (TryGetDefinedNextNode(inputType, out nextNode) && nextNode.IsAvailable) return true;

        nextNode = null;
        return false;
    }

    // 그래프 구성은 해금 여부와 무관하게 전체 정의를 연결하고 충돌을 검증한다.
    internal bool TryGetDefinedNextNode(AttackInputType inputType, out ComboNode nextNode)
    {
        int inputIndex = GetInputIndex(inputType);
        nextNode = _nextNodes[inputIndex];
        return nextNode != null;
    }

    internal void SetNextNode(AttackInputType inputType, ComboNode nextNode)
    {
        int inputIndex = GetInputIndex(inputType);

        if (_nextNodes[inputIndex] != null) throw new InvalidOperationException($"{inputType} 입력에 대한 다음 노드가 이미 존재합니다.");

        _nextNodes[inputIndex] = nextNode;
    }

    internal void SetCompletedCombo(ComboDefinition comboDefinition)
    {
        CompletedCombo = comboDefinition;
    }

    private static int GetInputIndex(AttackInputType inputType)
    {
        switch (inputType)
        {
            case AttackInputType.Light:
                return 0;
            case AttackInputType.Heavy:
                return 1;
            case AttackInputType.Grab:
                return 2;
            default:
                throw new ArgumentOutOfRangeException(nameof(inputType), inputType, null);
        }
    }
}
