using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class StageMapRoomView : MonoBehaviour
{
    [Header("Images")]
    [SerializeField] private Image _roomImage;
    [SerializeField] private Image _currentLocationImage;
    [SerializeField] private Image _teleportImage;

    public Image RoomImage => _roomImage;

    private void Awake()
    {
        // 표시용 이미지가 지도 드래그와 스크롤 입력을 가로막지 않도록 한다. -> 추후 텔레포트에 이용할 마우스 확인??
        DisableRaycast(_roomImage);
        DisableRaycast(_currentLocationImage);
        DisableRaycast(_teleportImage);
        ResetIndicators();
    }

    public void SetCurrentLocation(bool isCurrentLocation)
    {
        SetImageVisible(_currentLocationImage, isCurrentLocation);
    }

    public void SetTeleportVisible(bool visible)
    {
        SetImageVisible(_teleportImage, visible);
    }

    public void ResetIndicators()
    {
        // 풀에서 다른 방에 재사용할 때 이전 표시 상태를 초기화한다.
        SetCurrentLocation(false);
        SetTeleportVisible(false);
    }

    private static void SetImageVisible(Image image, bool visible)
    {
        if (image.enabled != visible)
        {
            image.enabled = visible;
        }
    }

    private static void DisableRaycast(Image image)
    {
        if (image)
        {
            image.raycastTarget = false;
        }
    }
}
