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

        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float sfxVolume = 0.85f;
        [Range(0f, 1f)] public float musicVolume = 0.35f;

        void Awake()
        {
            if (I != null && I != this)
            {
                Destroy(gameObject);
                return;
            }
            I = this;
            DontDestroyOnLoad(gameObject);

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
            src.PlayOneShot(clip, sfxVolume * masterVolume * volumeScale);
        }

        public void StartAmbient()
        {
            if (musicSource == null) return;
            var clip = ProceduralAudio.Get(SoundId.AmbientMusic);
            if (!clip) return;
            musicSource.clip = clip;
            musicSource.volume = musicVolume * masterVolume;
            if (!musicSource.isPlaying) musicSource.Play();
        }

        public void StopAmbient()
        {
            if (musicSource != null && musicSource.isPlaying) musicSource.Stop();
        }
    }
}
