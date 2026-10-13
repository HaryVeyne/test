using Godot;
using System;

namespace ProjectUnknown;

public partial class Soundscape : Node
{
    private AudioStreamPlayer _ambience = null!;
    private AudioStreamPlayer _chime = null!;
    private bool _silent;

    public override void _Ready()
    {
        _silent = DisplayServer.GetName() == "headless";
        if (_silent) return;
        _ambience = new AudioStreamPlayer { Stream = MakeAmbient(), VolumeDb = -24 };
        _chime = new AudioStreamPlayer();
        AddChild(_ambience);
        AddChild(_chime);
        _ambience.Play();
    }

    public void SetVolume(float value)
    {
        AudioServer.SetBusVolumeDb(0, value <= 0 ? -80 : Mathf.LinearToDb(value));
    }

    public void Chime(bool success)
    {
        if (_silent) return;
        const int rate = 22050;
        var samples = new byte[rate];
        for (int i = 0; i < samples.Length / 2; i++)
        {
            float t = (float)i / rate;
            float frequency = success ? (t < 0.16f ? 523.25f : t < 0.32f ? 659.25f : 783.99f) : 220;
            float envelope = Mathf.Sin(Mathf.Pi * t / 0.5f) * Mathf.Exp(-t * 3);
            short v = (short)(Mathf.Sin(Mathf.Tau * frequency * t) * envelope * 9000);
            samples[i * 2] = (byte)(v & 255);
            samples[i * 2 + 1] = (byte)(v >> 8);
        }
        var previous = _chime.Stream;
        _chime.Stop();
        _chime.Stream = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = rate, Data = samples };
        previous?.Dispose();
        _chime.Play();
    }

    public void Stop()
    {
        if (_silent) return;
        Release(_ambience);
        Release(_chime);
    }

    public override void _ExitTree() => Stop();

    private static void Release(AudioStreamPlayer player)
    {
        player.Stop();
        var stream = player.Stream;
        player.Stream = null;
        stream?.Dispose();
    }

    private static AudioStreamWav MakeAmbient()
    {
        const int rate = 22050;
        const int seconds = 8;
        var samples = new byte[rate * seconds * 2];
        var random = new Random(73129);
        float wind = 0;
        for (int i = 0; i < rate * seconds; i++)
        {
            float t = (float)i / rate;
            wind = wind * 0.985f + ((float)random.NextDouble() - 0.5f) * 0.015f;
            float drone = Mathf.Sin(Mathf.Tau * 110 * t) * 0.09f + Mathf.Sin(Mathf.Tau * 165 * t) * 0.025f;
            float loopFade = Mathf.Sin(Mathf.Pi * t / seconds);
            short v = (short)((wind + drone) * loopFade * 9500);
            samples[i * 2] = (byte)(v & 255);
            samples[i * 2 + 1] = (byte)(v >> 8);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = rate,
            Data = samples, LoopMode = AudioStreamWav.LoopModeEnum.Forward, LoopBegin = 0, LoopEnd = rate * seconds };
    }
}
