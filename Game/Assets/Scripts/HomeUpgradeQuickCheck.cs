using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
namespace CatSimulator
{
    public sealed class HomeUpgradeQuickCheck : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            if(!Environment.GetCommandLineArgs().Contains("--verify-home-physics"))return;
            var root=new GameObject("HomePhysicsCheck");DontDestroyOnLoad(root);root.AddComponent<HomeUpgradeQuickCheck>();
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();var output=args[Array.IndexOf(args,"--verification-output")+1];Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(.5f);
            if(args.Contains("--warmup-home-physics"))
            {
                var first=FindFirstObjectByType<CatController>();first.Automated=true;
                first.BeginInteraction(FindFirstObjectByType<CupInteractable>());while(first.IsBusy)yield return null;
                first.BeginInteraction(FindFirstObjectByType<CurtainInteractable>());while(first.IsBusy)yield return null;
            }
            SceneManager.LoadScene(ContentPreviewMenu.HomeScene);yield return new WaitForSecondsRealtime(1);
            var cat=FindFirstObjectByType<CatController>();var camera=FindFirstObjectByType<CatCamera>();cat.Automated=true;camera.Automated=true;
            VerificationRenderTarget.Initialize(camera.GetComponent<Camera>());
            yield return new WaitForEndOfFrame();var image=VerificationRenderTarget.Capture();
            if(image){File.WriteAllBytes(Path.Combine(output,"home-initial.png"),image.EncodeToPNG());Destroy(image);}
            var result=new HomeUpgradeVerification.Result();yield return HomeUpgradeVerification.Run(cat,camera,GameObject.Find("Home90Residence"),output,result);
            Application.Quit(result.passed?0:2);
        }
        void OnEnable()=>Application.logMessageReceived+=Log;
        void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string message,string stack,LogType type){if(type==LogType.Exception || type==LogType.Error)Application.Quit(2);}
    }
}
