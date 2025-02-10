// using NAudio.Wave;

// namespace Sixel.Protocols;

// public class GifAudio : IDisposable
// {
//   private readonly WaveOutEvent waveOut;
//   private readonly AudioFileReader audioFileReader;
//   public GifAudio(string audioFile)
//   {
//     waveOut = new WaveOutEvent();
//     audioFileReader = new AudioFileReader(audioFile);
//     waveOut.Init(audioFileReader);
//   }
//   public void Play()
//   {
//     waveOut.Play();
//   }
//   public void Stop()
//   {
//     waveOut.Stop();
//   }
//   public void Dispose()
//   {
//     waveOut.Dispose();
//     audioFileReader.Dispose();
//   }
//   public bool IsPlaying => waveOut.PlaybackState == PlaybackState.Playing;
// }
