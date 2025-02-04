using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Assets.Extensions;
using emt_sdk.Communication;
using emt_sdk.ScenePackage;
using emt_sdk.Settings;
using UnityEngine;

// TODO: Write custom inspector for states to make it easier to read
public class CalibrationManagerComponent : MonoBehaviour
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    public enum CalibrationStateEnum
    {
        Uninitialized,
        PhysicalAlignment,
        CornerAlignment,
        LensShift,
        ColorCorrection,
        Audio,
        NetworkConfiguration,
        NetworkCheck,
        Initialized
    }

    public enum NetworkStateEnum
    {
        Waiting,
        Valid,
        Invalid,
        VerificationDenied
    }

    public bool AlwaysCalibrate;

    private CalibrationStateEnum _calibrationState = CalibrationStateEnum.Uninitialized;
    private NetworkStateEnum _networkState = NetworkStateEnum.Waiting;
    
    [SerializeField] private ExhibitConnectionComponent _connection;
    [SerializeField] private NetworkComponent _network;
    [SerializeField] private AudioTestComponent _audioTest;

    // Unity can't serialize interfaces...
    private ICameraRig _camera;
    private Naki3D.Common.Protocol.DeviceType _deviceType => _camera.DeviceType;

    public List<GameObject> UiStates;

    private void UpdateUi(CalibrationStateEnum state)
    {
        // Steps can be empty for different devices
        if (UiStates[(int)_calibrationState] != null) UiStates[(int)_calibrationState].SetActive(false);
        if (UiStates[(int)state] != null) UiStates[(int)state].SetActive(true);
        
        _calibrationState = state;
    }

    private IEnumerator WaitForConfigurationReset()
    {
        // Give the rendering pipeline a while to get itself sorted
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        var deviceType = EmtSetting.FromConfig()?.Type ?? Naki3D.Common.Protocol.DeviceType.Unknown;
        var isCalibrated = deviceType switch
        {
            Naki3D.Common.Protocol.DeviceType.Pge => true,
            Naki3D.Common.Protocol.DeviceType.Ipw => ProjectorTransfomartionSettingsLoader.SettingsExists,
            _ => true
        };

        if (AlwaysCalibrate) isCalibrated = false;
        if (isCalibrated)
        {
            // Give the user few seconds to react
            yield return new WaitForSecondsRealtime(5f); 
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) isCalibrated = false;
        }

        if (isCalibrated)
        {
            ProjectorTransformationPass.SoftwareCalibration = false;

            // TODO: Validate with schema
            var loader = new PackageLoader(null);
            var startupPackage = loader
                .EnumeratePackages(false)
                .FirstOrDefault(p => Path.GetFileName(p.PackageDirectory) == _connection.Settings.StartupPackage);
            
            if (startupPackage != null)
            {
                _connection.SwitchScene(startupPackage);
            }
            else if (string.IsNullOrWhiteSpace(_connection.Settings.Communication.ContentHostname))
            {
                UpdateUi(CalibrationStateEnum.NetworkConfiguration);
            }
            else
            {
                Task.Run(VerifyNetwork);
                UpdateUi(CalibrationStateEnum.NetworkCheck);
            }
        }
        else
        {
            ProjectorTransformationPass.SoftwareCalibration = true;
            UpdateUi(CalibrationStateEnum.PhysicalAlignment);
        }
    }

    private async Task VerifyNetwork()
    {
        _connection.Connect();
        _networkState = NetworkStateEnum.Waiting;
        
        // Server probably refused connection or cannot connect at all
        if (ExhibitConnectionComponent.Connection == null ||
            ExhibitConnectionComponent.Connection.ConnectionState == ConnectionStateEnum.Disconnected)
        {
            _networkState = NetworkStateEnum.Invalid;
            return;
        }

        while (ExhibitConnectionComponent.Connection.ConnectionState <= ConnectionStateEnum.VerifyWait)
        {
            await Task.Delay(500);
        }

        if (ExhibitConnectionComponent.Connection.ConnectionState == ConnectionStateEnum.VerificationDenied)
        {
            _networkState = NetworkStateEnum.VerificationDenied;
            return;
        }
        
        _networkState = NetworkStateEnum.Valid;
    }

    private void Start()
    {
        StartCoroutine(WaitForConfigurationReset());
    }

    private void Update()
    {
        if (_camera == null)
        {
            var cameraObject = GameObject.FindGameObjectWithTag("MainCamera");
            if (cameraObject == null) return; // Waiting for camera spawn

            // Can't get an interface directly
            _camera = cameraObject.GetComponent<PeppersGhostCameraComponent>();
            if (_camera == null) _camera = cameraObject.GetComponent<DualCameraComponent>();
            if (_camera == null)
            {
                Logger.ErrorUnity("Main camera doesn't have an emt-sdk compatible display");
                throw new NotSupportedException("Main camera doesn't have an emt-sdk compatible display");
            }
        }

        switch (_calibrationState)
        {
            case CalibrationStateEnum.Uninitialized:
                break;
            case CalibrationStateEnum.PhysicalAlignment:
                if (Input.GetKeyDown(KeyCode.Return)) UpdateUi(_calibrationState + 1);
                if (_deviceType == Naki3D.Common.Protocol.DeviceType.Pge) UpdateUi(_calibrationState + 1);
                break;
            case CalibrationStateEnum.CornerAlignment:
                ProjectorTransformationPass.EnableBlending = false;
                if (Input.GetKeyDown(KeyCode.Return))
                {
                    UpdateUi(_calibrationState + 1);
                    ProjectorTransformationPass.EnableBlending = true;
                }
                if (_deviceType == Naki3D.Common.Protocol.DeviceType.Pge) UpdateUi(_calibrationState + 1);
                break;
            case CalibrationStateEnum.ColorCorrection:
                if (Input.GetKeyDown(KeyCode.Return)) UpdateUi(_calibrationState + 1);
                if (_deviceType == Naki3D.Common.Protocol.DeviceType.Pge) UpdateUi(_calibrationState + 1);
                break;
            case CalibrationStateEnum.LensShift:
                if (Input.GetKeyDown(KeyCode.Return))
                {
                    UpdateUi(_calibrationState + 1);
                    StartCoroutine(_audioTest.StartTest());
                    
                    _camera.SaveSettings(); // Save after changing all display related settings
                }
                if (_deviceType == Naki3D.Common.Protocol.DeviceType.Pge)
                {
                    StartCoroutine(_audioTest.StartTest());
                    UpdateUi(_calibrationState + 1);
                }
                break;
            case CalibrationStateEnum.Audio:
                if (_audioTest.Finished) UpdateUi(_calibrationState + 1);
                break;
            case CalibrationStateEnum.NetworkConfiguration:
                if (Input.GetKeyDown(KeyCode.Return))
                {
                    if (string.IsNullOrWhiteSpace(_connection.Settings.Communication.ContentHostname)) return;
                    
                    _network.ShowWarning = false;
                    _network.ShowVerification = false;
                    
                    _networkState = NetworkStateEnum.Waiting;
                    Task.Run(VerifyNetwork);
                    
                    UpdateUi(_calibrationState + 1);
                }
                break;
            case CalibrationStateEnum.NetworkCheck:
                switch (_networkState)
                {
                    case NetworkStateEnum.Invalid:
                        _network.ShowWarning = true;
                        UpdateUi(_calibrationState - 1);
                        break;
                    case NetworkStateEnum.VerificationDenied:
                        _network.ShowVerification = true;
                        UpdateUi(_calibrationState - 1);
                        break;
                    case NetworkStateEnum.Valid:
                        _connection.Settings.Save(); // Save network info
                        UpdateUi(_calibrationState + 1);
                        break;
                }
                break;
            case CalibrationStateEnum.Initialized:
                ProjectorTransformationPass.SoftwareCalibration = false;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        
        if (Input.GetKeyDown(KeyCode.Backspace) &&
            Input.GetKey(KeyCode.LeftShift) &&
            _calibrationState > CalibrationStateEnum.PhysicalAlignment &&
            _calibrationState <= CalibrationStateEnum.NetworkConfiguration)
        {
            UpdateUi(_calibrationState - 1);
        }

        // Checking the network again would just put us back where we were
        if (Input.GetKeyDown(KeyCode.Backspace) &&
            Input.GetKey(KeyCode.LeftShift) &&
            _calibrationState == CalibrationStateEnum.Initialized)
        {
            UpdateUi(CalibrationStateEnum.NetworkConfiguration);
        }
    }
}
