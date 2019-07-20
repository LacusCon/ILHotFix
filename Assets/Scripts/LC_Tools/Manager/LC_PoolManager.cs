using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace LC_Tools
{
    public class LC_PoolManager : Singleton<LC_PoolManager>
    {
        [SerializeField] public int elementsCount;

        private const int defaultPoolSize = 5;
        private const string poolKey = "{0}:{1}:{2}";
        private readonly Dictionary<string, ObjectStock> _poolDict = new Dictionary<string, ObjectStock>();
        private readonly Dictionary<string, Delegate> _waitForCall = new Dictionary<string, Delegate>();

        private class ObjectStock
        {
            public int poolSize = defaultPoolSize;
            public readonly Queue<object> objQueue = new Queue<object>();
        }

        public void SetObjectPoolSize(Object obj, int size)
        {
            SetPoolSize(obj.name, size);
        }

        public void SetObjectPoolSize<T>(string model, string name, int size)
        {
            SetPoolSize(GetKey<T>(model, name), size);
        }

        public void PreloadObject(string model, List<string> pList)
        {
            foreach (var prefab in pList)
            {
                var key = GetKey<GameObject>(model, prefab);
                var tar = LC_ResourceManager.Instance.GetObject<GameObject>(model, prefab);
                tar = SetObjName(tar, key);
                RecyclingObject(tar);
            }
        }

        public Sprite GetSpriteFromAtlas(string model, string atlas, string name, UnityAction<Sprite> callback = null, bool reuse = true)
        {
            if (callback == null)
            {
                var atlasGo = GetObject<SpriteAtlas>(model, atlas, null, reuse);
                return atlasGo.GetSprite(name);
            }

            GetObject<SpriteAtlas>(model, atlas, go => { callback(go.GetSprite(name)); }, reuse);
            return null;
        }

        public T GetObject<T>(string model, string name, UnityAction<T> callback = null, bool reuse = false)
        {
            var key = GetKey<T>(model, name);
            var result = GetObjectFromPool<T>(key, reuse);
            if (result != null && !result.Equals(default(T)))
            {
                callback?.Invoke(result);
                return result;
            }

            if (callback != null && typeof(T) != typeof(GameObject))
            {
                if (_waitForCall.ContainsKey(key))
                {
                    _waitForCall[key] = (UnityAction<T>) _waitForCall[key] + callback;
                    return default;
                }

                _waitForCall.Add(key, callback);
            }

            if (callback == null)
            {
                var ret = LC_ResourceManager.Instance.GetObject<T>(model, name);
                SetObjName(ret, key);
                return ret;
            }

            LC_ResourceManager.Instance.GetObject<T>(model, name, obj =>
            {
                var chg = SetObjName(obj, key);
                if (obj as GameObject)
                {
                    callback.Invoke(chg);
                }
                else
                {
                    CallBackFunc(key, chg);
                }
            });

            return default;
        }

        public void RecyclingObject(Object obj)
        {
            if (obj == null) return;
            AppendObjectToPool(obj.name, obj);
        }

        public static string GetKey<T>(string model, string name)
        {
            var rt = typeof(T).ToString();
            return string.Format(poolKey, model, name, rt);
        }

        private static T SetObjName<T>(T obj, string name)
        {
            var result = obj;
            if (obj as GameObject)
            {
                var tar = Instantiate(obj as GameObject);
                tar.name = name;
                result = (T) (object) tar;
            }
            else if (obj as Sprite)
            {
            }

            return result;
        }

        private void CallBackFunc<T>(string key, T result)
        {
            if (!_waitForCall.ContainsKey(key)) return;
            Delegate d;
            if (_waitForCall.TryGetValue(key, out d))
            {
                var action = d as UnityAction<T>;
                action?.Invoke(result);
            }

            _waitForCall.Remove(key);
        }

        private void SetPoolSize(string key, int size)
        {
            if (_poolDict.ContainsKey(key))
            {
                var cont = _poolDict[key];
                cont.poolSize = size;
            }
            else
            {
                _poolDict.Add(key, new ObjectStock {poolSize = size});
            }
        }

        private void ClearObjectPool(string model)
        {
            var nameList = _poolDict.Keys.ToList();
            foreach (var name in nameList)
            {
                var index = name.IndexOf(model);
                if (index == 0)
                {
                    _poolDict.Remove(name);
                }
            }
        }

        private void ClearOtherObjectPool(string model)
        {
            var nameList = _poolDict.Keys.ToList();
            foreach (var name in nameList)
            {
                var index = name.IndexOf(model);
                if (index == 0) continue;
                _poolDict.Remove(name);
            }
        }

        private void AppendObjectToPool(string key, object obj)
        {
            var go = obj as GameObject;
            if (go) go.SetActive(false);
            if (_poolDict.ContainsKey(key))
            {
                var content = _poolDict[key];
                if (content.objQueue.Count >= content.poolSize)
                {
                    if (go) Destroy(go);
                    return;
                }

                content.objQueue.Enqueue(obj);
            }
            else
            {
                var cont = new ObjectStock();
                cont.objQueue.Enqueue(obj);
                _poolDict.Add(key, cont);
            }
        }

        private T GetObjectFromPool<T>(string key, bool reuse)
        {
            if (!_poolDict.ContainsKey(key)) return default;

            var objQueue = _poolDict[key].objQueue;
            var count = objQueue.Count;
            if (count <= 0) return default;
            var result = objQueue.Dequeue();
            if (reuse)
            {
                objQueue.Enqueue(result);
            }

            return (T) result;
        }

        private void CountPools()
        {
            var count = 0;
            foreach (var pool in _poolDict)
            {
                count += pool.Value.objQueue.Count;
            }

            elementsCount = count;
        }

        private void Awake()
        {
            SetPersistent(gameObject);
        }

        private void Start()
        {
            LC_SceneManager.AddCallBacker((pre, now) =>
            {
                var names = now.name.Split('_');
                if (names.Length > 1)
                {
                    ClearOtherObjectPool(names[0]);
                }
            });

#if UNITY_EDITOR
            this.LateUpdateAsObservable().Sample(TimeSpan.FromSeconds(3)).Subscribe(_ => { CountPools(); });
#endif
        }

        private void OnDestroy()
        {
            _poolDict.Clear();
        }
    }
}