using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.U2D;
using static LC_Tools.AssetObjectData;
using Object = UnityEngine.Object;
#if ILPROJ
using ILRuntime.CLR.TypeSystem;
using ILRuntime.CLR.Method;
using ILRuntime.CLR.Utils;
using ILRuntime.Runtime.Intepreter;
using ILRuntime.Runtime.Stack;
using ILRuntime.Runtime.Enviorment;
#endif
#if UNITY_EDITOR
using UnityEditor;

#endif

namespace LC_Tools
{
    public class LC_ResourceManager : Singleton<LC_ResourceManager>
    {
        public static readonly string SERVER_ADDRESS = AddressConfig.GetResourceServerIP();
        private const string SERVER_SUB_FOLDER = @"Xxxx/Update/";
        private const float CACHE_KEEP_TIME = 0.0f;
        private const float PROGRESS_STEP = 0.01f;
        private const int DOWN_TIMEOUT = 1800; //s
        private bool RUNTIME_DEBUG;
        private const RunningModelEnum runningModel = AddressConfig.RUNNING_MODEL_ENUM;

        private static AssetBundleManifest _manifest;
        private static bool _manifestLoading;
        private static bool _manifestLoaded;

        private static float _lastTime;
        private const int ProcessLimit = 7;
        private static int _downCount;
        private static int _readCount;
        private static int _loadCount;

        private readonly LinkedList<DownLoadStruct> _assetDownloadList = new LinkedList<DownLoadStruct>();
        private readonly LinkedList<DownLoadStruct> _assetReadList = new LinkedList<DownLoadStruct>();
        private readonly LinkedList<LoadStruct> _assetLoadList = new LinkedList<LoadStruct>();
        private readonly Dictionary<string, AssetObjectData> _bundleCacheDict = new Dictionary<string, AssetObjectData>();
        private readonly Dictionary<string, ModelProgressClass> _modelProgressDict = new Dictionary<string, ModelProgressClass>();

#if UNITY_EDITOR
        private readonly Dictionary<string, Dictionary<ResourceType, List<string>>> _editorFileDict =
            new Dictionary<string, Dictionary<ResourceType, List<string>>>();
#endif

#if ILPROJ
        private List<string> _loadedILDLL = new List<string>();
#endif

        private struct DownLoadStruct
        {
            public string name;
            public Hash128 hash;
            public UnityAction<AssetBundle> callback;
            public UnityAction<string, float> progress;
        }

        private struct LoadStruct
        {
            public AssetBundle bundle;
            public string model_name;
            public string resource_name;
            public UnityAction<Object> callback;
            public UnityAction<string, float> progress;
        }

        private class ModelProgressClass
        {
            public string name;
            public int count;
            public readonly Dictionary<string, float> sub_progress = new Dictionary<string, float>();
            public UnityAction<string, float> progress;
            public float progress_value;
            public UnityAction<string> completed;
            public bool downloaded;
        }

        public enum RunningModelEnum
        {
            Editor,
            Local,
            Remote
        }

        public enum ResourceType
        {
            scenes,
            prefabs,
            spriteatlas,
            images,
            sounds,
            il,
            others,
        }

        public static bool IsManifestLoaded => runningModel == RunningModelEnum.Editor || _manifest != null;

        public void InitManifest()
        {
            if (runningModel == RunningModelEnum.Editor) return;
            if (_manifestLoaded || _manifestLoading) return;
            _manifestLoading = true;

            var defaultCache = Caching.defaultCache;

            var manifestPath = new StringBuilder(defaultCache.path).Append(Path.AltDirectorySeparatorChar)
                .Append(GetBuildTarget()).ToString();
            if (Directory.Exists(manifestPath))
            {
                Debug.LogWarning($"--- Delete Manifest {manifestPath} ---");
                Directory.Delete(manifestPath, true);
            }

            var assetStruct = new DownLoadStruct
            {
                name = GetBuildTarget(),
                hash = new Hash128(),
                callback = bundle =>
                {
                    _manifest = bundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
                    _manifestLoaded = true;
                    bundle.Unload(false);
                }
            };

            Observable.FromMicroCoroutine<AssetBundle>((observer, token) =>
                    DownAssetBundleAsync(observer, token, assetStruct, true))
                .OnErrorRetry()
                .TakeUntilDestroy(this)
                .Subscribe(CacheAssetBundle);

            this.LateUpdateAsObservable().Where(_ => IsManifestLoaded).OnErrorRetry().TakeUntilDestroy(this).Subscribe(_ =>
                {
                    RunDownloadList();
                    RunReadList();
                    RunLoadList();
                }
            );

            this.LateUpdateAsObservable().Sample(TimeSpan.FromSeconds(5)).Where(_ => CACHE_KEEP_TIME > 0.0f)
                .TakeUntilDestroy(this)
                .Subscribe(_ =>
                {
                    var nowTime = Time.realtimeSinceStartup;
                    foreach (var asset in _bundleCacheDict)
                    {
                        var aod = asset.Value;
                        if (aod.States == AssetStates.Loaded && nowTime - aod.Last_time >= CACHE_KEEP_TIME)
                        {
                            aod.ClearAssetData();
                        }
                    }
                });
        }

