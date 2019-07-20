using UnityEngine;

namespace LC_Tools
{
    public class AssetObjectData
    {

        public enum AssetStates
        {
            None,
            //WaitLoad,
            Update,
            DownLoading,
            DownLoaded,
            WaitLoad,
            Loading,
            Loaded,
            //Destroyed,
        }

        private AssetStates states = AssetStates.None;
        private AssetBundle bundle = null;
        private float lastTime = 0;
        public bool isKeep = false;

        public void ClearAssetData(bool isDestroy = false)
        {
            if (bundle != null)
            {
                bundle.Unload(isDestroy);
                bundle = null;
            }
            lastTime = 0;
            states = AssetStates.None;
        }

        public AssetStates States
        {
            get => states;
            set
            {
                lastTime = Time.realtimeSinceStartup;
                states = value;
            }
        }

        public string Name { get; set; }

        public AssetBundle Bundle
        {

            get
            {
                lastTime = Time.realtimeSinceStartup;
                return bundle;
            }

            set
            {
                lastTime = Time.realtimeSinceStartup;
                bundle = value;
            }
        }

        public float Last_time => lastTime;
    }
}