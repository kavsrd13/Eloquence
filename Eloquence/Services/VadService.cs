using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Eloquence.Services
{
    public class VadService : IDisposable
    {
        private InferenceSession _session;
        private float[,,] _h;
        private float[,,] _c;

        public VadService()
        {
            using var stream = typeof(VadService).Assembly.GetManifestResourceStream("Eloquence.silero_vad.onnx");
            if (stream == null) throw new Exception("VAD model not found in embedded resources.");
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            _session = new InferenceSession(ms.ToArray());
            ResetState();
        }

        public void ResetState()
        {
            _h = new float[2, 1, 64];
            _c = new float[2, 1, 64];
        }

        public float ProcessAudio(short[] pcmData)
        {
            float[] floatData = new float[pcmData.Length];
            for (int i = 0; i < pcmData.Length; i++)
                floatData[i] = pcmData[i] / 32768.0f;

            var tensor = new DenseTensor<float>(floatData, new[] { 1, floatData.Length });
            var hTensor = new DenseTensor<float>(Flatten(_h), new[] { 2, 1, 64 });
            var cTensor = new DenseTensor<float>(Flatten(_c), new[] { 2, 1, 64 });
            var srTensor = new DenseTensor<long>(new long[] { 16000 }, new[] { 1 });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", tensor),
                NamedOnnxValue.CreateFromTensor("sr", srTensor),
                NamedOnnxValue.CreateFromTensor("h", hTensor),
                NamedOnnxValue.CreateFromTensor("c", cTensor)
            };

            using var results = _session.Run(inputs);
            
            var outputProb = results.First(v => v.Name == "output").AsTensor<float>().First();
            var hOut = results.First(v => v.Name == "hn").AsTensor<float>().ToArray();
            var cOut = results.First(v => v.Name == "cn").AsTensor<float>().ToArray();

            UpdateState(_h, hOut);
            UpdateState(_c, cOut);

            return outputProb;
        }

        private float[] Flatten(float[,,] arr)
        {
            int d1 = arr.GetLength(0), d2 = arr.GetLength(1), d3 = arr.GetLength(2);
            float[] result = new float[d1 * d2 * d3];
            int idx = 0;
            for (int i = 0; i < d1; i++)
                for (int j = 0; j < d2; j++)
                    for (int k = 0; k < d3; k++)
                        result[idx++] = arr[i, j, k];
            return result;
        }

        private void UpdateState(float[,,] state, float[] newData)
        {
            int idx = 0;
            for (int i = 0; i < 2; i++)
                for (int j = 0; j < 1; j++)
                    for (int k = 0; k < 64; k++)
                        state[i, j, k] = newData[idx++];
        }

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}

