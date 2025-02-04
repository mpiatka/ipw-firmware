using System;
using System.Collections;
using System.IO;
using System.Linq;
using emt_sdk.Events;
using emt_sdk.Generated.ScenePackage;
using emt_sdk.Scene;
using Naki3D.Common.Protocol;
using UnityEngine;
using UnityEngine.Video;
using Action = emt_sdk.Generated.ScenePackage.Action;

public class VideoLoader : MonoBehaviour
{
    [SerializeField]
    private VideoPlayer _player;

    [SerializeField] 
    private VideoDisplayComponent _display;

    public void Start()
    {
        StartCoroutine(DelayApply());
    }

    public IEnumerator Apply(VideoScene scene, string basePath)
    {
        if (ColorUtility.TryParseHtmlString(scene.BackgroundColor, out var backgroundColor) == false)
            throw new ArgumentException("Background color is not a valid HTML hex color string",
                nameof(scene.BackgroundColor));

        _display.RigSpawner.CameraRig.SetBackgroundColor(backgroundColor);
        
        var fileName = Path.Combine(basePath, scene.FileName);
        _player.clip = null;
        _player.url = $"file://{fileName}";
        _player.isLooping = scene.Loop;
        
        // Make sure we get the correct info for resizing
        _player.Prepare();
        yield return new WaitUntil(() => _player.isPrepared);
        
        if (scene.AutoStart) _player.Play();
        
        _display.Resize(scene.AspectRatio);
    }

    private IEnumerator DelayApply()
    {
        // Wait two frames for the camera transformation to apply
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        // Debug mode
        if (ExhibitConnectionComponent.ActivePackage == null)
        {
            _display.Resize(VideoScene.VideoAspectRatioEnum.FitInside);
            
            _player.Stop();
            EventManager.Instance.Actions.Add(new Action
            {
                Effect = "play",
                Type = TypeEnum.ValueTrigger,
                Mapping = new Mapping
                {
                    Source = "atom_1_pir_1",
                    Condition = Condition.Equals,
                    ThresholdType = ThresholdType.Integer,
                    Threshold = ((int)PirMovementEvent.MovementStarted).ToString()
                }
            });
            
            EventManager.Instance.Actions.Add(new Action
            {
                Effect = "setVolume",
                Type = TypeEnum.Value,
                Mapping = new Mapping
                {
                    Source = "raspi-1-ultrasonic-1",
                    InMin = 0,
                    InMax = 60,
                    OutMin = 1,
                    OutMax = 0,
                }
            });
            
            yield break;
        }
        
        var settings = ExhibitConnectionComponent.ActivePackage.Parameters.Settings;
        yield return Apply(new VideoScene
        {
            Loop = settings.Loop.Value,
            AspectRatio = (VideoScene.VideoAspectRatioEnum) Enum.Parse(typeof(VideoScene.VideoAspectRatioEnum), settings.AspectRatio.Value.ToString()),
            AutoStart = settings.AutoStart.Value,
            BackgroundColor = settings.BackgroundColor,
            FileName = settings.FileName,
            VideoEvents = settings.VideoEvents.Select(ve => new VideoScene.VideoEvent
            {
                Timestamp = (float) ve.Timestamp.Value,
                EventName = ve.EventName
            }).ToArray()
        }, ExhibitConnectionComponent.ActivePackage.DataRoot);
    }
}
