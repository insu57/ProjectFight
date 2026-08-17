using System;

public sealed class ComboNode
{
    private const int InputTypeCount = 3;

    private readonly ComboNode[] _nextNodes = new ComboNode[InputTypeCount];

    public AttackDefinition AttackDefinition { get; }
    public ComboDefinition CompletedCombo { get; private set; }
    public bool IsComboEnd => CompletedCombo != null;

    public ComboNode(AttackDefinition attackDefinition)
    {
        AttackDefinition = attackDefinition;
    }

    public bool TryGetNextNode(AttackInputType inputType, out ComboNode nextNode)
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
