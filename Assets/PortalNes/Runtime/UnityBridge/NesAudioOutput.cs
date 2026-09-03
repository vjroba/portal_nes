using System;
using UnityEngine;

namespace PortalNes.UnityBridge
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class NesAudioOutput : MonoBehaviour
    {
        private NesRunner runner;
        private AudioSource audioSource;
        private AudioClip driverClip;
        private bool playbackStarted;

        public void Initialize(NesRunner owner)
        {
            runner = owner;
            EnsureAudioSourceIsPlaying();
        }

        private void Awake() => EnsureAudioSourceIsPlaying();

        private void EnsureAudioSourceIsPlaying()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null) return;
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f;
            if (driverClip == null)
            {
                int sampleRate = AudioSettings.outputSampleRate;
                driverClip = AudioClip.Create("PortalNes Audio Driver", sampleRate, 1, sampleRate, false);
                audioSource.clip = driverClip;
            }
            if (!audioSource.isPlaying) audioSource.Play();
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            Array.Clear(data, 0, data.Length);
            var machine = runner?.Machine;
            var apu = machine?.Apu;
            if (apu == null || channels <= 0) return;
            int frames = data.Length / channels;
            // Begin with two callback buffers in reserve. This absorbs normal
            // variation between Unity's audio thread and 60 Hz emulation frames.
            if (!playbackStarted)
            {
                if (apu.BufferedSampleCount < frames * 2) return;
                playbackStarted = true;
            }
            int read = apu.ReadSamples(data, 0, frames);
            if (read < frames)
            {
                // An underrun should fade to silence instead of producing a
                // hard discontinuity (the characteristic crackle). Rebuffer
                // before resuming on the next callback.
                float last = read > 0 ? data[read - 1] : 0f;
                int remaining = frames - read;
                for (int frame = read; frame < frames; frame++)
                    data[frame] = last * (frames - frame - 1f) / remaining;
                playbackStarted = false;
            }
            float volume = runner.AudioVolume;
            for (int frame = frames - 1; frame >= 0; frame--)
            {
                float sample = data[frame] * volume;
                int output = frame * channels;
                for (int channel = 0; channel < channels; channel++) data[output + channel] = sample;
            }
        }

        private void OnDestroy()
        {
            if (driverClip != null) Destroy(driverClip);
        }
    }
}
