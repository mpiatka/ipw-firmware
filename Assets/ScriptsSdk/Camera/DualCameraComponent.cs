using emt_sdk.Settings;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Collections;
using UnityEngine;
using Assets.Extensions;

public class DualCameraComponent : MonoBehaviour, ICameraRig
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    public TransformCameraComponent TopCamera;
    public TransformCameraComponent BottomCamera;
    public IPWSetting.IPWOrientation Orientation { get; set; }

    public IPWSetting Setting;

    Naki3D.Common.Protocol.DeviceType ICameraRig.DeviceType => Naki3D.Common.Protocol.DeviceType.Ipw;

    // TODO: Fix ortho sizing, lens shift is just physical worldspace shift scaled by orthosize
    // TODO: Disable culling on ortho scenes, leaves black squares for some reason

    void Awake()
    {
        for (int i = 1; i < Display.displays.Length; i++)
        {
            if (Display.displays[i].active) continue;
            //Display.displays[i].Activate();
            Display.displays[i].SetRenderingResolution(2048, 2048);
        }

        LoadSettings();
        StartCoroutine(ApplyDelay());
    }

    private IEnumerator ApplyDelay()
    {
        yield return new WaitForEndOfFrame();
        ApplySettings();
    }

    public void LoadSettings()
    {
        Setting = ProjectorTransfomartionSettingsLoader.LoadSettings();

        TopCamera.Settings = Setting.Displays;
        BottomCamera.Settings = Setting.Displays;
        Orientation = Setting.Orientation;
    }

    public Rect GetBoundaries(float? distance = null)
    {
        if (Setting.Orientation == IPWSetting.IPWOrientation.Single)
            return distance.HasValue ? TopCamera.GetCameraBoundries(distance.Value) : TopCamera.GetCameraBoundries();
        
        // Right in horizontal
        var topBoundary = distance.HasValue ? 
            TopCamera.GetCameraBoundries(distance.Value) : 
            TopCamera.GetCameraBoundries();
        
        // Left in horizontal
        var bottomBoundary = distance.HasValue ? 
            BottomCamera.GetCameraBoundries(distance.Value) : 
            BottomCamera.GetCameraBoundries();

        Vector2 size;
        switch (Setting.Orientation)
        {
            case IPWSetting.IPWOrientation.Vertical:
                size = new Vector2(bottomBoundary.width, topBoundary.yMax - bottomBoundary.yMin);
                return new Rect(topBoundary.x, bottomBoundary.yMin, size.x, size.y);
            case IPWSetting.IPWOrientation.Horizontal:
                size = new Vector2(topBoundary.xMax - bottomBoundary.x, bottomBoundary.height);
                return new Rect(bottomBoundary.x, bottomBoundary.y, size.x, size.y);
            case IPWSetting.IPWOrientation.Single:
                return new Rect(topBoundary.x, topBoundary.y, topBoundary.width, topBoundary.height);
            default:
                throw new NotSupportedException();
        }
    }

    public void ApplySettings()
    {
        switch (Setting.Orientation)
        {
            case IPWSetting.IPWOrientation.Vertical:
                TopCamera.Camera.lensShift = new Vector2(0, Setting.LensShift);
                BottomCamera.Camera.lensShift = new Vector2(0, -Setting.LensShift);
                
                TopCamera.Camera.gateFit = Camera.GateFitMode.Vertical;
                BottomCamera.Camera.gateFit = Camera.GateFitMode.Vertical;
                break;
            case IPWSetting.IPWOrientation.Horizontal:
                TopCamera.Camera.lensShift = new Vector2(Setting.LensShift, 0);
                BottomCamera.Camera.lensShift = new Vector2(-Setting.LensShift, 0);
                break;
            case IPWSetting.IPWOrientation.Single:
                TopCamera.Camera.lensShift = Vector2.zero;
                BottomCamera.Camera.lensShift = Vector2.zero;
                break;
            default:
                throw new NotImplementedException();
        }

        if (TopCamera.Camera.orthographic)
        {
            TopCamera.gameObject.transform.position = new Vector3(TopCamera.Camera.orthographicSize - Setting.LensShift, 0, 0);
            BottomCamera.gameObject.transform.position = new Vector3(-BottomCamera.Camera.orthographicSize + Setting.LensShift, 0, 0);
        }

        ProjectorTransformationPass.Vertical = Setting.Orientation == IPWSetting.IPWOrientation.Vertical;

        TopCamera.ApplySettings();
        BottomCamera.ApplySettings();
    }

    public void SaveSettings()
    {
        var userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configFile = Path.Combine(userFolder, "ipw.json");
        var json = JsonConvert.SerializeObject(Setting, Formatting.Indented);

        if (File.Exists(configFile))
        {
            var fileNameFriendlyDate = DateTime.Now.ToString("s").Replace(":", "");
            var backupPath = Path.Combine(userFolder, $"ipw_{fileNameFriendlyDate}.json");
            File.Move(configFile, backupPath);
        }

        File.WriteAllText(configFile, json);
        Logger.InfoUnity("Configuration saved");
    }

    public void SwapSettings()
    {
        (TopCamera.SettingIndex, BottomCamera.SettingIndex) = (BottomCamera.SettingIndex, TopCamera.SettingIndex);
    }

    public void SwapDisplays()
    {
        var firstDisplay = TopCamera.TargetDisplay;
        var secondDisplay = BottomCamera.TargetDisplay;

        TopCamera.TargetDisplay = secondDisplay;
        BottomCamera.TargetDisplay = firstDisplay;
    }

    public void SetBackgroundColor(Color color)
    {
        TopCamera.Camera.clearFlags = CameraClearFlags.SolidColor;
        TopCamera.Camera.backgroundColor = color;

        BottomCamera.Camera.clearFlags = CameraClearFlags.SolidColor;
        BottomCamera.Camera.backgroundColor = color;
    }

    public void ShowSkybox()
    {
        TopCamera.Camera.clearFlags = CameraClearFlags.Skybox;
        BottomCamera.Camera.clearFlags = CameraClearFlags.Skybox;
    }
}
