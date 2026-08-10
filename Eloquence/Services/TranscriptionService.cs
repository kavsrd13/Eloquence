using System;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using Whisper.net;
using Whisper.net.Ggml;

namespace Eloquence.Services
{
    public class TranscriptionService : IDisposable
    {
        private WhisperFactory? _factory;
        private WhisperProcessor? _processor;
        private readonly string _modelPath;
        private bool _isInitialized = false;

        public event Action<string>? OnDownloadProgress;

        public TranscriptionService()
        {
            _modelPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Eloquence", "ggml-base.en.bin");
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            if (!File.Exists(_modelPath))
            {
                OnDownloadProgress?.Invoke("Downloading AI Model... (150MB)");
                using var client = new System.Net.Http.HttpClient();
                using var modelStream = await client.GetStreamAsync("https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin");
                using var fileWriter = File.OpenWrite(_modelPath);
                await modelStream.CopyToAsync(fileWriter);
            }

            _factory = WhisperFactory.FromPath(_modelPath);
            _processor = _factory.CreateBuilder()
                .WithLanguage("en")
                .Build();

            _isInitialized = true;
        }

        private byte[] CreateWavHeader(int sampleRate, short channels, short bitsPerSample, int dataLength)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            bw.Write(new[] { 'R', 'I', 'F', 'F' });
            bw.Write(36 + dataLength);
            bw.Write(new[] { 'W', 'A', 'V', 'E' });
            bw.Write(new[] { 'f', 'm', 't', ' ' });
            bw.Write(16);
            bw.Write((short)1); // PCM
            bw.Write(channels);
            bw.Write(sampleRate);
            bw.Write(sampleRate * channels * bitsPerSample / 8);
            bw.Write((short)(channels * bitsPerSample / 8));
            bw.Write(bitsPerSample);
            bw.Write(new[] { 'd', 'a', 't', 'a' });
            bw.Write(dataLength);
            return ms.ToArray();
        }

        public async Task<string> TranscribeWavAsync(byte[] wavData)
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }

            try
            {
                using var ms = new MemoryStream(wavData);
                var sb = new StringBuilder();

                await foreach (var result in _processor!.ProcessAsync(ms))
                {
                    sb.Append(result.Text).Append(" ");
                }

                return sb.ToString().Trim();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public async Task<string> TranscribeAsync(byte[] pcm16kHz)
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }

            try
            {
                var header = CreateWavHeader(16000, 1, 16, pcm16kHz.Length);
                var fullWav = new byte[header.Length + pcm16kHz.Length];
                Buffer.BlockCopy(header, 0, fullWav, 0, header.Length);
                Buffer.BlockCopy(pcm16kHz, 0, fullWav, header.Length, pcm16kHz.Length);

                return await TranscribeWavAsync(fullWav);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public void Dispose()
        {
            _processor?.Dispose();
            _factory?.Dispose();
        }
    }
}

