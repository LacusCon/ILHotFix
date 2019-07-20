using UnityEngine;

//fork from  https://github.com/tsubaki/Unity_UI_Samples
namespace LC_Tools
{
    public interface IInfiniteScrollSetup
    {
        void OnPostSetupItems();
        void ResetItems();
        void OnUpdateItem(int itemCount, GameObject obj);
    }
}