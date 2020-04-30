using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Jacobi.Vst.Core;
using Jacobi.Vst.Core.Host;

namespace VSTAudioProcessor.Test.Process
{
    public class PluginStub : Jacobi.Vst.Core.Host.IVstPluginCommandStub
    {
        public readonly Dictionary<int, float> parameters = new Dictionary<int, float>();
        public byte[] rawChunk;

        public byte[] GetChunk(bool isPreset)
        {
            return rawChunk;
        }

        public int SetChunk(byte[] data, bool isPreset)
        {
            rawChunk = data.ToArray();
            return data.Length;
        }

        public void SetParameter(int index, float value)
        {
            parameters[index] = value;
        }

        public float GetParameter(int index)
        {
            return parameters[index];
        }

        #region Stub

        public void EditorClose()
        {
            throw new NotImplementedException();
        }

        public void Open()
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            throw new NotImplementedException();
        }

        public int GetVendorVersion()
        {
            throw new NotImplementedException();
        }

        public VstCanDoResult CanDo(string cando)
        {
            throw new NotImplementedException();
        }

        public int GetTailSize()
        {
            throw new NotImplementedException();
        }

        public VstParameterProperties GetParameterProperties(int index)
        {
            throw new NotImplementedException();
        }

        public int GetVstVersion()
        {
            throw new NotImplementedException();
        }

        public void EditorIdle()
        {
            throw new NotImplementedException();
        }

        public bool EditorOpen(IntPtr hWnd)
        {
            throw new NotImplementedException();
        }

        public VstPluginCategory GetCategory()
        {
            throw new NotImplementedException();
        }

        public bool SetSpeakerArrangement(VstSpeakerArrangement saInput, VstSpeakerArrangement saOutput)
        {
            throw new NotImplementedException();
        }

        public int GetProgram()
        {
            throw new NotImplementedException();
        }

        public void SetProgramName(string name)
        {
            throw new NotImplementedException();
        }

        public string GetProgramName()
        {
            throw new NotImplementedException();
        }

        public string GetParameterLabel(int index)
        {
            throw new NotImplementedException();
        }

        public string GetParameterDisplay(int index)
        {
            throw new NotImplementedException();
        }

        public string GetParameterName(int index)
        {
            throw new NotImplementedException();
        }

        public void SetSampleRate(float sampleRate)
        {
            throw new NotImplementedException();
        }

        public void SetBlockSize(int blockSize)
        {
            throw new NotImplementedException();
        }

        public void MainsChanged(bool onoff)
        {
            throw new NotImplementedException();
        }

        public IVstPluginContext PluginContext { get; set; }

        public bool ProcessEvents(VstEvent[] events)
        {
            throw new NotImplementedException();
        }

        public bool CanParameterBeAutomated(int index)
        {
            throw new NotImplementedException();
        }

        public void ProcessReplacing(VstAudioPrecisionBuffer[] inputs, VstAudioPrecisionBuffer[] outputs)
        {
            throw new NotImplementedException();
        }

        public bool SetBypass(bool bypass)
        {
            throw new NotImplementedException();
        }

        public void SetProgram(int programNumber)
        {
            throw new NotImplementedException();
        }

        public void ProcessReplacing(VstAudioBuffer[] inputs, VstAudioBuffer[] outputs)
        {
            throw new NotImplementedException();
        }

        public int StartProcess()
        {
            throw new NotImplementedException();
        }

        public int StopProcess()
        {
            throw new NotImplementedException();
        }

        public bool SetPanLaw(VstPanLaw type, float gain)
        {
            throw new NotImplementedException();
        }

        public bool String2Parameter(int index, string str)
        {
            throw new NotImplementedException();
        }

        public string GetProgramNameIndexed(int index)
        {
            throw new NotImplementedException();
        }

        public VstCanDoResult BeginLoadBank(VstPatchChunkInfo chunkInfo)
        {
            throw new NotImplementedException();
        }

        public VstCanDoResult BeginLoadProgram(VstPatchChunkInfo chunkInfo)
        {
            throw new NotImplementedException();
        }

        public bool GetMidiKeyName(VstMidiKeyName midiKeyName, int channel)
        {
            throw new NotImplementedException();
        }

        public bool BeginSetProgram()
        {
            throw new NotImplementedException();
        }

        public bool EditorGetRect(out Rectangle rect)
        {
            throw new NotImplementedException();
        }

        public bool EditorKeyDown(byte ascii, VstVirtualKey virtualKey, VstModifierKeys modifers)
        {
            throw new NotImplementedException();
        }

        public bool EditorKeyUp(byte ascii, VstVirtualKey virtualKey, VstModifierKeys modifers)
        {
            throw new NotImplementedException();
        }

        public bool SetEditorKnobMode(VstKnobMode mode)
        {
            throw new NotImplementedException();
        }

        public int GetMidiProgramName(VstMidiProgramName midiProgram, int channel)
        {
            throw new NotImplementedException();
        }

        public int GetCurrentMidiProgramName(VstMidiProgramName midiProgram, int channel)
        {
            throw new NotImplementedException();
        }

        public int GetMidiProgramCategory(VstMidiProgramCategory midiCat, int channel)
        {
            throw new NotImplementedException();
        }

        public bool HasMidiProgramsChanged(int channel)
        {
            throw new NotImplementedException();
        }

        public VstPinProperties GetOutputProperties(int index)
        {
            throw new NotImplementedException();
        }

        public bool EndSetProgram()
        {
            throw new NotImplementedException();
        }

        public string GetEffectName()
        {
            throw new NotImplementedException();
        }

        public string GetVendorString()
        {
            throw new NotImplementedException();
        }

        public string GetProductString()
        {
            throw new NotImplementedException();
        }

        public VstPinProperties GetInputProperties(int index)
        {
            throw new NotImplementedException();
        }

        public bool GetSpeakerArrangement(out VstSpeakerArrangement input, out VstSpeakerArrangement output)
        {
            throw new NotImplementedException();
        }

        public int GetNextPlugin(out string name)
        {
            throw new NotImplementedException();
        }

        public bool SetProcessPrecision(VstProcessPrecision precision)
        {
            throw new NotImplementedException();
        }

        public int GetNumberOfMidiInputChannels()
        {
            throw new NotImplementedException();
        }

        public int GetNumberOfMidiOutputChannels()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}