        public AssetStates CheckLoadModel(string modelName, UnityAction<string, float> progress = null, UnityAction<string> completed = null)
        {
            AssetStates result;
            if (!IsManifestLoaded)
            {
                Debug.LogError("!!!! Manifest is not loaded yet !!!!");
            }

            var lowName = modelName.ToLowerInvariant();
            if (runningModel != RunningModelEnum.Editor)
            {
                result = CheckModelState(modelName);
                Debug.LogWarning($"### CheckLoadModel name:[{modelName}] state:[{result.ToString()}]");

                if (result < AssetStates.DownLoading)
                {
                    DownloadOrReadByModel(modelName, progress, completed);
                }
                else if (result == AssetStates.WaitLoad)
                {
                    DownloadOrReadByModel(modelName, progress, completed, false);
                }
                else if (result == AssetStates.DownLoading)
                {
                    if (_modelProgressDict.ContainsKey(lowName))
                    {
                        _modelProgressDict[lowName].progress = progress;
                    }
                }
                else if (result >= AssetStates.DownLoaded)
                {
                    progress?.Invoke(lowName, 1.0f);
                    completed?.Invoke(lowName);
                }
            }
            else
            {
#if ILPROJ
                bool exist_dll = false;
                for (int i = 0; i < _loadedILDLL.Count; i++)
                {
                    string name = _loadedILDLL[i];
                    if (name.Contains(modelName))
                    {
                        exist_dll = true;
                        break;
                    }
                }

                if (!exist_dll)
                {
                    Debug.Log("CheckLoadModel() eGameRunningModel_Editor LoadDll:" + modelName);
                    _loadedILDLL.Add(modelName);
                    LC_RuntimeManager.LoadAssembly(modelName);
                }
#endif
                progress?.Invoke(lowName, 1.0f);
                completed?.Invoke(lowName);
                result = AssetStates.Loaded;
            }

            return result;
        }

        public float GetModelProgress(string modelName, bool download = true)
        {
            var lowName = modelName.ToLowerInvariant();
            if (_modelProgressDict.ContainsKey(lowName))
            {
                var model = _modelProgressDict[lowName];
                if (download && model.downloaded) return 1.0f;
                return _modelProgressDict[lowName].progress_value;
            }

            var states = CheckModelState(modelName);
            return states >= AssetStates.DownLoaded ? 1.0f : -1.0f;
        }

        public AssetStates CheckModelState(string modelName)
        {
#if UNITY_EDITOR
            if (runningModel == RunningModelEnum.Editor) return AssetStates.Loaded;
#endif
            var result = AssetStates.Loaded;
            var modules = GetModulesByModelName(modelName);
            foreach (var t in modules)
            {
                var moduleStates = CheckDependenciesStates(t);
                if (moduleStates < result)
                {
                    result = moduleStates;
                }
            }

            return modules.Count == 0 ? AssetStates.None : result;
        }

        public GameObject GetPrefab(string modelName, string prefabName,
            UnityAction<GameObject> callback = null, UnityAction<string, float> progress = null)
        {
            return LoadAssetFromDict(modelName, prefabName, ResourceType.prefabs, callback, progress);
        }

        public Sprite GetTexture(string modelName, string spriteName, UnityAction<Sprite> callback = null,
            UnityAction<string, float> progress = null)
        {
            return LoadAssetFromDict(modelName, spriteName, ResourceType.images, callback, progress);
        }

     public Texture2D GetTexture2D(string modelName, string textureName,
            UnityAction<Texture2D> callback = null, UnityAction<string, float> progress = null)
        {
            return LoadAssetFromDict(modelName, textureName, ResourceType.images, callback, progress);
        }

