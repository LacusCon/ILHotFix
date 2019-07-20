using System;
using System.Collections.Generic;
using UniRx;
using UniRx.Triggers;
using UnityEngine;

namespace LC_Tools
{
    public class LC_SoundManager : Singleton<LC_SoundManager>
    {
        private static AudioSource _bgmSource;
        private static float _audioVolume = 0.5f;
        private readonly List<AudioProperties> _audioSourceList = new List<AudioProperties>();
        private const int AUDIO_LIMIT = 15;
        private GameObject _pedalGo;

        private class AudioProperties
        {
            public string key;
            public AudioSource source;
            public float last_time;
        }

        private const string DISABLE = "0";
        private const string ENABLE = "1";
        private readonly Dictionary<int, bool> _interruptDict = new Dictionary<int, bool>();

        public static bool SoundOnOff { get; private set; }

        public static bool BGMOnOff { get; private set; }

        public static float AudioVolume
        {
            get => _audioVolume;
            set => _audioVolume = value;
        }

        private static bool OpSoundSetting
        {
            get => ENABLE.Equals(PlayerPrefs.GetString(SaveInfo.Sound.ToString()));
            set => PlayerPrefs.SetString(SaveInfo.Sound.ToString(), value ? ENABLE : DISABLE);
        }

        private static bool OpBGMSetting
        {
            get => ENABLE.Equals(PlayerPrefs.GetString(SaveInfo.Music.ToString()));
            set => PlayerPrefs.SetString(SaveInfo.Music.ToString(), value ? ENABLE : DISABLE);
        }

        private void ClearAudioData(string scene_name)
        {
            foreach (var properties in _audioSourceList)
            {
                properties.source.Stop();
                properties.source.clip = null;
                properties.source.enabled = false;
            }
        }

        public void PlayBGM(string model_name, string sound_name)
        {
            if (_bgmSource.clip != null && _bgmSource.clip.name == sound_name)
            {
                if (_bgmSource.isPlaying) return;
            }

            SetAudio(_bgmSource, model_name, sound_name, true, !BGMOnOff);
        }

        private static void ContinuePlayBGM()
        {
            if (_bgmSource == null || _bgmSource.clip == null || _bgmSource.isPlaying) return;
            _bgmSource.enabled = true;
            _bgmSource.Play();
        }

        public void StopBGM()
        {
            _bgmSource.Stop();
            _bgmSource.enabled = false;
        }

        public void PlaySound(string model_name, string sound_name, bool isLoop = false, bool isSingle = false, bool interrupt = true)
        {
//            Debug.Log($"****** PlaySound: name:{model_name}:{sound_name} mute:{!SoundOnOff} ******");
            if (!SoundOnOff) return;

            var key = LC_PoolManager.GetKey<AudioClip>(model_name, sound_name);
            if (isSingle)
            {
                foreach (var audio in _audioSourceList)
                {
                    if (audio.source.clip != null
                        && audio.source.clip.name.Equals(key))
                    {
                        return;
                    }
                }
            }

            AudioProperties audioProp;
            var index = 0;
            if (_audioSourceList.Count < AUDIO_LIMIT)
            {
                var audioSource = _pedalGo.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.enabled = false;
                audioProp = new AudioProperties
                {
                    source = audioSource,
                };
                _audioSourceList.Add(audioProp);
                index = _audioSourceList.Count - 1;
            }
            else
            {
                var faraway_time = 0.0f;
                for (var i = 0; i < _audioSourceList.Count; i++)
                {
                    var ap = _audioSourceList[i];
                    if (ap.source == null)
                    {
                        index = i;
                        break;
                    }

                    if (_interruptDict.ContainsKey(i)) continue;
                    if (faraway_time != 0.0f && !(ap.last_time < faraway_time)) continue;
                    faraway_time = ap.last_time;
                    index = i;
                }

                audioProp = _audioSourceList[index];
            }

            if (!interrupt && !_interruptDict.ContainsKey(index))
            {
                _interruptDict.Add(index, false);
            }

            audioProp.key = key;
            audioProp.last_time = Time.realtimeSinceStartup;
            SetAudio(audioProp.source, model_name, sound_name, isLoop, false, index);
        }

