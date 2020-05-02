# VSTAudioProcessor

VST Host with unix-way availability to processing wav (RIFF) files with VST Plugin with some preset from fxb/fxp-files.

## Using

### Save preset

You could skip this step if you're using different VST Hosts (i.e Wavosaur or Tone2 NanoHost) for creating presets.

```bash
VSTAudioProcessor.exe --vst-plugin "C:\Program Files\Common Files\VST2\Random-VST-Plugin.dll" --save-fxb --fxb "C:\temp\preset.fxb"
VSTAudioProcessor.exe --vst-plugin "C:\Program Files\Common Files\VST2\Random-VST-Plugin.dll" --save-fxb --fxb "C:\temp\preset.fxp" --fxb-format fxp
VSTAudioProcessor.exe --vst-plugin "C:\Program Files\Common Files\VST2\Random-VST-Plugin.dll" --save-fxb --fxb "C:\temp\preset.fxb" --fxb-as-params
VSTAudioProcessor.exe --vst-plugin "C:\Program Files\Common Files\VST2\Random-VST-Plugin.dll" --save-fxb --fxb "C:\temp\preset.fxp" --fxb-as-params --fxb-format fxp
```

Default saving type is opaque, because it saves all data from some big and complicated plugins like IL Minihost Modular. Use `--fxb-as-params` only if you plan to use your preset in any VST hosts, which don't support opaque type (i.e. Wavosaur).

### Use your preset

VSTAudioProcessor supports all 5 different types for preset file (Param preset, opaque preset, opaque bank, param bank + param preset, param bank + opaque preset). You could do preset in any VST Host you usually use and then load it here.

```bash
VSTAudioProcessor.exe --vst-plugin "C:\Program Files\Common Files\VST2\Random-VST-Plugin.dll" --fxb "C:\temp\preset.fxp" --input "C:\temp\input.wav" --output "C:\temp\fxb\output.wav"
```

If your preset contain different Plugin Version from your current VST Plugin, use `--ignore-plugin-version`.

```bash
VSTAudioProcessor.exe --vst-plugin "C:\Program Files\Common Files\VST2\Random-VST-Plugin.dll" --fxb "C:\temp\preset.fxp" --input "C:\temp\input.wav" --output "C:\temp\fxb\output.wav" --ignore-plugin-version
```

If you need to work other media types besides RIFF you could use `ffmpeg` (https://www.ffmpeg.org/download.html#build-windows).

For example:
```bash
ffmpeg -i some-media-file.raw -sn -vn -c:a pcm_s16le temporary.wav
VSTAudioProcessor.exe ... --input temporary.wav --output temporary.out.wav
ffmpeg -i temporary.out.wav -c:a aac -q:a 5 output.aac
```

## TODO
Split this package to separate VST Host and FXB/FXP file processor, and then publish it as .Net Core Nuget package.

This application WILL NEVER support any other media type except RIFF files, because of their simple not complicated nature.

## License

![](https://cdn.discordapp.com/attachments/568223142724763652/705420220398960650/Home_VSTLogoAlpha92x54.png)

VST is a trademark of Steinberg Media Technologies GmbH.

AudioLib is a intellectual property of Daniel Weck ( https://github.com/danielweck ) and DAISY Consortium.

