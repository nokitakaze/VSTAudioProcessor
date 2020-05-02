using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Jacobi.Vst.Interop.Host;
using VSTAudioProcessor.General;
using VSTAudioProcessor.VstSubClasses;

namespace VSTAudioProcessor.Dialog
{
    /// <summary>
    /// 
    /// </summary>
    public static class VSTSettings
    {
        private const int ReturnCodeOffset = 100;
        private static InvisibleForm form;
        private const string FxbFileMagicHeader = FxbFile.FxbFileMagicHeader;

        private const string FxbHeader_Preset_Param = FxbFile.FxbHeader_Preset_Param;
        private const string FxbHeader_Preset_Opaque = FxbFile.FxbHeader_Preset_Opaque;
        private const string FxbHeader_Bank_Param = FxbFile.FxbHeader_Bank_Param;

        // ReSharper disable once UnusedMember.Local
        private const string FxbHeader_Bank_Opaque = FxbFile.FxbHeader_Bank_Opaque;

        private const int PresetNameLength = FxbFile.PresetNameLength;
        private const int FxbReservedLength = FxbFile.FxbReservedLength;

        public static int SetUpPlugin(
            CallParameters parameters,
            System.Threading.CancellationToken cancellationToken
        )
        {
            var cancellationTaskSource = new CancellationTokenSource();
            Task.Run(() =>
            {
                cancellationToken.WaitHandle.WaitOne();
                form.Close();
            }, cancellationTaskSource.Token);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string pluginPath = parameters.VstPluginPath;

            var hostCmdStub = new HostCommandStub();
            var ctx = VstPluginContext.Create(pluginPath, hostCmdStub);
            ctx.PluginCommandStub.Open();

            // add custom data to the context
            ctx.Set("PluginPath", pluginPath);
            ctx.Set("HostCmdStub", hostCmdStub);

            ctx.PluginCommandStub.MainsChanged(true);
            form = new InvisibleForm(ctx);
            Application.Run(form);
            ctx.PluginCommandStub.MainsChanged(false);
            if (cancellationToken.IsCancellationRequested)
            {
                // Был затребован выход из программы
                ctx.PluginCommandStub.Close();
                cancellationTaskSource.Cancel();
                return ReturnCodeOffset + 1;
            }

            byte[] vstSaveState = null;
            float[] vstParameters = null;

            if (!parameters.FxbFileAsParams)
            {
                vstSaveState = ctx.PluginCommandStub.GetChunk(true);
            }
            else
            {
                var vstParametersList = new List<float>();
                var paramCount = ctx.PluginInfo.ParameterCount;
                for (int i = 0; i < paramCount; i++)
                {
                    vstParametersList.Add(ctx.PluginCommandStub.GetParameter(i));
                }

                vstParameters = vstParametersList.ToArray();
            }

            // VST навм больше не нужен, закрываем
            ctx.PluginCommandStub.Close();
            if (cancellationToken.IsCancellationRequested)
            {
                // Был затребован выход из программы
                cancellationTaskSource.Cancel();
                return ReturnCodeOffset + 1;
            }

            int result;
            switch (parameters.FxbFileFormat.ToLowerInvariant())
            {
                case "fxb":
                    result = SaveStateAsFxb(parameters.FxbFile, vstSaveState, vstParameters,
                        !parameters.FxbFileAsParams,
                        (uint) ctx.PluginInfo.PluginID, (uint) ctx.PluginInfo.PluginVersion);
                    break;
                case "fxp":
                    result = SaveStateAsFxp(parameters.FxbFile, vstSaveState, vstParameters,
                        !parameters.FxbFileAsParams,
                        (uint) ctx.PluginInfo.PluginID, (uint) ctx.PluginInfo.PluginVersion);
                    break;
                default:
                    Console.Error.WriteLine("Save state format {0} does not supported", parameters.FxbFileFormat);
                    cancellationTaskSource.Cancel();
                    return ReturnCodeOffset + 2;
            }

            Console.WriteLine(
                "VST plugin settings saved to {0} (file format {1}; file subformat: {2})",
                parameters.FxbFile,
                parameters.FxbFileFormat,
                (parameters.FxbFileFormat.ToLowerInvariant() == "fxb")
                    ? "bank param with " + (parameters.FxbFileAsParams ? "preset param" : "preset opaque")
                    : (parameters.FxbFileAsParams ? "preset param" : "preset opaque")
            );

            cancellationTaskSource.Cancel();

            return (result > 0) ? ReturnCodeOffset + result : 0;
        }

        #region Generate Binary data