        public SpriteAtlas GetSpriteAtlas(string modelName, string textureName,
            UnityAction<SpriteAtlas> callback = null, UnityAction<string, float> progress = null)
        {
            return LoadAssetFromDict(modelName, textureName, ResourceType.spriteatlas, callback, progress);
        }
        
        public AudioClip GetAudioClip(string modelName, string audioName,
            UnityAction<AudioClip> callback = null, UnityAction<string, float> progress = null)
        {
            return LoadAssetFromDict(modelName, audioName, ResourceType.sounds, callback, progress);
        }

        public T GetObject<T>(string modelName, string resourceName,
            UnityAction<T> callback = null, UnityAction<string, float> progress = null)
        {
            var type = typeof(T);
            if (type == typeof(GameObject))
            {
                if (callback == null) return (T) (object) GetPrefab(modelName, resourceName, null, progress);
                GetPrefab(modelName, resourceName, obj => { callback.Invoke((T) (object) obj); }, progress);
            }

            if (type == typeof(Sprite))
            {
                if (callback == null) return (T) (object) GetTexture(modelName, resourceName, null, progress);
                GetTexture(modelName, resourceName, obj => { callback.Invoke((T) (object) obj); }, progress);
            }

            if (type == typeof(Texture2D))
            {
                if (callback == null) return (T) (object) GetTexture2D(modelName, resourceName, null, progress);
                GetTexture2D(modelName, resourceName, obj => { callback.Invoke((T) (object) obj); }, progress);
            }

            if (type == typeof(SpriteAtlas))
            {
                if (callback == null) return (T) (object) GetSpriteAtlas(modelName, resourceName, null, progress);
                GetSpriteAtlas(modelName, resourceName, obj => { callback.Invoke((T) (object) obj); }, progress);
            }

            if (type != typeof(AudioClip)) return default;
            if (callback == null) return (T) (object) GetAudioClip(modelName, resourceName, null, progress);
            GetAudioClip(modelName, resourceName, obj => { callback.Invoke((T) (object) obj); }, progress);
            return default;
        }

        public void SetKeepModel(string modelName, ResourceType rt, bool isKeep)
        {
            var assetName = modelName.ToLower() + "_" + rt;
            CheckDictValid(assetName);
            _bundleCacheDict[assetName].isKeep = isKeep;
        }

        public void UnloadGameModelExceptIL()
        {
            var list = _bundleCacheDict.Values.ToList();
            var basic = new Regex(@"^((gamehall)|(common))_.+");
            var save = new Regex(@"^.+_(il)"); //todo
            foreach (var single in list)
            {
                if (string.IsNullOrEmpty(single.Name) || single.Name.Split('_').Length < 2) continue;
                if (single.isKeep || basic.IsMatch(single.Name) || save.IsMatch(single.Name)) continue;
//                Debug.LogError($"&&&&&& Clear : {single.Name} &&&&&&");
                single.ClearAssetData(true);
                _bundleCacheDict.Remove(single.Name);
            }

            GC.Collect();
            Resources.UnloadUnusedAssets();
        }


        private T LoadAssetFromDict<T>(string modelName, string resourceName, ResourceType rt,
            UnityAction<T> callback, UnityAction<string, float> progress)
        {
//            Debug.Log(
//                $"^^^ LoadAssetFromDict model:[{modelName}] resource:[{resourceName}] Type:[{rt.ToString()}] callback:[{callback?.Method.Name}] progress:[{progress?.Method.Name}] ^^^");
#if UNITY_EDITOR
            if (runningModel == RunningModelEnum.Editor)
            {
                return GetResourceWithEditorModel(modelName, resourceName, rt, callback, progress);
            }
#endif
            var assetName = modelName.ToLowerInvariant() + "_" + rt;
            var loadStatus = GetAssetStates(assetName);

            if (loadStatus == AssetStates.Loaded)
            {
                var bundle = _bundleCacheDict[assetName].Bundle;
                return LoadObject(bundle, modelName, resourceName, callback, progress);
            }

            DownloadOrReadList(assetName, bundle =>
            {
                if (callback != null)
                {
                    LoadObject(bundle, modelName, resourceName, callback, progress);
                }
            });
            return default;
        }

        private T LoadObject<T>(AssetBundle bundle, string modelName, string resourceName,
            UnityAction<T> callback, UnityAction<string, float> progress)
        {
            if (callback == null)
            {
                var fullPath = CheckFullPathInAssetBundle(bundle, modelName, resourceName);
                if (string.IsNullOrEmpty(fullPath)) return default;
                var asset = bundle.LoadAsset(fullPath);
                return ChangeSpriteObject<T>(asset);
            }

            var ls = new LoadStruct
            {
                bundle = bundle,
                model_name = modelName,
                resource_name = resourceName,
                callback = obj => { callback.Invoke(ChangeSpriteObject<T>(obj)); },
                progress = progress,
            };
            _assetLoadList.AddLast(ls);

            return default;
        }