        public void StopSound(string model_name = null, string sound_name = null)
        {
            var isStopAll = string.IsNullOrEmpty(model_name) || string.IsNullOrEmpty(sound_name);

            string key = null;
            if (!isStopAll) key = LC_PoolManager.GetKey<AudioClip>(model_name, sound_name);
            foreach (var audioStruct in _audioSourceList)
            {
                var same = audioStruct.key.Equals(key);
                if (!isStopAll && !same) continue;
                if (audioStruct.source == null || audioStruct.source.clip == null) continue;
                var clip = audioStruct.source.clip;
                audioStruct.source.Stop();
                audioStruct.source.enabled = false;
                audioStruct.source.clip = null;
                audioStruct.key = "";
                LC_PoolManager.Instance.RecyclingObject(clip);
                if (same) break;
            }
        }

        public void ChangeToggleValue(bool isOn, bool isBGM, string model = null, string sound = null)
        {
            if (isBGM)
            {
                BGMOnOff = isOn;
                OpBGMSetting = isOn;
                if (isOn)
                {
                    ContinuePlayBGM();
                }
                else
                {
                    StopBGM();
                }
            }
            else
            {
                SoundOnOff = isOn;
                OpSoundSetting = isOn;
                if (!isOn)
                {
                    StopSound();
                }
            }

            if (!string.IsNullOrEmpty(model) && !string.IsNullOrEmpty(sound))
            {
                PlaySound(model, sound);
            }
        }

        private void SetAudio(AudioSource source, string model_name, string sound_name, bool isLoop = false,
            bool isMute = false, int index = -1)
        {
//            Debug.LogWarning($"******  SetAudio Mute:[{isMute}] model:[{model_name}] sound:[{sound_name}] ******");
            LC_PoolManager.Instance.GetObject<AudioClip>(model_name, sound_name, clip =>
            {
                source.enabled = true;
                source.Stop();
                source.clip = clip;
                source.volume = _audioVolume;
                source.loop = isLoop;
                if (isMute) return;
                source.Play();
//                Debug.Log($"%%% Play: [{sound_name}] Time:[{DateTime.Now}] Index:[{index}]");
                if (!isLoop && clip != null)
                {
                    this.LateUpdateAsObservable().Sample(TimeSpan.FromSeconds(clip.length)).FirstOrDefault().TakeUntilDestroy(this).Subscribe(_ =>
                    {
                        if (index >= 0 && _interruptDict.ContainsKey(index))
                        {
                            _interruptDict.Remove(index);
                        }

                        source.Stop();
                        source.clip = null;
                        LC_PoolManager.Instance.RecyclingObject(clip);
                    });
                }
            });
        }

        private static void InitComponent(GameObject main_go)
        {
            main_go.AddComponent<AudioListener>();
            _bgmSource = main_go.AddComponent<AudioSource>();
        }

        private static void SetDefaultVoiceState()
        {
            var mus = PlayerPrefs.GetString(SaveInfo.Music.ToString());
            if (string.IsNullOrEmpty(mus))
            {
                PlayerPrefs.SetString(SaveInfo.Music.ToString(), ENABLE);
            }

            BGMOnOff = OpBGMSetting;

            var sou = PlayerPrefs.GetString(SaveInfo.Sound.ToString());
            if (string.IsNullOrEmpty(sou))
            {
                PlayerPrefs.SetString(SaveInfo.Sound.ToString(), ENABLE);
            }

            SoundOnOff = OpSoundSetting;
        }

        private void Awake()
        {
            _pedalGo = new GameObject("Pedal");
            _pedalGo.transform.SetParent(transform);
            SetPersistent(gameObject);
            InitComponent(gameObject);
            SetDefaultVoiceState();
        }

        private void OnEnable()
        {
            LC_SceneManager.Instance.scenePreloadEvent += ClearAudioData;
        }
    }
}