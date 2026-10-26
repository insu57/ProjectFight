using System;
using System.Collections.Generic;

public sealed class ComboGraph
{
    private readonly ComboNode _root = new ComboNode(null);

    public bool TryGetFirstNode(AttackInputType inputType, out ComboNode nextNode)
    {
        return _root.TryGetNextNode(inputType, out nextNode);
    }

    public static ComboGraph Create(ComboDefinition[] comboDefinitions)
    {
        if (comboDefinitions == null) throw new ArgumentNullException(nameof(comboDefinitions));

        ComboGraph graph = new ComboGraph();
        HashSet<int> comboIDs = new HashSet<int>();

        for (int i = 0; i < comboDefinitions.Length; i++)
        {
            ComboDefinition comboDefinition = comboDefinitions[i];

            ValidateComboDefinition(comboDefinition, i, comboIDs);
            graph.AddCombo(comboDefinition);
        }

        return graph;
    }

    private void AddCombo(ComboDefinition comboDefinition)
    {
        ComboNode currentNode = _root;
        ComboStep[] comboSteps = comboDefinition.ComboSteps;

        for (int i = 0; i < comboSteps.Length; i++)
        {
            ComboStep comboStep = comboSteps[i];

            if (currentNode.TryGetNextNode(comboStep.AttackInputType, out ComboNode nextNode))
            {
                // 같은 입력 경로에는 동일한 공격 데이터가 사용되어야 한다.
                if (nextNode.AttackDefinition != comboStep.AttackDefinition)
                {
                    throw new InvalidOperationException($"콤보 '{comboDefinition.ComboName}'의 {i + 1}번째 단계가 " +
                        $"기존 콤보와 충돌합니다. 같은 입력 경로에 서로 다른 AttackDefinition이 지정되었습니다.");
                }

                currentNode = nextNode;
                continue;
            }

            nextNode = new ComboNode(comboStep.AttackDefinition);
            currentNode.SetNextNode(comboStep.AttackInputType, nextNode);
            currentNode = nextNode;
        }

        if (currentNode.CompletedCombo)
        {
            throw new InvalidOperationException($"콤보 '{comboDefinition.ComboName}'과 " +
                $"'{currentNode.CompletedCombo.ComboName}'의 입력 순서가 같습니다.");
        }

        // 마지막 노드에는 해당 입력 경로로 완성되는 콤보를 기록한다.
        currentNode.SetCompletedCombo(comboDefinition);
    }

    private static void ValidateComboDefinition(ComboDefinition comboDefinition, int definitionIndex, HashSet<int> comboIds)
    {
        if (!comboDefinition) throw new InvalidOperationException($"{definitionIndex}번째 ComboDefinition이 비어 있습니다.");

        if (!comboIds.Add(comboDefinition.ComboID))
            throw new InvalidOperationException($"중복된 ComboID가 발견되었습니다: {comboDefinition.ComboID}");

        ComboStep[] comboSteps = comboDefinition.ComboSteps;

        if (comboSteps == null || comboSteps.Length == 0)
            throw new InvalidOperationException($"콤보 '{comboDefinition.ComboName}'에 단계가 없습니다.");

        for (int i = 0; i < comboSteps.Length; i++)
        {
            if (!comboSteps[i].AttackDefinition)
                throw new InvalidOperationException($"콤보 '{comboDefinition.ComboName}'의 {i + 1}번째 AttackDefinition이 비어 있습니다.");
        }
    }
}
