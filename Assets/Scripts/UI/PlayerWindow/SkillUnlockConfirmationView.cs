using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SkillUnlockConfirmationView : MonoBehaviour, ICancelHandler, IPointerDownHandler
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private TMP_Text _skillNameText;
    [SerializeField] private TMP_Text _costText;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private InputActionReference _cancelAction;

    private GameObject _previousSelectedObject;
    private int _pendingNodeId = -1;

    public bool IsOpen => _panel && _panel.activeSelf;

    public event Action<int> Confirmed;

    private void Awake()
    {
        SetPanelActive(false);
    }

    private void OnEnable()
    {
        if (_confirmButton)
        {
            _confirmButton.onClick.AddListener(HandleConfirm);
        }

        if (_cancelButton)
        {
            _cancelButton.onClick.AddListener(HandleCancel);
        }

        if (_cancelAction)
        {
            _cancelAction.action.performed += HandleCancelInput;
        }
    }

    private void OnDisable()
    {
        if (_confirmButton)
        {
            _confirmButton.onClick.RemoveListener(HandleConfirm);
        }

        if (_cancelButton)
        {
            _cancelButton.onClick.RemoveListener(HandleCancel);
        }

        if (_cancelAction)
        {
            _cancelAction.action.performed -= HandleCancelInput;
        }

        Hide();
    }

    public void Show(SkillTreeNodeRuntime node)
    {
        if (node == null || !_panel)
        {
            return;
        }

        if (!IsOpen)
        {
            _previousSelectedObject = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        }

        _pendingNodeId = node.NodeID;

        if (_skillNameText)
        {
            _skillNameText.SetText(node.Definition.NodeName);
        }

        if (_costText)
        {
            _costText.SetText("필요 포인트: {0}", node.Definition.RequiredPoints);
        }

        SetPanelActive(true);
        FocusConfirmButton();
    }

    public void Hide()
    {
        _pendingNodeId = -1;
        _previousSelectedObject = null;
        SetPanelActive(false);
    }

    public void OnCancel(BaseEventData eventData)
    {
        if (!IsOpen)
        {
            return;
        }

        Close();
        eventData.Use();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsOpen)
        {
            // 팝업 배경을 클릭해도 포커스가 팝업 밖으로 빠져나가지 않도록 한다.
            FocusConfirmButton();
        }
    }

    private void HandleConfirm()
    {
        if (!IsOpen || _pendingNodeId < 0)
        {
            return;
        }

        int nodeId = _pendingNodeId;
        Close();
        Confirmed?.Invoke(nodeId);
    }

    private void HandleCancel()
    {
        if (IsOpen)
        {
            Close();
        }
    }

    private void HandleCancelInput(InputAction.CallbackContext context)
    {
        if (IsOpen)
        {
            // UI Cancel 입력 시 플레이어 창은 유지하고 확인 팝업만 닫는다.
            Close();
        }
    }

    private void Close()
    {
        GameObject previousSelectedObject = _previousSelectedObject;
        Hide();

        if (EventSystem.current && previousSelectedObject)
        {
            EventSystem.current.SetSelectedGameObject(previousSelectedObject);
        }
    }

    private void FocusConfirmButton()
    {
        if (EventSystem.current && _confirmButton)
        {
            EventSystem.current.SetSelectedGameObject(_confirmButton.gameObject);
        }
    }

    private void SetPanelActive(bool isActive)
    {
        if (_panel && _panel.activeSelf != isActive)
        {
            _panel.SetActive(isActive);
        }
    }
}
