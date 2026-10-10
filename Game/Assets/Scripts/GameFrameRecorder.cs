using System.IO;
using System.Collections;
using UnityEngine;

namespace CatSimulator
{
    public sealed class GameFrameRecorder : MonoBehaviour
    {
        public string folder;
        public bool Recording=true;
        int frame;
        int previousCaptureRate;
        bool clockCaptured;
        IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            previousCaptureRate=Time.captureFramerate;
            Time.captureFramerate=30;clockCaptured=true;
            while(Recording)
            {
                yield return new WaitForEndOfFrame();
                if(!Recording) break;
                var image=ScreenCapture.CaptureScreenshotAsTexture();
                if(image==null) break;
                File.WriteAllBytes(Path.Combine(folder,(frame++).ToString("D5")+".jpg"),image.EncodeToJPG(90));Destroy(image);
            }
            RestoreClock();
        }
        void OnDestroy()=>RestoreClock();
        void RestoreClock()
        {if(clockCaptured) {Time.captureFramerate=previousCaptureRate;clockCaptured=false;}}
    }
}
