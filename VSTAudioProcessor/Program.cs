using System;
using System.Threading;
using CommandLine;
using VSTAudioProcessor.Dialog;
using VSTAudioProcessor.Process;

namespace VSTAudioProcessor
{
    internal static class Program
    {
        public static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        private static int returnCode;

        public static int Main(string[] args)
        {
            try
            {
                Parser.Default
                    .ParseArguments<VSTAudioProcessor.General.CallParameters>(args)
                    .WithParsed(RunOptionsAndReturnExitCode);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);

                return 2;
            }

            return returnCode;
        }

        private static void RunOptionsAndReturnExitCode(VSTAudioProcessor.General.CallParameters parameters)
        {
            Console.CancelKeyPress += consoleCtrlC;
            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
            if (!parameters.SaveFxbFile)
            {
                // Обрабатываем wave-файл
                returnCode = VSTProcessing.ProcessWaveFile(parameters, CancellationTokenSource.Token);
            }
            else
            {
                // Сохраняем настройки VST-плагина
                returnCode = VSTSettings.SetUpPlugin(parameters, CancellationTokenSource.Token);
            }
        }

        private static void consoleCtrlC(object obj, ConsoleCancelEventArgs param)
        {
            CancellationTokenSource.Cancel();
            Console.WriteLine("{0}\tUnloading", DateTime.Now.ToUniversalTime());
        }
    }
}