        private static string CheckFullPathInAssetBundle(AssetBundle bundle, string modelName, string resourceName)
        {
            var regexStr = new StringBuilder(@"Assets/").Append(modelName).Append(@"/.*/").Append(resourceName)
                .Append(@"\..*").ToString().ToLowerInvariant();

            var count = 0;
            var alls = bundle.GetAllAssetNames();
            string fullPath = null;
            foreach (var single in alls)
            {
                if (!Regex.IsMatch(single, regexStr)) continue;
                fullPath = single;
                count++;
            }

            if (count > 1 || string.IsNullOrEmpty(fullPath))
            {
                //Debug.LogError(string.Format("!!!! LC_ResourceManager Find  [{0}]:Resources by Pattern:[{1}]", count, regex_str));
                Debug.LogWarning(
                    $"!!!! LC_ResourceManager Find [{count}]: Model:[{modelName}] Resource:[{resourceName}] Path:[{fullPath}]");
            }

            return fullPath;
        }

#if UNITY_EDITOR
        private T GetResourceWithEditorModel<T>(string modelName, string resourceName, ResourceType rt,
            UnityAction<T> callback, UnityAction<string, float> progress)
        {
            if (!_editorFileDict.ContainsKey(modelName))
            {
                _editorFileDict.Add(modelName, new Dictionary<ResourceType, List<string>>());
            }

            if (!_editorFileDict[modelName].ContainsKey(rt))
            {
                var filePattern = "";
                //string file_pattern = "*.spriteatlas|*.prefab|*.unity|*.ogg|*.mp3|*.mp4|*.wav|*.png|*.jpg|*.jpeg|*.tga";
                switch (rt)
                {
                    case ResourceType.scenes:
                        filePattern = @"*.unity";
                        break;
                    case ResourceType.prefabs:
                        filePattern = @"*.prefab";
                        break;
                    case ResourceType.images:
                        filePattern = @"*.png|*.jpg|*.jpeg|*.tga";
                        break;
                    case ResourceType.sounds:
                        filePattern = @"*.ogg|*.mp3|*.mp4|*.wav";
                        break;
                    case ResourceType.spriteatlas:
                        filePattern = @"*.spriteatlas";
                        break;
                }

                var rootPath = Application.dataPath + Path.AltDirectorySeparatorChar + modelName;
                var splitPattern = filePattern.Split('|');
                var tmpPath = (from pattern in splitPattern
                    from item in Directory.GetFiles(rootPath, pattern, SearchOption.AllDirectories)
                    select item.Substring(item.IndexOf("Assets", StringComparison.Ordinal))).ToList();

                _editorFileDict[modelName].Add(rt, tmpPath);
            }

            var fixName = resourceName.Replace(@"\", @"/") + @".";
            if (_editorFileDict.ContainsKey(modelName) && _editorFileDict[modelName].ContainsKey(rt))
            {
                var resourcePaths = _editorFileDict[modelName][rt];
                foreach (var t in resourcePaths)
                {
                    var path = t.Replace(@"\", @"/");
                    if (path.IndexOf(fixName, StringComparison.Ordinal) <= 0) continue;
                    var obj = AssetDatabase.LoadMainAssetAtPath(path);

                    var ret = ChangeSpriteObject<T>(obj);
                    if (callback == null)
                        return ret;

                    callback.Invoke(ret);
                    progress?.Invoke(resourceName, 1.0f);
                    return ret;
                }
            }

            Debug.LogError($"== Cant Find Resource Type:[{rt.ToString()}] Model:[{modelName}] Name:[{resourceName}] ==");
            return default;
        }
#endif

        private static T ChangeSpriteObject<T>(Object obj)
        {
            if (typeof(T) != typeof(Sprite)) return (T) (object) obj;
            var tex = obj as Texture2D;
            if (tex == null) return (T) (object) (obj as Sprite);
            var ret = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero);
            ret.name = tex.name;
            return (T) (object) ret;
        }

        private void RunDownloadList()
        {
            if (_downCount >= ProcessLimit || _assetDownloadList.Count == 0) return;
            ++_downCount;
            var bundleStruct = _assetDownloadList.First;
            _assetDownloadList.RemoveFirst();
            Observable.FromMicroCoroutine<AssetBundle>((observer, token) => DownAssetBundleAsync(observer, token, bundleStruct.Value))
                .DoOnError(ex =>
                {
                    Observable.Timer(TimeSpan.FromSeconds(3)).TakeUntilDestroy(this).Subscribe(_ =>
                    {
//                        Debug.LogError($"== RunDownloadList DoOnError:[{bundleStruct.Value.name}] ==");
                        _assetDownloadList.AddLast(bundleStruct);
                        --_downCount;
                    });
                })
                .TakeUntilDestroy(this)
                .Subscribe(bundle =>
                {
                    --_downCount;
                    CacheAssetBundle(bundle);
                });
        }

        private void RunReadList()
        {
            if (_readCount >= ProcessLimit || _assetReadList.Count == 0) return;
            ++_readCount;
            var bundleStruct = _assetReadList.First;
            _assetReadList.RemoveFirst();
            Observable.FromMicroCoroutine<AssetBundle>((observer, token) =>
                    DownAssetBundleAsync(observer, token, bundleStruct.Value))
                .DoOnError(ex =>
                {
                    Observable.Timer(TimeSpan.FromSeconds(3)).TakeUntilDestroy(this).Subscribe(_ =>
                    {
                        _assetReadList.AddLast(bundleStruct);
                        --_readCount;
                    });
                })
                .TakeUntilDestroy(this)
                .Subscribe(bundle =>
                {
                    --_readCount;
                    CacheAssetBundle(bundle);
                });
        }

        private IDisposable _loadCoroutine;

        private void RunLoadList()
        {
            if (_loadCount >= ProcessLimit || _assetLoadList.Count == 0) return;
            ++_loadCount;
            var listNode = _assetLoadList.First;
            _assetLoadList.RemoveFirst();
            _loadCoroutine = Observable.FromMicroCoroutine(token =>
                    LoadAssetAsync(token, listNode.Value))
                .TakeUntilDestroy(this)
                .Subscribe();
        }

        private void InterruptLoad(string sceneName)
        {
            _assetLoadList.Clear();
            _loadCoroutine?.Dispose();
        }

        private readonly Regex _ilReg = new Regex(@".*?_" + ResourceType.il);
        private readonly Regex _dlcReg = new Regex(@"_dlc\.txt$");
        private readonly Regex _plcReg = new Regex(@"_plc\.txt$");

        private void CacheAssetBundle(AssetBundle bundle)
        {
            if (bundle == null) return;
            var bundleName = bundle.name;
            SetAssetStates(bundleName, AssetStates.Loading);
//            Debug.Log($"=== 对{bundleName}资源包进行保存");
            CheckDictValid(bundleName);
            SetAssetStates(bundleName, AssetStates.Loaded);
            if (_bundleCacheDict[bundleName].Bundle != null) return;
            _bundleCacheDict[bundleName].Bundle = bundle;
#if ILPROJ
            if (!_ilReg.IsMatch(bundleName)) return;
            var alls = bundle.GetAllAssetNames();
            byte[] dll_data = null;
            byte[] pdb_data = null;
            foreach (var name in alls)
            {
                if (_dlcReg.IsMatch(name))
                {
                    dll_data = bundle.LoadAsset<TextAsset>(name).bytes;
                }
                else if (RUNTIME_DEBUG && _plcReg.IsMatch(name))
                {
                    pdb_data = bundle.LoadAsset<TextAsset>(name).bytes;
                }
            }

            LC_RuntimeManager.LoadAssembly(dll_data, pdb_data);
            bundle.Unload(false);
            Debug.Log($"=== Loaded IL Script: {bundleName}");
#endif
        }

        private static string GetBuildTarget()
        {
            var platform = "";
#if UNITY_ANDROID
            platform = RuntimePlatform.Android.ToString();
#elif UNITY_STANDALONE_WIN
            platform = @"StandaloneWindows64";
#elif UNITY_IOS
            //platform = RuntimePlatform.IPhonePlayer.ToString();
            platform = @"iOS";
#endif
            return platform;
        }

        private static string GetUrlPath(DownLoadStruct downInfo, bool isManifest)
        {
            var urlSb = new StringBuilder();
            if (RunningModelEnum.Remote == runningModel)
            {
                urlSb.Append(SERVER_ADDRESS).Append(SERVER_SUB_FOLDER);
            }
            else
            {
#if UNITY_EDITOR_OSX
                urlSb.Append(SERVER_ADDRESS).Append("StreamingAssets/");
#elif UNITY_EDITOR
                urlSb.Append(Application.streamingAssetsPath).Append(Path.DirectorySeparatorChar);
#else
#if UNITY_ANDROID
                urlSb.Append("jar:file://").Append(Application.dataPath).Append("!/assets/");
#elif UNITY_IOS
            urlSb.Append(Application.dataPath).Append("/Raw/");
#elif UNITY_STANDALONE_WIN
            urlSb.Append(Application.streamingAssetsPath).Append(Path.DirectorySeparatorChar);
#endif
#endif
            }

            urlSb.Append(GetBuildTarget()).Append(Path.DirectorySeparatorChar).Append(downInfo.name);
            if (runningModel == RunningModelEnum.Local) return urlSb.ToString().Replace(@"\", @"/");
            if (isManifest)
            {
                urlSb.Append("?").Append(Guid.NewGuid());
            }
            else
            {
                urlSb.Append("?").Append(downInfo.hash);
            }

            return urlSb.ToString().Replace(@"\", @"/");
        }

        private float _lastCheckSpeedTime;
        private ulong _lastDownloadBytes;

        private void TestDownloadSpeed(UnityWebRequest request)
        {
            var nowTime = Time.realtimeSinceStartup;
            var timeInterval = nowTime - _lastCheckSpeedTime;
            if (!(timeInterval >= 1f)) return;
            var nowBytes = request.downloadedBytes;
            var speedBySec = (nowBytes - _lastDownloadBytes) / 1024;
            _lastDownloadBytes = nowBytes;
            _lastCheckSpeedTime = nowTime;
            Debug.LogWarning($"######### DownLoad Speed is [{speedBySec / timeInterval}]KB By Sec #########");
        }

        private static IEnumerator LoadAssetAsync(CancellationToken token, LoadStruct loadStruct)
        {
            var fullPath =
                CheckFullPathInAssetBundle(loadStruct.bundle, loadStruct.model_name, loadStruct.resource_name);
            if (string.IsNullOrEmpty(fullPath)) yield break;
            var request = loadStruct.bundle.LoadAssetAsync(fullPath);
            var loadProgress = 0.0f;
            while (true)
            {
                if (token.IsCancellationRequested)
                    yield break;
                if (request.progress - PROGRESS_STEP > loadProgress && request.progress < 1.0f)
                {
                    //Debug.LogWarning(string.Format("!!! LoadAssetAsync Download name:[{0}] progress:[{1}] !!!", loadStruct.model_name, load_progress));
                    loadProgress = request.progress;
                    loadStruct.progress?.Invoke(loadStruct.model_name, loadProgress);
                }

                if (request.isDone) break;
                yield return null;
            }

            loadStruct.progress?.Invoke(loadStruct.model_name, loadProgress);
            loadStruct.callback?.Invoke(request.asset);

            --_loadCount;
        }

        private void DownloadOrReadByModel(string modelName, UnityAction<string, float> progress, UnityAction<string> completed, bool isDL = true)
        {
            var modules = GetModulesByModelName(modelName);
            var lowName = modelName.ToLowerInvariant();
            ModelProgressClass mpc;
            if (_modelProgressDict.ContainsKey(lowName))
            {
                mpc = _modelProgressDict[lowName];
                mpc.count = modules.Count - 1;
                mpc.progress = progress;
                mpc.completed = completed;
                mpc.sub_progress.Clear();
            }
            else
            {
                mpc = new ModelProgressClass
                {
                    name = lowName,
                    count = modules.Count,
                    progress = progress,
                    completed = completed
                };
                _modelProgressDict.Add(lowName, mpc);
            }

            foreach (var t in modules)
            {
                DownloadOrReadList(t, null, ProcessModelProgress, isDL);
            }
        }

        private void ProcessModelProgress(string moduleName, float progress)
        {
            var splitName = moduleName?.Split('_');
            if (splitName.Length <= 0) return;
            var modelName = splitName[0];
            if (!_modelProgressDict.ContainsKey(modelName)) return;
            var mps = _modelProgressDict[modelName];
            if (mps.sub_progress.ContainsKey(moduleName))
            {
                mps.sub_progress[moduleName] = progress;
            }
            else
            {
                _modelProgressDict[modelName].sub_progress.Add(moduleName, progress);
            }

            var totalProgress = 0.0f;
            foreach (var item in mps.sub_progress)
            {
                totalProgress += item.Value;
//                Debug.Log($"### Loading Resource Name: [{item.Key}] Progress:[{item.Value}]");
            }

            var progressValue = totalProgress / mps.count;
            mps.progress?.Invoke(modelName, progressValue);
            mps.progress_value = progressValue;
            if (!(progressValue >= 1.0f)) return;
            mps.downloaded = true;
            mps.completed?.Invoke(modelName);
            //Debug.LogWarning(string.Format("--5ProcessModelProgress model:[{0}] progress:[{1}]  module:[{2}] pro:[{3}]", model_name, (total_progress / (float)mps.count), module_name, progress));
        }

        private static List<string> GetModulesByModelName(string modelName)
        {
            var regexStr = new Regex(@"^" + modelName.ToLowerInvariant() + @"_.*");
            var manifests = _manifest.GetAllAssetBundles();
            return manifests.Where(moduleName => regexStr.IsMatch(moduleName)).ToList();
        }

        private void DownloadOrReadList(string assetName, UnityAction<AssetBundle> callback = null, UnityAction<string, float> progress = null, bool isDL = true)
        {
            var assetStates = GetAssetStates(assetName);
            if (assetStates >= AssetStates.DownLoading && assetStates != AssetStates.WaitLoad) return;
            foreach (var item in _assetDownloadList)
            {
                if (assetName == item.name) return;
            }

            var depends = _manifest.GetAllDependencies(assetName);
            foreach (var t in depends)
            {
                //Debug.Log(string.Format("-> -> Find dependencies [{0}] -> ->", t));
                DownloadOrReadList(t, callback, progress, isDL);
            }

            var checkList = _assetReadList;
            if (!isDL)
            {
                checkList = _assetReadList;
            }

            foreach (var down in checkList)
            {
                if (down.name.Equals(assetName)) return;
            }

            var asset = new DownLoadStruct
            {
                name = assetName,
                hash = _manifest.GetAssetBundleHash(assetName),
                callback = callback,
                progress = progress
            };

            //Debug.Log(string.Format("+++++ Add [{0}]  To Queue  +++++", asset_name));
            if (isDL)
            {
                _assetDownloadList.AddLast(asset);
            }
            else
            {
                _assetReadList.AddLast(asset);
            }
        }

        private AssetStates GetAssetStates(string assetName)
        {
            var maniHash = _manifest.GetAssetBundleHash(assetName);
            if (!maniHash.isValid)
            {
                Debug.Log($"== No Resource By Manifest: [{assetName}] ==");
                return AssetStates.None;
            }

            foreach (var loadStruct in _assetDownloadList)
            {
                if (assetName.Equals(loadStruct.name)) return AssetStates.DownLoading;
            }

            foreach (var loadStruct in _assetLoadList)
            {
                if (assetName.Equals(loadStruct.resource_name)) return AssetStates.Loading;
            }

            if (_bundleCacheDict.ContainsKey(assetName))
            {
                return _bundleCacheDict[assetName].States;
            }

            return !Caching.IsVersionCached(assetName, maniHash) ? AssetStates.Update : AssetStates.WaitLoad;
        }

        private void SetAssetStates(string assetName, AssetStates states)
        {
            CheckDictValid(assetName);
            _bundleCacheDict[assetName].States = states;
        }

        private AssetStates CheckDependenciesStates(string assetName)
        {
            var result = AssetStates.Loaded;
            var depends = _manifest.GetAllDependencies(assetName);
            foreach (var subName in depends)
            {
                var subStates = CheckDependenciesStates(subName);
                if (subStates < result)
                {
                    result = subStates;
                }
            }

            var now = GetAssetStates(assetName);
            return now < result ? now : result;
        }

        private void CheckDictValid(string assetName)
        {
            if (_bundleCacheDict.ContainsKey(assetName)) return;
            var isCommon = IsCommonParts(assetName);
            _bundleCacheDict.Add(assetName, new AssetObjectData
            {
                Name = assetName,
                isKeep = isCommon,
            });
        }

        private static bool IsCommonParts(string assetName)
        {
            var lowName = assetName.ToLowerInvariant();
            return !string.IsNullOrEmpty(assetName) && lowName.Contains("common_");
        }

        private IEnumerator DownAssetBundleAsync(IObserver<AssetBundle> observer, CancellationToken token,
            DownLoadStruct assetInfo,
            bool isManifest = false)
        {
            var moduleName = assetInfo.name;
            if (!isManifest)
            {
                var nowStates = GetAssetStates(moduleName);
                if (nowStates >= AssetStates.DownLoading && nowStates != AssetStates.WaitLoad)
                {
                    yield break;
                }

                SetAssetStates(moduleName, AssetStates.DownLoading);
            }

            var downloadProgress = 0.0f;
            var uri = GetUrlPath(assetInfo, isManifest);
            var cachedAssetBundle = new CachedAssetBundle
            {
                name = moduleName,
                hash = assetInfo.hash
            };
            var request = UnityWebRequestAssetBundle.GetAssetBundle(uri, cachedAssetBundle, 0);
            request.timeout = DOWN_TIMEOUT;
            var ural = request.SendWebRequest();
            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    yield break;
                }

                if (ural.progress - PROGRESS_STEP > downloadProgress && ural.progress < 1.0f)
                {
                    downloadProgress = ural.progress;
                    assetInfo.progress?.Invoke(moduleName, downloadProgress);
//                    Debug.Log($"-->>>>downloading Name:[{moduleName}] Progress:[{downloadProgress * 100}%]");
                }

                if (ural.isDone || request.isNetworkError || request.isHttpError) break;
                yield return null;
            }

            if (request.isNetworkError || request.isHttpError)
            {
                Debug.LogError($"!!!!!!!!!! LoadAssetBundleInfo !!! URI:{uri} E:{request.error} Code:[{request.responseCode}] !!!!!!!!!!");
                var code = (int) request.responseCode;
                request.Abort();
                request.Dispose();
                SetAssetStates(moduleName, AssetStates.Update);
                observer.OnError(new NetworkInformationException(code));
                observer.OnCompleted();
                yield break;
            }

            SetAssetStates(moduleName, AssetStates.DownLoaded);
            //Debug.Log(string.Format("^^^^^^^ Downloaded File Name:[{0}] Hash:{1} Size:[{2}]  Progress:[{3}%] - [{4}%] ", module_name, asset_info.hash, request.downloadedBytes, download_progress * 100, request.downloadProgress * 100));
            var bundle = DownloadHandlerAssetBundle.GetContent(request);
            if (request.downloadedBytes > 0
                && bundle != null
                && !string.IsNullOrEmpty(bundle.name))
            {
                Caching.ClearOtherCachedVersions(bundle.name, assetInfo.hash);
            }

            observer.OnNext(bundle);
            assetInfo.callback?.Invoke(bundle);
            assetInfo.progress?.Invoke(moduleName, 1.0f);
            request.Dispose();
            observer.OnCompleted();
        }

        private static IEnumerator GetFileSize(string url, Action<long> result)
        {
            var uri = GetUrlPath(new DownLoadStruct {name = url}, false);
            var uwr = UnityWebRequest.Head(uri);
            yield return uwr.SendWebRequest();
            var size = uwr.GetResponseHeader("Content-Length");

            if (uwr.isNetworkError || uwr.isHttpError)
            {
                Debug.LogError("Error While Getting Length: " + uwr.error);
//                result?.Invoke(-1);
                throw new NetworkInformationException();
            }

            result?.Invoke(Convert.ToInt64(size));
        }

//        private void AtlasRequested(string tag, Action<SpriteAtlas> atlasAction)
//        {
//            Debug.Log($"~~~ request tag:{tag} ~~~");
//            foreach (var bundle in _bundleCacheDict)
//            {
////                if (!bundle.Key.Contains("images")) return;
//                if (bundle.Value.Bundle == null) continue;
//                var list = bundle.Value.Bundle.GetAllAssetNames();
////                for (int i = 0; i < list.Length; i++)
////                {
////                    Debug.LogWarning($"~~AtlasRequested key:{bundle.Key} index:[{i}] list:{list[i]} ");
////                }
////                var request = bundle.Value.Bundle.LoadAssetAsync<SpriteAtlas>(tag);
////                request.completed += (op) => { atlasAction.Invoke((SpriteAtlas) request.asset); };
//            }
//        }

        private void Awake()
        {
            SetPersistent(gameObject);
            LC_SceneManager.AddCallBacker((pre, cur) =>
            {
                var names = cur.name.Split('_');
                if (names.Length < 2) return;
                var main = names[0].ToLowerInvariant();
                if (!main.Equals("gamehall")) return;
                UnloadGameModelExceptIL();
            });
        }

        public void OnEnable()
        {
            LC_SceneManager.Instance.scenePreloadEvent += InterruptLoad;
//            SpriteAtlasManager.atlasRequested += AtlasRequested;
        }
    }
}