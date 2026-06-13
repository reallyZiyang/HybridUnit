using Cysharp.Threading.Tasks;
using Game.Play.Adapters;
using Game.Play.Systems.Level.Command;
using UniKit.Asset;
using UniKit.UI;
using UnityEngine;

namespace Game.Play
{
    public class GameManager : MonoBehaviour
    {
        //private LoadingView m_LoadingView;

        private void Awake()
        {
            InitSettings();
            InitDontDestroyObjects();
            InitAssets();
        }

        private static void InitSettings()
        {
            Time.timeScale = 1f;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
        }

        private void InitDontDestroyObjects()
        {
            DontDestroyOnLoad(gameObject);
            DontDestroyOnLoad(Camera.main);
            DontDestroyOnLoad(GameObject.Find("UI Root"));
            DontDestroyOnLoad(GameObject.Find("EventSystem"));
        }

        private void InitAssets()
        {
            StartCoroutine(AssetManager.InitializeAsync(ret =>
            {
                if (ret)
                {
                    InitLocalization();
                }
            }));
        }

        private void InitLocalization()
        {
            InitLoading();
            // API.Assets.LoadAsset<LocalizationSettings>("Localization Settings", (_, settings) =>
            // {
            //     LocalizationManager.Initialize(settings);
                
            // });
        }

        private void InitLoading()
        {
            var um = UIManager.Instance;
            um.Context = GameContext.Instance;
                InitTables().Forget();
        }

        private async UniTask InitTables()
        {
            await API.InitConfig();
            await API.UI.InitConfig();
            await InitContext();
        }

        private async UniTask InitContext()
        {
            var context = GameContext.Instance;
            await context.InitAsync();
            context.SendCommand(new OpenMainMenuCommand());
        }
    }
}
