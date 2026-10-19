using System.Collections.Generic;
using UnityEngine;

namespace BlindAttack.Game
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }

        const int PoolSize = 12;
        readonly List<AudioSource> sfxPool = new List<AudioSource>();
        int poolIndex;
        AudioSource musicSource;

        [Range(0f, 1f)] public float masterVolume = 0.8f;
        [Range(0f, 1f)] public float sfxVolume = 0.8f;
        [Range(0f, 1f)] public float musicVolume = 0.25f;

        // ---- kayıtlı ses ayarları (Ayarlar ekranı) ----
        const string MasterKey = "BlindAttack.Volume.Master", MusicKey = "BlindAttack.Volume.Music", SfxKey = "BlindAttack.Volume.Sfx", MuteKey = "BlindAttack.Volume.Mute";
        public bool muted;

        public static float Master { get { EnsureExists(); return I.masterVolume; } set { EnsureExists(); I.masterVolume = Mathf.Clamp01(value); I.ApplyVolumes(); } }
        public static float Music { get { EnsureExists(); return I.musicVolume; } set { EnsureExists(); I.musicVolume = Mathf.Clamp01(value); I.ApplyVolumes(); } }
        public static float Sfx { get { EnsureExists(); return I.sfxVolume; } set { EnsureExists(); I.sfxVolume = Mathf.Clamp01(value); I.ApplyVolumes(); } }
        public static bool Muted { get { EnsureExists(); return I.muted; } set { EnsureExists(); I.muted = value; I.ApplyVolumes(); } }

        void LoadSettings()
        {
            masterVolume = PlayerPrefs.GetFloat(MasterKey, masterVolume);
            musicVolume = PlayerPrefs.GetFloat(MusicKey, musicVolume);
            sfxVolume = PlayerPrefs.GetFloat(SfxKey, sfxVolume);
            muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
        }

        public static void SaveSettings()
        {
            if (I == null) return;
            PlayerPrefs.SetFloat(MasterKey, I.masterVolume);
            PlayerPrefs.SetFloat(MusicKey, I.musicVolume);
            PlayerPrefs.SetFloat(SfxKey, I.sfxVolume);
            PlayerPrefs.SetInt(MuteKey, I.muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        void ApplyVolumes()
        {
            if (musicSource != null) musicSource.volume = muted ? 0f : musicVolume * masterVolume;
        }

        void Awake()
        {
            if (I != null && I != this)
            {
                Destroy(gameObject);
                return;
            }
            I = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();

            for (int i = 0; i < PoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                sfxPool.Add(src);
            }

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
            musicSource.volume = musicVolume * masterVolume;

            StartAmbient();
        }

        public static void EnsureExists()
        {
            if (I != null) return;
            var go = new GameObject("AudioManager");
            go.AddComponent<AudioManager>();
        }

        public static void Play(SoundId id, float volumeScale = 1f, float pitchVariance = 0.06f)
        {
            EnsureExists();
            if (I == null) return;
            I.PlayInternal(id, volumeScale, pitchVariance);
        }

        void PlayInternal(SoundId id, float volumeScale, float pitchVariance)
        {
            var clip = ProceduralAudio.Get(id);
            if (!clip) return;

            var src = sfxPool[poolIndex];
            poolIndex = (poolIndex + 1) % sfxPool.Count;

            src.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
            if (muted || sfxVolume * masterVolume <= 0.001f) return;
            src.PlayOneShot(clip, sfxVolume * masterVolume * volumeScale);
        }

        public void StartAmbient()
        {
            if (musicSource == null) return;
            var clip = ProceduralAudio.Get(SoundId.AmbientMusic);
            if (!clip) return;
            musicSource.clip = clip;
            ApplyVolumes();
            if (!musicSource.isPlaying) musicSource.Play();
        }

        public void StopAmbient()
        {
            if (musicSource != null && musicSource.isPlaying) musicSource.Stop();
        }
    }
}
