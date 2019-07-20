using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LC_Tools
{
    [RequireComponent(typeof(InfiniteScroll))]
    public class ItemControllerLoop : UIBehaviour, IInfiniteScrollSetup
    {
        private bool _isSetup;

        public void OnPostSetupItems()
        {
            GetComponentInParent<ScrollRect>().movementType = ScrollRect.MovementType.Unrestricted;
            _isSetup = true;
        }

        public void ResetItems()
        {
        }

        public void OnUpdateItem(int itemCount, GameObject obj)
        {
            if (_isSetup) return;

            var item = obj.GetComponentInChildren<BankRecordItem>();
            item.UpdateItem(itemCount);
        }
    }
}