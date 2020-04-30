using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using VSTAudioProcessor.Process;

namespace VSTAudioProcessor.Test.Process
{
    public class FxbReaderTest
    {
        public static IEnumerable<object[]> ReadFilesTestData()
        {
            var data = new List<object[]>();
            {
                var opaqueFiles = new List<Tuple<string, int, uint, uint>>()
                {
                    new Tuple<string, int, uint, uint>("./Resources/nanohost-reeq-opaque-5100.fxb", 0xA0, 0x7265_6571, 0x044C),
                    new Tuple<string, int, uint, uint>("./Resources/nanohost-reeq-opaque-5200.fxp", 0xA0, 0x7265_6571, 0x044C),
                };

                var dataLocal = new List<object[]>();
                foreach (var (filename, chunkSize, pluginId, pluginVersion) in opaqueFiles)
                {
                    dataLocal.Add(GetDataFromOpaque(filename, chunkSize, pluginId, pluginVersion));
                }

                data.AddRange(dataLocal);
                foreach (var datum in dataLocal.Select(datum => datum.ToArray()))
                {
                    // ReSharper disable once RedundantCast
                    datum[2] = (int) 0x7FFFFFF0;
                    datum[3] = true;
                    data.Add(datum);
                }
            }
            {
                var paramFiles = new List<Tuple<string, int, uint, uint>>()
                {
                    new Tuple<string, int, uint, uint>("./Resources/wavosaur-reeq-4500.fxb", 0x0C, 0x7265_6571, 0),
                    new Tuple<string, int, uint, uint>("./Resources/wavosaur-reeq-5500.fxp", 0x0C, 0x7265_6571, 0),
                    new Tuple<string, int, uint, uint>("./Resources/wavosaur-reeq-3set.fxb", 0x09, 0x7265_6571, 0),
                    new Tuple<string, int, uint, uint>("./Resources/wavosaur-reeq-3set.fxp", 0x09, 0x7265_6571, 0),
                };

                var dataLocal = new List<object[]>();
                foreach (var (filename, paramCount, pluginId, pluginVersion) in paramFiles)
                {
                    dataLocal.Add(GetDataFromParam(filename, paramCount, pluginId, pluginVersion));
                }

                data.AddRange(dataLocal);
                foreach (var datum in dataLocal.Select(datum => datum.ToArray()))
                {
                    // ReSharper disable once RedundantCast
                    datum[2] = (int) 0x7FFFFFF0;
                    datum[3] = true;
                    data.Add(datum);
                }
            }

            return data;
        }

        private static object[] GetDataFromOpaque(
            string filename,
            int chunkSize,
            uint pluginId,
            uint pluginVersion
        )
        {
            var fileData = File.ReadAllBytes(filename);
            var bytes = new byte[chunkSize];

            Array.Copy(fileData, fileData.Length - chunkSize, bytes, 0, chunkSize);

            return new object[] {filename, pluginId, pluginVersion, false, null, bytes};
        }

        private static object[] GetDataFromParam(
            string filename,
            int paramCount,
            uint pluginId,
            uint pluginVersion
        )
        {
            var fileData = File.ReadAllBytes(filename);
            var bytes = new byte[paramCount * 4];

            Array.Copy(fileData, fileData.Length - paramCount * 4, bytes, 0, paramCount * 4);

            var parameters = new List<float>();
            var b = new byte[4];
            for (int i = 0; i < paramCount; i++)
            {
                Array.Copy(bytes, i * 4, b, 0, 4);
                var value = BitConverter.ToSingle(b.Reverse().ToArray(), 0);
                parameters.Add(value);
            }

            return new object[] {filename, pluginId, pluginVersion, false, parameters.ToArray(), null};
        }

        [Theory]
        [MemberData(nameof(ReadFilesTestData))]
        public void ReadFilesTest(
            string filename,
            uint pluginId,
            uint pluginVersion,
            bool ignorePluginVersion,
            float[] vstParameters,
            byte[] saveState
        )
        {
            Assert.True((vstParameters != null) || (saveState != null));

            var pluginStub = new PluginStub();

            var result = FxbReader.ReadFileIntoPluginStub(
                filename,
                pluginId,
                pluginVersion,
                ignorePluginVersion,
                pluginStub
            );

            Assert.Equal(0, result);
            if (vstParameters != null)
            {
                for (int i = 0; i < vstParameters.Length; i++)
                {
                    var realValue = pluginStub.GetParameter(i);

                    Assert.Equal(vstParameters[i], realValue);
                }
            }

            if (saveState != null)
            {
                var realData = pluginStub.GetChunk(true);
                Assert.Equal(saveState.Length, realData.Length);

                for (int i = 0; i < saveState.Length; i++)
                {
                    var realValue = realData[i];

                    Assert.Equal(saveState[i], realValue);
                }
            }
        }
    }
}