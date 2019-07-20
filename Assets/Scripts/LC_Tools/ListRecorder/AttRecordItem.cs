using System;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LC_Tools
{
    public class AttRecordItem : UIBehaviour
    {
        [SerializeField] public SeatStatus seatStatus;
        public static Action<int, Action<ulong, uint, SeatStatus, uint>> UpdateItemAction;
        public static Action<AttRecordItem> ClickAction;
        public int showNo;
        public ulong roomNo;
        public uint gsId;

        public enum SeatStatus
        {
            Free,
            Fill,
            Leave,
        }

        private int _offset;
        private const string _leaveChair = "chair_grey";
        private const string _fillChair = "chair_red";
        private const string _freeChair = "chair_green";
        private const string _leaveMachine = "machine_grey";
        private const string _defaultMachine = "machine_color";

        private readonly Color[] colors =
        {
            new Color32(233, 217, 92, 255), //gold
            new Color32(207, 207, 207, 255) //grey
        };

        private Text _titleTxt;
        private Text _titleDesTxt;
        private Image _machineImg;
        private Image _chairImg;
        private GameObject _personGo;
        private GameObject _timeGo;
        private Text _timeTxt;

        public void UpdateItem(int index)
        {
            SetName(index);
            UpdateItemAction?.Invoke(showNo, InjectData);
        }

        public void InjectData(ulong roomId, uint id, SeatStatus status, uint time)
        {
            roomNo = roomId;
            gsId = id;
            SetStatus(status, time);
        }

        public void SetStatus(SeatStatus status, uint time = 0)
        {
            seatStatus = status;
//            Debug.Log("!! AttRecordItem SetStatus !!" + status);
            var machine = _defaultMachine;
            var chair = _freeChair;
            var color = colors[0];

            if (status == SeatStatus.Fill)
            {
                chair = _fillChair;
            }
            else if (status == SeatStatus.Leave)
            {
                chair = _leaveChair;
                machine = _leaveMachine;
                color = colors[1];

                showTimeTxt(time);
                var counter = Observable.Interval(TimeSpan.FromSeconds(1)).TakeUntilDestroy(this).Subscribe(x =>
                {
                    --time;
                    showTimeTxt(time);
                });

                Observable.Timer(TimeSpan.FromSeconds(time)).FirstOrDefault().TakeUntilDestroy(this).Subscribe(_ =>
                {
                    counter.Dispose();
                    SetStatus(SeatStatus.Free);
                });
            }

            _titleTxt.color = color;
            _titleDesTxt.color = color;
            _machineImg.sprite = LC_PoolManager.Instance.GetSpriteFromAtlas("GameHall", "SubGame", machine);
            _chairImg.sprite = LC_PoolManager.Instance.GetSpriteFromAtlas("GameHall", "SubGame", chair);
            _personGo.SetActive(status == SeatStatus.Fill);
            _timeGo.SetActive(status == SeatStatus.Leave);
        }

        private void SetName(int index)
        {
            showNo = index * 5 + _offset;
            _titleTxt.text = showNo.ToString();
        }

        private void showTimeTxt(uint time)
        {
            _timeTxt.text = $"{time / 60:D2}:{time % 60:D2}";
        }

        private void Awake()
        {
            var names = gameObject.name.Split('_');
            _offset = int.Parse(names[1]);
            var baseSet = gameObject.transform.Find("Set");
            _machineImg = baseSet.GetComponent<Image>();
            _titleTxt = baseSet.Find("Title").GetComponent<Text>();
            _titleDesTxt = baseSet.Find("Title/Text").GetComponent<Text>();
            _personGo = baseSet.Find("Person").gameObject;
            _chairImg = baseSet.Find("Seat").GetComponent<Image>();
            _timeGo = baseSet.Find("Time").gameObject;
            _timeTxt = baseSet.Find("Time/Text").GetComponent<Text>();

            transform.GetComponentInChildren<Button>().OnClickAsObservable().ThrottleFirst(TimeSpan.FromMilliseconds(DoubleClick.MILLISECONDS)).TakeUntilDestroy(this)
                .Subscribe(_ => { ClickAction?.Invoke(this); });
        }
    }
}