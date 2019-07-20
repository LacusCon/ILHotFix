using UnityEngine;

namespace LC_Tools
{
    /// <summary>
    /// Inherit from this base class to create a singleton.
    /// e.g. public class MyClassName : Singleton<MyClassName> {}
    /// </summary>
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static object m_Lock = new object();
        private static T m_Instance;
        private static GameObject _pedal;

        /// <summary>
        /// Access singleton instance through this propriety.
        /// </summary>
        public static T Instance
        {
            get
            {
                lock (m_Lock)
                {
                    if (m_Instance != null) return m_Instance;

                    // Search for existing instance.
                    m_Instance = FindObjectOfType<T>();
                    // Create new instance if one doesn't already exist.
                    if (m_Instance != null) return m_Instance;
                    // Need to create a new GameObject to attach the singleton to.
                    var singletonObject = new GameObject(typeof(T) + " (Singleton)");
                    m_Instance = singletonObject.AddComponent<T>();
                    return m_Instance;
                }
            }
        }

        private const string pedalName = "SingletonPedal";

        protected static void SetPersistent(GameObject child)
        {
            if (_pedal == null)
            {
                var gos = FindObjectsOfType<GameObject>();
                foreach (var go in gos)
                {
                    if (!pedalName.Equals(go.name)) continue;
                    _pedal = go;
                    break;
                }

                if (!_pedal)
                {
                    _pedal = new GameObject(pedalName);
                    DontDestroyOnLoad(_pedal);
                }
            }

            child.transform.SetParent(_pedal.transform, false);
            DontDestroyOnLoad(child);
        }

        private void OnDestroy()
        {
            _pedal.transform.parent = null;
            _pedal = null;
        }
    }
}