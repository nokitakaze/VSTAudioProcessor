using System;
using System.IO;
using Jacobi.Vst.Core.Host;
using VSTAudioProcessor.General;

namespace VSTAudioProcessor.Process
{
    public static class FxbReader
    {
        private const int ReturnCodeOffset = 300;

        private const string FxbFileMagicHeader = FxbFile.FxbFileMagicHeader;

        private const string FxbHeader_Preset_Param = FxbFile.FxbHeader_Preset_Param;
        private const string FxbHeader_Preset_Opaque = FxbFile.FxbHeader_Preset_Opaque;
        private const string FxbHeader_Bank_Param = FxbFile.FxbHeader_Bank_Param;
        private const string FxbHeader_Bank_Opaque = FxbFile.FxbHeader_Bank_Opaque;
        private const int PresetNameLength = FxbFile.PresetNameLength;
        private const int FxbReservedLength = FxbFile.FxbReservedLength;

        public static int ReadFileIntoPluginStub(
            string filename,
            uint pluginId,
            uint pluginVersion,
            bool pluginVersionIgnore,
            IVstPluginCommandStub pluginCommandStub
        )
        {
            if (!File.Exists(filename))
            {
                return ReturnCodeOffset + 0;
            }

            var bytes = File.ReadAllBytes(filename);
            if (bytes.Length <= 8)
            {
                Console.WriteLine("File {0} too short", filename);

                return ReturnCodeOffset + 1;
            }

            var stream = new ByteReader(bytes);
            var fxbMagic = stream.Read4CharString();
            if (fxbMagic != FxbFileMagicHeader)
            {
                Console.WriteLine("File {0}: Magic header is missed", filename);

                return ReturnCodeOffset + 2;
            }

            var fxbSize = stream.ReadUint();
            if (stream.GetLeftSize() != fxbSize)
            {
                Console.WriteLine("File {0}: file malformed", filename);

                return ReturnCodeOffset + 3;
            }

            const int presetNumber = -1; // TODO: fix

            var result = ReadStreamFromType(
                stream,
                (uint) pluginId,
                (uint) pluginVersion,
                pluginVersionIgnore,
                presetNumber,
                pluginCommandStub
            );

            return (result > 0) ? result + ReturnCodeOffset : 0;
        }

        public static int ReadStreamFromType(
            ByteReader stream,
            uint pluginId,
            uint pluginVersion,
            bool pluginVersionIgnore,
            int presetNumber,
            IVstPluginCommandStub pluginCommandStub
        )
        {
            var streamType = stream.Read4CharString();
            switch (streamType)
            {
                case FxbHeader_Preset_Param:
                    return ReadPresetParamFromVersion(stream, pluginId, pluginVersion,
                        pluginVersionIgnore, pluginCommandStub);
                case FxbHeader_Preset_Opaque:
                    return ReadPresetOpaqueFromVersion(stream, pluginId, pluginVersion,
                        pluginVersionIgnore, pluginCommandStub);
                case FxbHeader_Bank_Param:
                    return ReadBankParamFromVersion(stream, pluginId, pluginVersion,
                        pluginVersionIgnore, presetNumber, pluginCommandStub);
                case FxbHeader_Bank_Opaque:
                    return ReadBankOpaqueFromVersion(stream, pluginId, pluginVersion,
                        pluginVersionIgnore, presetNumber, pluginCommandStub);
                default:
                    return 4;
            }
        }

        private static int ReadCommonFxbHeader(
            ByteReader stream,
            uint pluginId,
            uint pluginVersion,
            bool pluginVersionIgnore,
            bool streamVersionCouldBe2,
            int innerCodeOffset
        )
        {
            var streamVersion = stream.ReadUint();
            if (streamVersion != 1)
            {
                if (!streamVersionCouldBe2 || (streamVersion != 2))
                {
                    return innerCodeOffset + 0;
                }
            }

            if (stream.GetLeftSize() <= 12)
            {
                return innerCodeOffset + 1;
            }

            var streamPluginId = stream.ReadUint();
            if (streamPluginId != pluginId)
            {
                return innerCodeOffset + 2;
            }

            var streamPluginVersion = stream.ReadUint();
            if (streamPluginVersion != pluginVersion)
            {
                Console.Error.WriteLine(
                    "Bank param stream: expected VST Plugin Version {0}, but bank file has {1}",
                    pluginVersion,
                    streamPluginVersion
                );
                if (!pluginVersionIgnore)
                {
                    return innerCodeOffset + 3;
                }
            }

            return 0;
        }

        #region Parse Bank

