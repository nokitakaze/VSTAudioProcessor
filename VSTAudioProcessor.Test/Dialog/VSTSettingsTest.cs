using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using VSTAudioProcessor.Dialog;
using VSTAudioProcessor.Process;
using VSTAudioProcessor.Test.Process;
using Xunit;

namespace VSTAudioProcessor.Test.Dialog
{
    public class VSTSettingsTest
    {
        protected static readonly Random rnd = new Random();

        public static IEnumerable<object[]> WriteReadTestData()
        {
            var data = new List<object[]>();
            for (int i = 0; i < 3; i++)
            {
                // chunk
                var stateSize = rnd.Next(1, 200) * 4;
                var saveState = new byte[stateSize];
                rnd.NextBytes(saveState);
                data.Add(new object[] {saveState, null});

                // params
                var paramCount = rnd.Next(1, 200);
                var parameters = new List<float>();
                for (int j = 0; j < paramCount; j++)
                {
                    parameters.Add((float) rnd.NextDouble());
                }

                data.Add(new object[] {null, parameters.ToArray()});
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(WriteReadTestData))]
        public void WriteReadTest(
            byte[] saveState,
            float[] vstParameters
        )
        {
            Assert.True((saveState != null) || (vstParameters != null));
            var temporaryFile = Path.GetTempFileName();
            var pluginId = (uint) rnd.Next(1, 0x7FFF_FFFF);
            var pluginVersion = (uint) rnd.Next(1, 0x7FFF_FFFF);

            VSTSettings.SaveStateAsFxb(
                temporaryFile,
                saveState,
                vstParameters,
                (saveState != null),
                pluginId,
                pluginVersion
            );

            Assert.True(System.IO.File.Exists(temporaryFile));

            AssertFile(
                temporaryFile,
                pluginId,
                pluginVersion,
                saveState,
                vstParameters
            );
            System.IO.File.Delete(temporaryFile);

            //
            VSTSettings.SaveStateAsFxp(
                temporaryFile,
                saveState,
                vstParameters,
                (saveState != null),
                pluginId,
                pluginVersion
            );
            Assert.True(System.IO.File.Exists(temporaryFile));

            AssertFile(
                temporaryFile,
                pluginId,
                pluginVersion,
                saveState,
                vstParameters
            );
            System.IO.File.Delete(temporaryFile);
        }

        protected static void AssertFile(string temporaryFile, uint pluginId, uint pluginVersion,
            byte[] saveState, float[] vstParameters)
        {
            var pluginStub = new PluginStub();
            var result = FxbReader.ReadFileIntoPluginStub(
                temporaryFile,
                pluginId,
                pluginVersion,
                false,
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