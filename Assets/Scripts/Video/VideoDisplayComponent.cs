using System;
using emt_sdk.Scene;
using UnityEngine;
using UnityEngine.Video;
using Vector3 = UnityEngine.Vector3;

public class VideoDisplayComponent : MonoBehaviour
{
    public VideoPlayer VideoPlayer;
    public DualCameraComponent Camera;

    public void SetVolume(double volume)
    {
        // Only works for direct, single track audio
        VideoPlayer.SetDirectAudioVolume(0, (float)volume);
    }

    public void Play()
    {
        if (!VideoPlayer.isPlaying) VideoPlayer.Play();
    }
    
    public void Resize(VideoScene.VideoAspectRatioEnum aspectRatio)
    {
        var viewport = Camera.GetBoundaries(5);
        var videoAspect = VideoPlayer.width / (float)VideoPlayer.height;
        var viewportAspect = viewport.width / (float)viewport.height;
        
        // 10m is the default unity plane size, why it's not 1 is beyond me...
        const float planeSize = 10f;
        
        // TODO: Vertical mode
        switch (aspectRatio)
        {
            case VideoScene.VideoAspectRatioEnum.Stretch:
                transform.localScale = new Vector3(viewport.width / planeSize, 1, viewport.height / planeSize);
                break;
            case VideoScene.VideoAspectRatioEnum.FitInside:
                if (videoAspect > viewportAspect) // Wider 
                {
                    transform.localScale = new Vector3(viewport.width / planeSize, 1, (viewport.width / videoAspect) / planeSize);
                }
                else // Taller
                {
                    transform.localScale = new Vector3((viewport.height * videoAspect) / planeSize, 1, viewport.height / planeSize);
                }
                break;
            case VideoScene.VideoAspectRatioEnum.FitOutside:
                throw new NotImplementedException();
            default:
                throw new NotSupportedException();
        }
        
        transform.localPosition = new Vector3(viewport.center.x, viewport.center.y, 5);
    }
}