        private static int ReadBankParamFromVersion(
            ByteReader stream,
            uint pluginId,
            uint pluginVersion,
            bool pluginVersionIgnore,
            int presetNumber,
            IVstPluginCommandStub pluginCommandStub
        )
        {
            const int innerCodeOffset = 20;
            var result = ReadCommonFxbHeader(
                stream,
                pluginId,
                pluginVersion,
                pluginVersionIgnore,
                true,
                innerCodeOffset
            );
            if (result != 0)
            {
                return result;
            }

            var numPrograms = stream.ReadUint();
            if (numPrograms == 0)
            {
                Console.Error.WriteLine("Bank file doesn't contain any presets within");
                return innerCodeOffset + 7;
            }

            int realPresetNumber;
            if (presetNumber != -1)
            {
                if (presetNumber >= numPrograms)
                {
                    Console.Error.WriteLine("Chosen preset number {0} > number of presets {1}",
                        presetNumber, numPrograms);
                    return innerCodeOffset + 5;
                }

                stream.SeekPlus(4);
                realPresetNumber = presetNumber;
            }
            else
            {
                var currentProgram = (int) stream.ReadUint();
                if (currentProgram >= numPrograms)
                {
                    Console.Error.WriteLine("Current bank preset number {0} > number of presets {1}",
                        currentProgram, numPrograms);
                    return innerCodeOffset + 6;
                }

                realPresetNumber = currentProgram;
            }

            stream.SeekPlus(FxbReservedLength);

            for (int id = 0; id <= realPresetNumber; id++)
            {
                var presetMagic = stream.Read4CharString();
                if (presetMagic != FxbFileMagicHeader)
                {
                    Console.WriteLine("Bank file: Magic header is missed in preset #{0}", id);

                    return innerCodeOffset + 8;
                }

                var bytesLength = stream.ReadUint();
                if (stream.GetLeftSize() != bytesLength)
                {
                    return innerCodeOffset + 9;
                }

                result = ReadStreamFromType(
                    stream,
                    pluginId,
                    pluginVersion,
                    pluginVersionIgnore,
                    -2,
                    (id == realPresetNumber) ? pluginCommandStub : null
                );
                if (result != 0)
                {
                    return result;
                }
            }

            return 0;
        }

        private static int ReadBankOpaqueFromVersion(
            ByteReader stream,
            uint pluginId,
            uint pluginVersion,
            bool pluginVersionIgnore,
            int presetNumber,
            IVstPluginCommandStub pluginCommandStub
        )
        {
            const int innerCodeOffset = 40;
            var result = ReadCommonFxbHeader(
                stream,
                pluginId,
                pluginVersion,
                pluginVersionIgnore,
                true,
                innerCodeOffset
            );
            if (result != 0)
            {
                return result;
            }

            var numPrograms = stream.ReadUint();
            if (numPrograms != 1)
            {
                Console.Error.WriteLine("Opaque bank file's presets count != 1");
                return innerCodeOffset + 5;
            }

            if ((presetNumber != -1) && (presetNumber != 0))
            {
                Console.Error.WriteLine("Malformed preset number ({0})", presetNumber);
                return innerCodeOffset + 6;
            }

            stream.SeekPlus(4 + FxbReservedLength);
            var length = stream.ReadUint();
            if (length > stream.GetLeftSize())
            {
                return innerCodeOffset + 7;
            }

            var rawChunk = stream.ReadBytes(length);
            var readBytes = pluginCommandStub.SetChunk(rawChunk, true);
            if (readBytes != length)
            {
                return innerCodeOffset + 8;
            }

            return 0;
        }

        #endregion

        #region Parse Preset

        private static int ReadPresetParamFromVersion(
            ByteReader stream,
            uint pluginId,
            uint pluginVersion,
            bool pluginVersionIgnore,
            IVstPluginCommandStub pluginCommandStub
        )
        {
            const int innerCodeOffset = 60;
            var result = ReadCommonFxbHeader(
                stream,
                pluginId,
                pluginVersion,
                pluginVersionIgnore,
                false,
                innerCodeOffset
            );
            if (result != 0)
            {
                return result;
            }

            var paramCount = stream.ReadUint();
            if (paramCount * 4 + PresetNameLength > stream.GetLeftSize())
            {
                return innerCodeOffset + 5;
            }

            stream.SeekPlus(PresetNameLength);
            if (pluginCommandStub == null)
            {
                stream.SeekPlus(paramCount * 4);
                return 0;
            }

            for (int i = 0; i < paramCount; i++)
            {
                pluginCommandStub.SetParameter(i, stream.ReadFloat());
            }

            return 0;
        }

        private static int ReadPresetOpaqueFromVersion(
            ByteReader stream,
            uint pluginId,
            uint pluginVersion,
            bool pluginVersionIgnore,
            IVstPluginCommandStub pluginCommandStub
        )
        {
            const int innerCodeOffset = 80;
            var result = ReadCommonFxbHeader(
                stream,
                pluginId,
                pluginVersion,
                pluginVersionIgnore,
                false,
                innerCodeOffset
            );
            if (result != 0)
            {
                return result;
            }

            stream.SeekPlus(4 + PresetNameLength); // Param count useless & preset name
            var length = stream.ReadUint();
            if (length > stream.GetLeftSize())
            {
                return innerCodeOffset + 5;
            }

            if (pluginCommandStub != null)
            {
                var rawChunk = stream.ReadBytes(length);
                var readBytes = pluginCommandStub.SetChunk(rawChunk, true);
                if (readBytes != length)
                {
                    Console.Error.WriteLine("VST Plugin says he read {0} bytes, but it should be {1}",
                        readBytes, length);
                }
            }
            else
            {
                stream.SeekPlus(length);
            }

            return 0;
        }

        #endregion
    }
}