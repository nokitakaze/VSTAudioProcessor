using System;
using System.Collections.Generic;
using Jacobi.Vst.Core;
using Jacobi.Vst.Core.Host;
using Jacobi.Vst.Interop.Host;
using NokitaKaze.WAVParser;
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
            var vstPlugin = VstPluginContext.Create(pluginPath, hostCmdStub);
            vstPlugin.PluginCommandStub.Open();

            // add custom data to the context
            vstPlugin.Set("PluginPath", pluginPath);
            vstPlugin.Set("HostCmdStub", hostCmdStub);


            #region Draw VST Plugin Information

            // plugin product
            Console.WriteLine("{0,-20}\t{1}", "Plugin Name", vstPlugin.PluginCommandStub.GetEffectName());
            Console.WriteLine("{0,-20}\t{1}", "Product", vstPlugin.PluginCommandStub.GetProductString());
            Console.WriteLine("{0,-20}\t{1}", "Vendor", vstPlugin.PluginCommandStub.GetVendorString());
            Console.WriteLine("{0,-20}\t{1}", "Vendor Version",
                vstPlugin.PluginCommandStub.GetVendorVersion().ToString());
            Console.WriteLine("{0,-20}\t{1}", "Vst Support", vstPlugin.PluginCommandStub.GetVstVersion().ToString());
            Console.WriteLine("{0,-20}\t{1}", "Plugin Category", vstPlugin.PluginCommandStub.GetCategory().ToString());

            // plugin info
            Console.WriteLine("{0,-20}\t{1}", "Flags", vstPlugin.PluginInfo.Flags.ToString());
            Console.WriteLine("{0,-20}\t{1}", "Plugin ID", vstPlugin.PluginInfo.PluginID.ToString());
            Console.WriteLine("{0,-20}\t{1}", "Plugin Version", vstPlugin.PluginInfo.PluginVersion.ToString());

            #endregion

            {
                var t = vstPlugin.PluginCommandStub.CanDo(VstCanDoHelper.ToString(VstPluginCanDo.Offline));
                if (t == VstCanDoResult.No)
                {
                    Console.Error.WriteLine("This VST Plugin does not support offline convertation");

                    return ReturnCodeOffset + 6;
                }

                if (!vstPlugin.PluginInfo.Flags.HasFlag(VstPluginFlags.CanReplacing) &&
                    !vstPlugin.PluginInfo.Flags.HasFlag(VstPluginFlags.CanDoubleReplacing))
                {
                    Console.Error.WriteLine("This VST Plugin does not replacing samples");

                    return ReturnCodeOffset + 7;
                }
            }

            vstPlugin.PluginCommandStub.MainsChanged(true);
            int result = FxbReader.ReadFileIntoPluginStub(
                parameters.FxbFile,
                (uint) vstPlugin.PluginInfo.PluginID,
                (uint) vstPlugin.PluginInfo.PluginVersion,
                parameters.IgnorePluginVersion,
                vstPlugin.PluginCommandStub
            );
            vstPlugin.PluginCommandStub.MainsChanged(false);
            if (result != 0)
            {
                vstPlugin.PluginCommandStub.Close();
                return result;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                // Был затребован выход из программы
                vstPlugin.PluginCommandStub.Close();
                return ReturnCodeOffset + 5;
            }

            result = ProcessFile(parameters.InputWavFile, parameters.OutputWavFile, vstPlugin, cancellationToken);
            vstPlugin.PluginCommandStub.Close();

            return result;
        }

        private static T[] NumeratorToArray<T>(IEnumerator<T> source)
        {
            var list = new List<T>();

            while (source.MoveNext())
            {
                list.Add(source.Current);
            }

            return list.ToArray();
        }

        public static int ProcessFile(
            string inputFile,
            string outputFile,
            VstPluginContext pluginContext,
            System.Threading.CancellationToken cancellationToken
        )
        {
            var isDoublePrecision = pluginContext.PluginInfo.Flags.HasFlag(VstPluginFlags.CanDoubleReplacing);

            var pcmInput = new WAVParser(inputFile);
            var pcmOutput = new WAVParser
            {
                ChannelCount = pcmInput.ChannelCount,
                Samples = new List<List<double>>(),
                SampleRate = pcmInput.SampleRate,
            };
            for (int i = 0; i < pcmOutput.ChannelCount; i++)
            {
                pcmOutput.Samples.Add(new List<double>());
            }

            pluginContext.PluginCommandStub.SetSampleRate(pcmInput.SampleRate);
            pluginContext.PluginCommandStub.SetProcessPrecision(VstProcessPrecision.Process32);

            // hint: samples per buffer should be equal to pcmInput.SampleRate
            int samplesPerBuffer = (int) pcmInput.SampleRate;
            pluginContext.PluginCommandStub.SetBlockSize(samplesPerBuffer);

            int inputCount = pluginContext.PluginInfo.AudioInputCount;
            int outputCount = pluginContext.PluginInfo.AudioOutputCount;

            VstAudioBuffer[] vstBufIn = null, vstBufOut = null;
            VstAudioPrecisionBuffer[] vstBufIn2 = null, vstBufOut2 = null;

            if (isDoublePrecision)
            {
                var vstBufManIn = new VstAudioPrecisionBufferManager(inputCount, samplesPerBuffer);
                var vstBufManOut = new VstAudioPrecisionBufferManager(outputCount, samplesPerBuffer);

                vstBufIn2 = NumeratorToArray(vstBufManIn.GetEnumerator());
                vstBufOut2 = NumeratorToArray(vstBufManOut.GetEnumerator());
            }
            else
            {
                var vstBufManIn = new VstAudioBufferManager(inputCount, samplesPerBuffer);
                var vstBufManOut = new VstAudioBufferManager(outputCount, samplesPerBuffer);

                vstBufIn = NumeratorToArray(vstBufManIn.GetEnumerator());
                vstBufOut = NumeratorToArray(vstBufManOut.GetEnumerator());
            }

            pluginContext.PluginCommandStub.MainsChanged(true);
            pluginContext.PluginCommandStub.StartProcess();

            for (int samplesOffset = 0;
                samplesOffset < pcmInput.SamplesCount;
                samplesOffset += (int) pcmInput.SampleRate)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return ReturnCodeOffset + 20;
                }

                var result = ProcessSingleBuffer(
                    pluginContext,
                    samplesOffset,
                    vstBufIn,
                    vstBufOut,
                    vstBufIn2,
                    vstBufOut2,
                    isDoublePrecision,
                    pcmInput,
                    pcmOutput,
                    inputCount,
                    outputCount,
                    samplesPerBuffer
                );

                if (result != 0)
                {
                    return result;
                }
            }

            // Close VST Context
            pluginContext.PluginCommandStub.StopProcess();
            pluginContext.PluginCommandStub.MainsChanged(false);

            // Save
            pcmOutput.Save(outputFile);

            return 0;
        }


        private static int ProcessSingleBuffer(
            IVstPluginContext pluginContext,
            int samplesOffset,
            VstAudioBuffer[] vstBufIn,
            VstAudioBuffer[] vstBufOut,
            VstAudioPrecisionBuffer[] vstBufIn2,
            VstAudioPrecisionBuffer[] vstBufOut2,
            bool isDoublePrecision,
            WAVParser pcmInput,
            WAVParser pcmOutput,
            int vstInputCount,
            int vstOutputCount,
            int samplesPerBuffer
        )
        {
            var result = ProcessSingleBufferClearBuffers(
                vstBufIn,
                vstBufOut,
                vstBufIn2,
                vstBufOut2,
                isDoublePrecision,
                vstInputCount,
                vstOutputCount,
                samplesPerBuffer
            );
            if (result != 0)
            {
                return result;
            }

            result = ProcessSingleBufferFillBufferInput(
                samplesOffset,
                vstBufIn,
                vstBufIn2,
                isDoublePrecision,
                pcmInput,
                pcmOutput,
                vstOutputCount
            );
            if (result != 0)
            {
                return result;
            }

            if (isDoublePrecision)
            {
                pluginContext.PluginCommandStub.ProcessReplacing(vstBufIn2, vstBufOut2);
            }
            else
            {
                pluginContext.PluginCommandStub.ProcessReplacing(vstBufIn, vstBufOut);
            }

            result = ProcessSingleBufferFillByteBuffer(
                vstBufOut,
                vstBufOut2,
                isDoublePrecision,
                pcmOutput,
                vstOutputCount,
                samplesPerBuffer
            );

            return result;
        }

        private static int ProcessSingleBufferClearBuffers(
            IReadOnlyList<VstAudioBuffer> vstBufIn,
            IReadOnlyList<VstAudioBuffer> vstBufOut,
            IReadOnlyList<VstAudioPrecisionBuffer> vstBufIn2,
            IReadOnlyList<VstAudioPrecisionBuffer> vstBufOut2,
            bool isDoublePrecision,
            int vstInputCount,
            int vstOutputCount,
            int samplesPerBuffer
        )
        {
            // hint: Array.Clear isn't possible, because vstBufIn2[channel] is not an array
            int iSample;
            for (iSample = 0; iSample < samplesPerBuffer; iSample++)
            {
                for (int channel = 0; channel < vstInputCount; channel++)
                {
                    if (isDoublePrecision)
                    {
                        vstBufIn2[channel][iSample] = 0.0;
                    }
                    else
                    {
                        vstBufIn[channel][iSample] = 0.0f;
                    }
                }

                for (int channel = 0; channel < vstOutputCount; channel++)
                {
                    if (isDoublePrecision)
                    {
                        vstBufOut2[channel][iSample] = 1.0;
                    }
                    else
                    {
                        vstBufOut[channel][iSample] = 1.0f;
                    }
                }
            }

            return 0;
        }

        /// <summary>
        /// Read 2-bytes octets from byteBuffer and put them to VstAudioBuffer
        /// </summary>
        /// <param name="samplesOffset"></param>
        /// <param name="vstBufIn"></param>
        /// <param name="vstBufIn2"></param>
        /// <param name="isDoublePrecision"></param>
        /// <param name="pcmInput"></param>
        /// <param name="pcmOutput"></param>
        /// <param name="vstOutputCount"></param>
        /// <returns></returns>
        private static int ProcessSingleBufferFillBufferInput(
            int samplesOffset,
            IReadOnlyList<VstAudioBuffer> vstBufIn,
            IReadOnlyList<VstAudioPrecisionBuffer> vstBufIn2,
            bool isDoublePrecision,
            WAVParser pcmInput,
            WAVParser pcmOutput,
            int vstOutputCount
        )
        {
            for (int i = samplesOffset;
                i < Math.Min(pcmInput.SamplesCount, samplesOffset + pcmInput.SampleRate);
                i++)
            {
                var iSample = i - samplesOffset;
                for (int channel = 0; channel < pcmOutput.ChannelCount; channel++)
                {
                    if (channel >= vstOutputCount)
                    {
                        continue;
                    }

                    double sample = pcmInput.Samples[channel][i];

                    if (!isDoublePrecision)
                    {
                        vstBufIn[channel][iSample] = (float) sample;
                    }
                    else
                    {
                        vstBufIn2[channel][iSample] = sample;
                    }
                }
            } // int i = 0; i < bytesReadFromAudioStream

            return 0;
        }

        private static int ProcessSingleBufferFillByteBuffer(
            IReadOnlyList<VstAudioBuffer> vstBufOut,
            IReadOnlyList<VstAudioPrecisionBuffer> vstBufOut2,
            bool isDoublePrecision,
            WAVParser pcmOutput,
            int vstOutputCount,
            int samplesPerBuffer
        )
        {
            for (var iSample = 0; iSample < samplesPerBuffer; iSample++)
            {
                for (int channel = 0; channel < pcmOutput.ChannelCount; channel++)
                {
                    if (channel < vstOutputCount)
                    {
                        if (!isDoublePrecision)
                        {
                            float sampleF = vstBufOut[channel][iSample];
                            pcmOutput.Samples[channel].Add(sampleF);
                        }
                        else
                        {
                            double sampleD = vstBufOut2[channel][iSample];
                            pcmOutput.Samples[channel].Add(sampleD);
                        }
                    }
                }
            }

            return 0;
        }
    }
}