using System;
using System.Collections.Generic;
using System.IO;
using AudioLib;
using Jacobi.Vst.Core;
using Jacobi.Vst.Core.Host;
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

            MemoryStream inputStream;
            MemoryStream outputStream = new MemoryStream();
            {
                inputStream = new MemoryStream();
                var bytes = System.IO.File.ReadAllBytes(inputFile);
                inputStream.Write(bytes, 0, bytes.Length);
                inputStream.Seek(0, SeekOrigin.Begin);
            }

            var pcmInput = AudioLibPCMFormat.RiffHeaderParse(inputStream, out _);
            var pcmOutput = new AudioLibPCMFormat();
            pcmOutput.CopyFrom(pcmInput);

            pluginContext.PluginCommandStub.SetSampleRate(pcmInput.SampleRate);
            pluginContext.PluginCommandStub.SetProcessPrecision(VstProcessPrecision.Process32);

            // Write stub header
            var audioStreamRiffOffset = pcmOutput.RiffHeaderWrite(outputStream, 0);

            int bytesToTransfer = (int) Math.Min(
                inputStream.Length - (long) audioStreamRiffOffset,
                pcmOutput.ConvertTimeToBytes(1000 * AudioLibPCMFormat.TIME_UNIT)
            );

            // hint: samples per buffer should be equal to pcmInput.SampleRate
            int samplesPerBuffer = (int) Math.Round(bytesToTransfer / 2.0 / pcmOutput.NumberOfChannels);
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

            int bytesReadFromAudioStream;
            byte[] byteBuffer = new byte[bytesToTransfer];
            while ((bytesReadFromAudioStream = inputStream.Read(byteBuffer, 0, bytesToTransfer)) > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return ReturnCodeOffset + 20;
                }

                var result = ProcessSingleBuffer(
                    pluginContext,
                    byteBuffer,
                    bytesReadFromAudioStream,
                    vstBufIn,
                    vstBufOut,
                    vstBufIn2,
                    vstBufOut2,
                    isDoublePrecision,
                    pcmOutput,
                    inputCount,
                    outputCount,
                    samplesPerBuffer
                );

                if (result != 0)
                {
                    return result;
                }

                outputStream.Write(byteBuffer, 0, bytesReadFromAudioStream);
            }

            pluginContext.PluginCommandStub.StopProcess();
            pluginContext.PluginCommandStub.MainsChanged(false);

            {
                outputStream.Seek(0, SeekOrigin.Begin);
                pcmOutput.RiffHeaderWrite(outputStream,
                    (uint) (outputStream.Length - (long) audioStreamRiffOffset));

                inputStream.Dispose();
                var outputBytes = outputStream.ToArray();
                outputStream.Dispose();
                File.WriteAllBytes(outputFile, outputBytes);
            }

            return 0;
        }


        private static int ProcessSingleBuffer(
            IVstPluginContext pluginContext,
            byte[] byteBuffer,
            int bytesReadFromAudioStream,
            VstAudioBuffer[] vstBufIn,
            VstAudioBuffer[] vstBufOut,
            VstAudioPrecisionBuffer[] vstBufIn2,
            VstAudioPrecisionBuffer[] vstBufOut2,
            bool isDoublePrecision,
            AudioLibPCMFormat pcmOutput,
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
                byteBuffer,
                bytesReadFromAudioStream,
                vstBufIn,
                vstBufIn2,
                isDoublePrecision,
                pcmOutput,
                vstOutputCount
            );
            if (result != 0)
            {
                return result;
            }

            {
                var t = new List<float>();
                for (int i = 0; i < samplesPerBuffer; i++)
                {
                    t.Add(vstBufIn[0][i]);
                }

                Console.WriteLine(t);
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
                byteBuffer,
                bytesReadFromAudioStream,
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
        /// <param name="byteBuffer"></param>
        /// <param name="bytesReadFromAudioStream"></param>
        /// <param name="vstBufIn"></param>
        /// <param name="vstBufIn2"></param>
        /// <param name="isDoublePrecision"></param>
        /// <param name="pcmOutput"></param>
        /// <param name="vstOutputCount"></param>
        /// <returns></returns>
        private static int ProcessSingleBufferFillBufferInput(
            byte[] byteBuffer,
            int bytesReadFromAudioStream,
            IReadOnlyList<VstAudioBuffer> vstBufIn,
            IReadOnlyList<VstAudioPrecisionBuffer> vstBufIn2,
            bool isDoublePrecision,
            AudioLibPCMFormat pcmOutput,
            int vstOutputCount
        )
        {
            var iSample = 0;
            for (int i = 0; i < bytesReadFromAudioStream; iSample++)
            {
                for (int channel = 0; channel < pcmOutput.NumberOfChannels; channel++)
                {
                    if (i + 1 >= bytesReadFromAudioStream)
                    {
                        break;
                    }

                    var sample = BitConverter.ToInt16(byteBuffer, i);
                    i += 2;

                    if (channel >= vstOutputCount)
                    {
                        continue;
                    }

                    if (!isDoublePrecision)
                    {
                        float sampleF = sample / 32768f;
                        if (sampleF > 1.0f)
                        {
                            sampleF = 1.0f;
                        }

                        if (sampleF < -1.0f)
                        {
                            sampleF = -1.0f;
                        }

                        vstBufIn[channel][iSample] = sampleF;
                    }
                    else
                    {
                        double sampleD = sample / 32768d;
                        if (sampleD > 1.0d)
                        {
                            sampleD = 1.0d;
                        }

                        if (sampleD < -1.0d)
                        {
                            sampleD = -1.0d;
                        }

                        vstBufIn2[channel][iSample] = sampleD;
                    }
                }
            } // int i = 0; i < bytesReadFromAudioStream

            return 0;
        }

        private static int ProcessSingleBufferFillByteBuffer(
            byte[] byteBuffer,
            int bytesReadFromAudioStream,
            IReadOnlyList<VstAudioBuffer> vstBufOut,
            IReadOnlyList<VstAudioPrecisionBuffer> vstBufOut2,
            bool isDoublePrecision,
            AudioLibPCMFormat pcmOutput,
            int vstOutputCount,
            int samplesPerBuffer
        )
        {
            int iByte = 0;
            Array.Clear(byteBuffer, 0, byteBuffer.Length);
            for (var iSample = 0; iSample < samplesPerBuffer; iSample++)
            {
                for (int channel = 0; channel < pcmOutput.NumberOfChannels; channel++)
                {
                    short sample = 0;

                    if (channel < vstOutputCount)
                    {
                        if (!isDoublePrecision)
                        {
                            float sampleF = vstBufOut[channel][iSample];
                            sampleF *= 32768f;
                            if (sampleF > short.MaxValue)
                            {
                                sampleF = short.MaxValue;
                            }

                            if (sampleF < short.MinValue)
                            {
                                sampleF = short.MinValue;
                            }

                            sample = (short) Math.Round(sampleF);
                        }
                        else
                        {
                            double sampleD = vstBufOut2[channel][iSample];
                            sampleD *= 32768d;
                            if (sampleD > short.MaxValue)
                            {
                                sampleD = short.MaxValue;
                            }

                            if (sampleD < short.MinValue)
                            {
                                sampleD = short.MinValue;
                            }

                            sample = (short) Math.Round(sampleD);
                        }
                    }

                    if (iByte + 1 >= bytesReadFromAudioStream)
                    {
                        break;
                    }

                    byte[] sampleBytes = BitConverter.GetBytes(sample);
                    Array.Copy(sampleBytes, 0, byteBuffer, iByte, 2);
                    iByte += 2;
                }
            }

            return 0;
        }
    }
}