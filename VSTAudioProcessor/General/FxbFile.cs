namespace VSTAudioProcessor.General
{
    /// <url>
    /// https://forum.cockos.com/showthread.php?t=45198
    /// </url>
    public static class FxbFile
    {
        public const string FxbFileMagicHeader = "CcnK";

        public const string FxbHeader_Preset_Param = "FxCk";
        public const string FxbHeader_Preset_Opaque = "FPCh";
        public const string FxbHeader_Bank_Param = "FxBk";
        public const string FxbHeader_Bank_Opaque = "FBCh";
    }
}