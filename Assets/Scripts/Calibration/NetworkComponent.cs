using System.Collections;
using System.Collections.Generic;
using emt_sdk.Settings;
using TMPro;
using UnityEngine;

public class NetworkComponent : MonoBehaviour
{
    public bool ShowWarning = false;

    [SerializeField]
    private TMP_InputField _hostname;

    [SerializeField]
    private GameObject _warning;

    [SerializeField]
    private DualCameraComponent _camera;
    
    private CommunicationSettings _communication = new CommunicationSettings();

    private void Start()
    {
        //_communication = _camera.Setting.Communication;
    }
    
    private void Update()
    {
        _warning.SetActive(ShowWarning);
        _communication.ContentHostname = _hostname.text;
        
        _hostname.Select();
        _hostname.ActivateInputField();
    }
}