using CommandLine;

namespace VSTAudioProcessor.General
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class CallParameters
    {
        [Option('v', "vst-plugin", Required = true, HelpText = "VST plugin path (to .dll)")]
        public string VstPluginPath { get; set; }

        [Option('s', "fxb", Required = true, HelpText = "FXB or FXP file for VST")]
        public string FxbFile { get; set; }

        [Option('i', "input", Required = false, HelpText = "Input wav-file")]
        public string InputWavFile { get; set; }

        [Option('o', "output", Required = false, HelpText = "Output wav-file")]
        public string OutputWavFile { get; set; }

        [Option("save-fxb", Required = false, Default = false, HelpText = "Save FXB options instead of processing wave file")]
        public bool SaveFxbFile { get; set; }

        [Option("fxb-format", Required = false, Default = "fxb", HelpText = "Save file format (fxb or fxp)")]
        public string FxbFileFormat { get; set; }

        [Option("fxb-as-params", Required = false, Default = false, HelpText = "Save fxb/fxp-file as parameters instead of opaque file")]
        public bool FxbFileAsParams { get; set; }
    }
}