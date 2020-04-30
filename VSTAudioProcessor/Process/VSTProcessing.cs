using System;
using Jacobi.Vst.Interop.Host;
using VSTAudioProcessor.General;
using VSTAudioProcessor.VstSubClasses;

namespace VSTAudioProcessor.Process
{
    public static class VSTProcessing
    {
        private const int ReturnCodeOffset = 200;

        public static int ProcessWaveFile(
            CallParameters parameters,
            System.Threading.CancellationToken cancellationToken
        )
        {
            string pluginPath = parameters.VstPluginPath;

            if (!System.IO.File.Exists(parameters.FxbFile))
            {
                Console.Error.WriteLine("Can not find Bank/Preset file {0}", parameters.FxbFile);

                return ReturnCodeOffset + 0;
            }

            if (!System.IO.File.Exists(pluginPath))
            {
                Console.Error.WriteLine("Can not find VST-plugin file {0}", pluginPath);

                return ReturnCodeOffset + 1;
            }

            if (parameters.InputWavFile == null)
            {
                Console.Error.WriteLine("InputWavFile param is missing");

                return ReturnCodeOffset + 2;
            }

            if (!System.IO.File.Exists(parameters.InputWavFile))
            {
                Console.Error.WriteLine("Can not find wave file {0}", parameters.InputWavFile);

                return ReturnCodeOffset + 3;
            }

            if (parameters.OutputWavFile == null)
            {
                Console.Error.WriteLine("OutputWavFile param is missing");

                return ReturnCodeOffset + 4;
            }

            var hostCmdStub = new HostCommandStub();
            var ctx = VstPluginContext.Create(pluginPath, hostCmdStub);
            ctx.PluginCommandStub.Open();

            // add custom data to the context
            ctx.Set("PluginPath", pluginPath);
            ctx.Set("HostCmdStub", hostCmdStub);

            ctx.PluginCommandStub.MainsChanged(true);
            int result = FxbReader.ReadFileIntoPluginStub(
                parameters.FxbFile,
                (uint) ctx.PluginInfo.PluginID,
                (uint) ctx.PluginInfo.PluginVersion,
                parameters.IgnorePluginVersion,
                ctx.PluginCommandStub
            );
            ctx.PluginCommandStub.MainsChanged(false);
            if (result != 0)
            {
                ctx.PluginCommandStub.Close();
                return result;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                // Был затребован выход из программы
                ctx.PluginCommandStub.Close();
                return ReturnCodeOffset + 5;
            }

            throw new NotImplementedException();
        }
    }
}