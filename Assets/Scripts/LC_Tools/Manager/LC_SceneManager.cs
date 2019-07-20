using DG.Tweening;
using System;
using System.Collections;
using System.Threading;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LC_Tools
{
    public class LC_SceneManager : Singleton<LC_SceneManager>
    {
        private static GameObject _canvasGo;
        private static Transform _progressPanel;
        private static Slider _progressSlider;

        public delegate void ScenePreloadEvent(string sceneName);

        public ScenePreloadEvent scenePreloadEvent;

        private bool IsLoading { get; set; }
        public string CurLoadingSceneName { get; private set; }

        public static void AddCallBacker(UnityAction<Scene, Scene> pcCallBacker)
        {
            SceneManager.activeSceneChanged += pcCallBacker;
        }

        public static void RemoveCallBacker(UnityAction<Scene, Scene> pcCallBacker)
        {
            SceneManager.activeSceneChanged -= pcCallBacker;
        }

        public static Transform GetCanvasTransform()
        {
            return _canvasGo.transform;
        }

        public void LoadScene(string sceneName, UnityAction<string> callback = null,
            UnityAction<string, float> progress = null)
        {
            if (IsLoading) return;
            IsLoading = true;
            InitDoParam();
            scenePreloadEvent?.Invoke(sceneName);
            Observable.FromCoroutine(token => LoadSceneAsync(token, sceneName, callback, progress)).DoOnError(exception => { Debug.LogError($"!!! LoadScene=>> {exception} "); })
                .TakeUntilDestroy(this)
                .Subscribe();
        }

        public void ChangeLoadProgressBG(string text, Sprite title)
        {
            InitDoParam();
            if (_progressPanel == null)
            {
                var go = Instantiate(LC_ResourceManager.Instance.GetPrefab("GameHall", "LoadProgress"), _canvasGo.transform, false);
                _progressPanel = go.transform;
                _progressSlider = _progressPanel.Find("Slider").GetComponent<Slider>();
                _progressSlider.value = 0;
                _progressPanel.localPosition = new Vector3(0, 0, 100);
                _progressPanel.SetAsLastSibling();
                _progressPanel.gameObject.SetActive(false);
            }

            _progressPanel.Find("Tips").GetComponent<Text>().text = text;
            var iconic = _progressPanel.Find("Background/Iconic");
            iconic.GetComponent<Image>().sprite = title;

            iconic.gameObject.SetActive(true);
            _progressPanel.gameObject.SetActive(true);
        }

        private void Awake()
        {
//            Debug.LogError($"===== LC_SceneManager : Awake =======");
            InitProgressPanel(gameObject);
            SetPersistent(gameObject);
        }

        private void Start()
        {
//            Debug.LogError($"===== LC_SceneManager : Start =======");
            this.UpdateAsObservable().Where(_ => _isDoComplete && _isWaitForDo).Subscribe(_ =>
            {
                _isWaitForDo = false;
                DynamicNum(_waitTarget);
            });
        }

        private float _nowProgress;
        private float _lastProgress;
        private bool _isDoComplete = true;
        private bool _isWaitForDo;
        private float _waitTarget;

        private void InitDoParam()
        {
            //Debug.LogWarning("== InitDoParam ==");
            _nowProgress = 0;
            _lastProgress = 0;
            _isDoComplete = true;
            _isWaitForDo = false;
            _waitTarget = 0.0f;
            SetSlider(0);
            DOTween.KillAll();
            DOTween.Init();
            //DOTween.useSafeMode = true;
        }

        private void DynamicNum(float target)
        {
            if (_progressPanel == null) return;
            if (_isDoComplete)
            {
                target = (float) Math.Round(target, 2);
                Debug.Log($"  DynamicNum  progress:[{target}]");
                _isDoComplete = false;
                var duration = (target - _nowProgress) / 4;
                var tweener = DOTween.To(() => _nowProgress, x => _nowProgress = x, (int) target, duration);
                tweener.OnUpdate(() =>
                {
                    if (_nowProgress - _lastProgress < 0.01) return;
                    _lastProgress = _nowProgress;
                    SetSlider(_lastProgress);
                });
                tweener.OnComplete(() =>
                {
                    _isDoComplete = true;
                    if (target >= 1.0f)
                    {
                        _progressPanel.gameObject.SetActive(false);
                    }
                });
            }
            else
            {
                _isWaitForDo = true;
                _waitTarget = target;
            }
        }

        private static void SetSlider(float percent)
        {
            if (_progressPanel != null && _progressSlider != null)
            {
                _progressSlider.value = percent;
            }
        }

        private static void InitProgressPanel(GameObject go)
        {
            _canvasGo = new GameObject("Scene_Canvas");
            _canvasGo.transform.SetParent(go.transform, false);

            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1;
            canvas.pixelPerfect = true;
            _canvasGo.AddComponent<GraphicRaycaster>();
            _canvasGo.AddComponent<CanvasScaler>();
//            _canvasGo.AddComponent<SetCanvasScalerScript>();
            Canvas.ForceUpdateCanvases();
        }

        private IEnumerator LoadSceneAsync(CancellationToken token, string scene_name, UnityAction<string> callback,
            UnityAction<string, float> progress)
        {
            Debug.Log(
                $"###  LoadSceneAsync  scene_name:[{scene_name}] call_func:[{callback?.Method.Name}] progress_func:[{progress?.Method.Name}] ###");
            CurLoadingSceneName = scene_name;
            yield return new WaitForEndOfFrame();
            var async = SceneManager.LoadSceneAsync(scene_name);
            async.allowSceneActivation = true;
            var tmpProgress = 0.0f;
            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    yield break;
                }

                if (async.progress - tmpProgress > 0.01)
                {
                    tmpProgress = (float) Math.Round(async.progress, 2);
                    DynamicNum(tmpProgress);
                    progress?.Invoke(scene_name, tmpProgress);
                }

                if (async.isDone) break;
                yield return null;
            }

            var model = scene_name.Split('_')[0];
            var preloadList = PreloadConfig.GetPreloadList(model);
            if (preloadList != null && preloadList.Count > 0)
            {
                LC_PoolManager.Instance.PreloadObject(model, preloadList);
            }

            callback?.Invoke(scene_name);
            IsLoading = false;
        }
    }
}