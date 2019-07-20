using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LC_Tools
{
    [RequireComponent(typeof(InfiniteScroll))]
    public class ItemControllerInfinite : UIBehaviour, IInfiniteScrollSetup
    {
        public void OnPostSetupItems()
        {
            GetComponent<InfiniteScroll>().onUpdateItem.AddListener(OnUpdateItem);
            GetComponentInParent<ScrollRect>().movementType = ScrollRect.MovementType.Unrestricted;
        }

        public void ResetItems()
        {
            
        }

        public void OnUpdateItem(int itemCount, GameObject obj)
        {
            var item = obj.GetComponentInChildren<BankRecordItem>();
            item.UpdateItem(itemCount);
        }
    }
}