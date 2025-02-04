using Assets.Extensions;
using UnityEngine;

public class CameraRigSpawnerComponent : MonoBehaviour
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    public  ICameraRig CameraRig { get; private set; }

    private GameObject _spawnedRig;

    [SerializeField]
    private GameObject _dualCameraRig;

    [SerializeField]
    private GameObject _peppersGhostRig;

    void Start()
    {
        var config = emt_sdk.Settings.EmtSetting.FromConfig();
        if (config == null) Logger.ErrorUnity("Could not determine device type, not spawning any camera prefab");

        switch (config.Type)
        {
            case Naki3D.Common.Protocol.DeviceType.Ipw:
                _spawnedRig = Instantiate(_dualCameraRig, transform);
                CameraRig = _spawnedRig.GetComponent<DualCameraComponent>();
                break;
            case Naki3D.Common.Protocol.DeviceType.Pge:
                _spawnedRig = Instantiate(_peppersGhostRig, transform);
                CameraRig = _spawnedRig.GetComponent<PeppersGhostCameraComponent>();
                break;
        }
    }
}