        public static int SaveStateAsFxb(
            string outputFilename,
            byte[] saveState,
            float[] vstParameters,
            bool isOpaque,
            uint pluginID,
            uint pluginVersion
        )
        {
            const int innerCodeOffset = 10;
            int result = GenerateFxbData(saveState, vstParameters, isOpaque, pluginID, pluginVersion, out var fxbData);
            if (result != 0)
            {
                return result;
            }

            try
            {
                File.WriteAllBytes(outputFilename, fxbData);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Exception: {0}", e);
                return innerCodeOffset;
            }

            return 0;
        }

        public static int SaveStateAsFxp(
            string outputFilename,
            byte[] saveState,
            float[] vstParameters,
            bool isOpaque,
            uint pluginID,
            uint pluginVersion
        )
        {
            const int innerCodeOffset = 20;
            int result = GenerateFxpData(saveState, vstParameters, isOpaque, pluginID, pluginVersion, out var fxpData);
            if (result != 0)
            {
                return result;
            }

            try
            {
                File.WriteAllBytes(outputFilename, fxpData);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Exception: {0}", e);
                return innerCodeOffset;
            }

            return 0;
        }

        private static int GenerateFxpData(
            byte[] saveState,
            float[] vstParameters,
            bool isOpaque,
            uint pluginID,
            uint pluginVersion,
            out byte[] fxpData
        )
        {
            // ReSharper disable once UnusedVariable
            const int innerCodeOffset = 30;
            var fxpDataSubList = new List<byte>();

            if (isOpaque)
            {
                fxpDataSubList.AddRange(WriteUint32(0)); // Num Params
                fxpDataSubList.AddRange(WriteZeroBytes(PresetNameLength)); // Preset name
                fxpDataSubList.AddRange(WriteUint32((uint) saveState.Length)); // Num Params
                fxpDataSubList.AddRange(saveState);
            }
            else
            {
                fxpDataSubList.AddRange(WriteUint32((uint) vstParameters.Length)); // Num Params
                fxpDataSubList.AddRange(WriteZeroBytes(PresetNameLength)); // Preset name

                foreach (var value in vstParameters)
                {
                    fxpDataSubList.AddRange(WriteFloat(value));
                }
            }

            var fxpDataList = new List<byte>();

            fxpDataList.AddRange(WriteString(FxbFileMagicHeader)); // Magic
            fxpDataList.AddRange(WriteUint32((uint) fxpDataSubList.Count + 4 * 4)); // Size
            fxpDataList.AddRange(WriteString(isOpaque ? FxbHeader_Preset_Opaque : FxbHeader_Preset_Param));
            fxpDataList.AddRange(WriteUint32(1));
            fxpDataList.AddRange(WriteUint32(pluginID));
            fxpDataList.AddRange(WriteUint32(pluginVersion));
            fxpDataList.AddRange(fxpDataSubList);

            fxpData = fxpDataList.ToArray();
            return 0;
        }

        private static int GenerateFxbData(
            byte[] saveState,
            float[] vstParameters,
            bool isOpaque,
            uint pluginID,
            uint pluginVersion,
            out byte[] fxbData
        )
        {
            var result = GenerateFxpData(saveState, vstParameters, isOpaque, pluginID, pluginVersion, out var fxpData);
            if (result != 0)
            {
                fxbData = null;
                return result;
            }

            // Always bank params
            var fxbDataSubList = new List<byte>();
            fxbDataSubList.AddRange(WriteUint32(1)); // Num programs
            fxbDataSubList.AddRange(WriteUint32(0)); // Current programs
            fxbDataSubList.AddRange(WriteZeroBytes(FxbReservedLength)); // Num Params
            fxbDataSubList.AddRange(fxpData);

            var fxbDataList = new List<byte>();

            fxbDataList.AddRange(WriteString(FxbFileMagicHeader)); // Magic
            fxbDataList.AddRange(WriteUint32((uint) fxbDataSubList.Count + 4 * 4)); // Size
            fxbDataList.AddRange(WriteString(FxbHeader_Bank_Param));
            fxbDataList.AddRange(WriteUint32(1));
            fxbDataList.AddRange(WriteUint32(pluginID));
            fxbDataList.AddRange(WriteUint32(pluginVersion));
            fxbDataList.AddRange(fxbDataSubList);

            fxbData = fxbDataList.ToArray();
            return 0;
        }

        #endregion

        #region Generate Byte Flow

        private static byte[] WriteString(string value)
        {
            if (value.Length != 4)
            {
                throw new Exception();
            }

            return Encoding.ASCII.GetBytes(value);
        }

        private static byte[] WriteUint32(uint value)
        {
            return BitConverter.GetBytes(value).Reverse().ToArray();
        }

        private static byte[] WriteFloat(float value)
        {
            return BitConverter.GetBytes(value).Reverse().ToArray();
        }

        private static byte[] WriteZeroBytes(int count)
        {
            return new byte[count];
        }

        #endregion
    }
}