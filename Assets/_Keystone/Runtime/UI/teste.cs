using Assets._Keystone.Runtime.Scripts.Networking;
using UnityEngine;
using UnityEngine.UI;

public class teste : MonoBehaviour
{
    [Header("Botões do Menu")]
    [SerializeField] private Button _leaveButton;

    private void Start()
    {
        if (_leaveButton != null)
        {
            _leaveButton.onClick.RemoveAllListeners();
            _leaveButton.onClick.AddListener(OnClick);
        }
    }

    private void OnClick()
    {
        SteamNetcodeBridge.Instance.LeaveSession();
    }
}